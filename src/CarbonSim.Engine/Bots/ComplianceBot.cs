using CarbonSim.Engine.Allocation;
using CarbonSim.Engine.Clock;
using CarbonSim.Engine.Domain;
using CarbonSim.Engine.Market;
using CarbonSim.Engine.Market.Exchange;
using CarbonSim.Engine.Randomness;
using CarbonSim.Engine.Reporting;

namespace CarbonSim.Engine.Bots;

/// <summary>
/// A cost-minimising compliance agent, built to the rules in the fidelity brief: it works out
/// what it is short, buys abatement while that is cheaper than the allowances it would
/// otherwise need, bids for part of the rest at auction, and works the secondary markets for
/// what it still needs or has too much of. It values offsets at a discount to allowances,
/// because they can only be used against a share of the obligation.
/// </summary>
/// <remarks>
/// A bot only ever acts when the host asks it to, taking the time from the clock: this way a
/// whole year can be played in a test without waiting. Its trigger times are drawn once from
/// the simulation's seeded stream, so two hundred bots do not all move on the same tick and the
/// same seed always produces the same run.
/// </remarks>
public sealed class ComplianceBot
{
    private readonly Company _company;
    private readonly List<Unit> _units;
    private readonly BotSettings _settings;
    private readonly SimulationRandom _random;
    private readonly Dictionary<BotTrigger, decimal> _triggerAt;
    private readonly HashSet<(int Year, BotTrigger Trigger)> _done = [];
    private readonly HashSet<(int Year, int Section)> _bidIn = [];
    private decimal _expectedPrice;

    internal ComplianceBot(
        Company company,
        IReadOnlyList<Unit> units,
        BotSettings settings,
        SimulationRandom random,
        decimal referencePrice)
    {
        _company = company;
        _units = [.. units];
        _settings = settings;
        _random = random;
        _expectedPrice = referencePrice;

        // Fixed fractions of the year with a little jitter, drawn in one deterministic order.
        _triggerAt = new Dictionary<BotTrigger, decimal>
        {
            [BotTrigger.Abatement] = random.NextDecimal(0.01m, 0.04m),
            [BotTrigger.Trade] = random.NextDecimal(0.40m, 0.60m),
        };
    }

    public Company Company => _company;

    public IReadOnlyList<Unit> Units => _units;

    public BotSettings Settings => _settings;

    /// <summary>What the bot currently thinks an allowance is worth.</summary>
    public decimal ExpectedAllowancePrice => _expectedPrice;

    /// <summary>When in the year this bot acts, as a fraction of the year.</summary>
    public decimal TriggerTime(BotTrigger trigger) => _triggerAt[trigger];

    /// <summary>
    /// Gives the bot its turn. Whatever is due for the time the clock is showing gets done,
    /// once; anything the markets refuse is reported and skipped rather than thrown, so one
    /// bot's failed order cannot stop the run.
    /// </summary>
    public IReadOnlyList<BotAction> Act(Simulation simulation, SimulationClock clock, BotMarkets markets)
    {
        ArgumentNullException.ThrowIfNull(simulation);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(markets);

        List<BotAction> actions = [];

        if (clock.State != SimulationState.Running || _units.Count == 0)
        {
            return actions;
        }

        int year = clock.CurrentYear;
        decimal elapsed = (decimal)clock.ElapsedInYear.TotalMinutes / (decimal)clock.YearLength.TotalMinutes;

        LearnPrices(simulation, year);

        if (Due(year, BotTrigger.Abatement, elapsed))
        {
            Abate(simulation, year, actions);
        }

        if (clock.IsAuctionOpen && _bidIn.Add((year, clock.CurrentAuction)))
        {
            Bid(simulation, year, clock.CurrentAuction, markets, actions);
        }

        if (Due(year, BotTrigger.Trade, elapsed))
        {
            Trade(simulation, year, markets, actions);
        }

        return actions;
    }

    private bool Due(int year, BotTrigger trigger, decimal elapsed)
    {
        return elapsed >= _triggerAt[trigger] && _done.Add((year, trigger));
    }

    /// <summary>Learns from whatever the market has most recently shown.</summary>
    private void LearnPrices(Simulation simulation, int year)
    {
        MarketTrade? lastAuction = simulation.Journal
            .Between(1, year, TradeChannel.Auction, ProductKind.Allowance)
            .LastOrDefault();

        MarketTrade? lastTrade = simulation.Journal
            .Between(1, year, kind: ProductKind.Allowance)
            .Where(trade => trade.Channel != TradeChannel.Auction)
            .LastOrDefault();

        if (lastAuction is not null)
        {
            _expectedPrice = lastAuction.Price;
        }
        else if (lastTrade is not null)
        {
            _expectedPrice = lastTrade.Price;
        }
    }

    private void Abate(Simulation simulation, int year, List<BotAction> actions)
    {
        decimal reservation = _expectedPrice * _settings.AbatementMargin;
        int remainingYears = simulation.Allocation.Years.Count - year + 1;

        foreach (Unit unit in _units)
        {
            foreach (AbatementOption option in unit.AbatementOptions.OrderBy(option => option.NetCostPerTonne))
            {
                if (simulation.Abatements.HasImplemented(unit, option)
                    || option.NetCostPerTonne >= reservation
                    || option.ImplementationYears > remainingYears)
                {
                    continue;
                }

                if (!simulation.Cash.CanAfford(_company, option.UpfrontCost))
                {
                    continue;
                }

                try
                {
                    simulation.Abatements.Implement(unit, option, year);
                    actions.Add(new BotAction(_company.Name, year, BotTrigger.Abatement, $"implemented {option.Code} at {option.NetCostPerTonne} per tonne"));
                }
                catch (InvalidOperationException exception)
                {
                    actions.Add(new BotAction(_company.Name, year, BotTrigger.Abatement, $"could not implement {option.Code}: {exception.Message}"));
                }
            }
        }
    }

    private void Bid(Simulation simulation, int year, int section, BotMarkets markets, List<BotAction> actions)
    {
        Parameters parameters = simulation.TradingSystems.Single().Parameters;
        Auction auction = markets.Schedule.ForSection(year, section);
        decimal shortfall = Shortfall(simulation, year);

        if (shortfall <= 0m)
        {
            return;
        }

        decimal wanted = shortfall * _settings.BidVolumeFraction;
        decimal noise = _random.NextDecimal(-_settings.BidPriceNoise, _settings.BidPriceNoise);
        decimal price = Clamp(NextMarginalAbatementCost(simulation) is { } cost && cost < _expectedPrice
            ? cost
            : _expectedPrice * (1m + noise), parameters.AuctionFloorPrice, parameters.AuctionCeilingPrice);
        decimal affordable = simulation.Cash.Available(_company) + _company.OverdraftLimit;
        decimal volume = Math.Min(wanted, affordable / price);

        if (volume <= 0m)
        {
            actions.Add(new BotAction(_company.Name, year, BotTrigger.Auction, "could not afford a bid"));
            return;
        }

        try
        {
            auction.PlaceBid(_units[0], auction.Lots[0].Vintage, price, volume);
            actions.Add(new BotAction(_company.Name, year, BotTrigger.Auction, $"bid {volume} at {price}"));
        }
        catch (ArgumentException exception)
        {
            actions.Add(new BotAction(_company.Name, year, BotTrigger.Auction, $"bid refused: {exception.Message}"));
        }
        catch (InvalidOperationException exception)
        {
            actions.Add(new BotAction(_company.Name, year, BotTrigger.Auction, $"bid refused: {exception.Message}"));
        }
    }

    private void Trade(Simulation simulation, int year, BotMarkets markets, List<BotAction> actions)
    {
        Parameters parameters = simulation.TradingSystems.Single().Parameters;
        decimal shortfall = Shortfall(simulation, year);
        decimal offsetValue = _expectedPrice * (1m - _settings.OffsetDiscount);
        decimal offsetLimit = simulation.Allocation.BausEmissionsFor(_company, year) * parameters.OffsetUsageLimit;
        decimal offsetsHeld = simulation.Ledger.Held(_company, Product.Offset);

        // Surplus offsets are worth more sold than kept, because only a share of them can be used.
        if (offsetsHeld > offsetLimit)
        {
            Sell(markets, Product.Offset, offsetsHeld - offsetLimit, offsetValue, actions, year, "surplus offsets");
        }

        if (shortfall <= 0m)
        {
            // Long: offer what cannot be banked.
            decimal held = HeldAllowances(simulation, year);
            decimal bankable = Math.Round(simulation.Allocation.BausEmissionsFor(_company, year) * parameters.BankingLimit, 0, MidpointRounding.AwayFromZero);
            decimal surplus = held - bankable;

            if (surplus > 0m)
            {
                Sell(markets, Product.Allowance(year), surplus, _expectedPrice * _settings.ReservationPriceFactor, actions, year, "surplus allowances");
            }

            return;
        }

        decimal offsetsWanted = Math.Min(shortfall, offsetLimit - offsetsHeld);

        if (offsetsWanted > 0m)
        {
            Buy(markets, Product.Offset, offsetsWanted, offsetValue, actions, year, "offsets");
        }

        Buy(markets, Product.Allowance(year), shortfall - offsetsWanted, _expectedPrice * _settings.ReservationPriceFactor, actions, year, "allowances");
    }

    private void Buy(BotMarkets markets, Product product, decimal volume, decimal limitPrice, List<BotAction> actions, int year, string what)
    {
        OrderBook book = markets.Exchange.Book(product);
        decimal price = Clamp(limitPrice, book.BandFloor, book.BandCeiling);
        decimal affordable = markets.Exchange.CashOf(_company) / price;
        decimal wanted = Math.Min(volume, affordable);

        if (wanted <= 0m)
        {
            return;
        }

        try
        {
            // A limit order is good until cancelled, so it can rest and wait for a seller.
            Order order = markets.Exchange.Place(new OrderRequest(_units[0], product, OrderSide.Buy, OrderKind.Limit, wanted, price));
            actions.Add(new BotAction(_company.Name, year, BotTrigger.Trade, $"bid {wanted} {what} at {price}, {order.FilledVolume} filled"));
        }
        catch (InvalidOperationException exception)
        {
            actions.Add(new BotAction(_company.Name, year, BotTrigger.Trade, $"could not buy {what}: {exception.Message}"));
        }
    }

    private void Sell(BotMarkets markets, Product product, decimal volume, decimal limitPrice, List<BotAction> actions, int year, string what)
    {
        OrderBook book = markets.Exchange.Book(product);
        decimal price = Clamp(limitPrice, book.BandFloor, book.BandCeiling);
        decimal wanted = Math.Min(volume, markets.Exchange.HoldingsOf(_company, product));

        if (wanted <= 0m)
        {
            return;
        }

        try
        {
            Order order = markets.Exchange.Place(new OrderRequest(_units[0], product, OrderSide.Sell, OrderKind.Limit, wanted, price));
            actions.Add(new BotAction(_company.Name, year, BotTrigger.Trade, $"offered {wanted} {what} at {price}, {order.FilledVolume} filled"));
        }
        catch (InvalidOperationException exception)
        {
            actions.Add(new BotAction(_company.Name, year, BotTrigger.Trade, $"could not sell {what}: {exception.Message}"));
        }
    }

    /// <summary>What the company still has to find this year, after what it already holds.</summary>
    public decimal Shortfall(Simulation simulation, int year)
    {
        decimal emissions = new CompliancePosition(simulation).EmissionsFor(_company, year);
        decimal offsets = Math.Min(
            simulation.Ledger.Available(_company, Product.Offset),
            Math.Round(emissions * simulation.TradingSystems.Single().Parameters.OffsetUsageLimit, 0, MidpointRounding.AwayFromZero));
        decimal shortfall = emissions - offsets - HeldAllowances(simulation, year);

        return shortfall < 0m ? 0m : shortfall;
    }

    private decimal HeldAllowances(Simulation simulation, int year)
    {
        decimal held = 0m;

        for (int vintage = 0; vintage <= year; vintage++)
        {
            held += simulation.Ledger.Held(_company, Product.Allowance(vintage));
        }

        return held;
    }

    /// <summary>The cheapest abatement this bot has not taken yet, which is what caps its bid.</summary>
    private decimal? NextMarginalAbatementCost(Simulation simulation)
    {
        decimal? cheapest = null;

        foreach (Unit unit in _units)
        {
            foreach (AbatementOption option in unit.AbatementOptions.Where(option => !simulation.Abatements.HasImplemented(unit, option)))
            {
                if (cheapest is null || option.NetCostPerTonne < cheapest)
                {
                    cheapest = option.NetCostPerTonne;
                }
            }
        }

        return cheapest;
    }

    private static decimal Clamp(decimal value, decimal min, decimal max) =>
        value < min ? min : value > max ? max : Math.Round(value, 2, MidpointRounding.AwayFromZero);
}
