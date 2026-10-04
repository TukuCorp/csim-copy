using CarbonSim.Engine.Allocation;
using CarbonSim.Engine.Clock;
using CarbonSim.Engine.Domain;
using CarbonSim.Engine.Market;
using CarbonSim.Engine.Market.Exchange;
using CarbonSim.Engine.Randomness;
using CarbonSim.Engine.Reporting;
using CarbonSim.Engine.Snapshot;

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
        : this(
            company,
            units,
            settings,
            random,
            referencePrice,
            new Dictionary<BotTrigger, decimal>
            {
                // Fixed fractions of the year with a little jitter, drawn in one deterministic order.
                [BotTrigger.Abatement] = random.NextDecimal(0.01m, 0.04m),
                [BotTrigger.Trade] = random.NextDecimal(0.40m, 0.60m),
            })
    {
    }

    /// <summary>
    /// Builds a bot with trigger times already in hand. Only a restore uses this: the times came
    /// out of the simulation's random stream, so re-drawing them on load would both move the
    /// stream on and change when every bot in the run acts.
    /// </summary>
    private ComplianceBot(
        Company company,
        IReadOnlyList<Unit> units,
        BotSettings settings,
        SimulationRandom random,
        decimal expectedPrice,
        Dictionary<BotTrigger, decimal> triggerAt)
    {
        _company = company;
        _units = [.. units];
        _settings = settings;
        _random = random;
        _expectedPrice = expectedPrice;
        _triggerAt = triggerAt;
    }

    public Company Company => _company;

    public IReadOnlyList<Unit> Units => _units;

    public BotSettings Settings => _settings;

    /// <summary>What the bot currently thinks an allowance is worth.</summary>
    public decimal ExpectedAllowancePrice => _expectedPrice;

    /// <summary>When in the year this bot acts, as a fraction of the year.</summary>
    public decimal TriggerTime(BotTrigger trigger) => _triggerAt[trigger];

    /// <summary>
    /// Everything the bot has decided and is waiting to do: the units it trades for, its
    /// trigger times, what it thinks an allowance is worth, which triggers it has already acted
    /// on this year and which auction sections it has already bid into. Without the last two, a
    /// restored bot would repeat this year's abatement and bid into the same auction twice.
    /// </summary>
    internal BotSnapshot ToSnapshot()
    {
        return new BotSnapshot(
            _company.Id,
            [.. _units.Select(unit => unit.Id)],
            new BotSettingsSnapshot(
                _settings.Difficulty,
                _settings.AbatementMargin,
                _settings.BidPriceNoise,
                _settings.BidVolumeFraction,
                _settings.OffsetDiscount,
                _settings.ReservationPriceFactor),
            _expectedPrice,
            [.. _triggerAt
                .OrderBy(pair => pair.Key)
                .Select(pair => new BotTriggerTimeSnapshot(pair.Key, pair.Value))],
            [.. _done
                .OrderBy(entry => entry.Year)
                .ThenBy(entry => entry.Trigger)
                .Select(entry => new BotTriggerDoneSnapshot(entry.Year, entry.Trigger))],
            [.. _bidIn
                .OrderBy(entry => entry.Year)
                .ThenBy(entry => entry.Section)
                .Select(entry => new BotSectionSnapshot(entry.Year, entry.Section))]);
    }

    /// <summary>Rebuilds a bot with the trigger times and progress a snapshot found it with.</summary>
    internal static ComplianceBot Restore(
        Company company,
        IReadOnlyList<Unit> units,
        SimulationRandom random,
        BotSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(company);
        ArgumentNullException.ThrowIfNull(units);
        ArgumentNullException.ThrowIfNull(random);
        ArgumentNullException.ThrowIfNull(snapshot);

        ComplianceBot bot = new(
            company,
            units,
            Rebuild(snapshot.Settings),
            random,
            snapshot.ExpectedPrice,
            snapshot.TriggerTimes.ToDictionary(time => time.Trigger, time => time.AtFractionOfYear));

        foreach (BotTriggerDoneSnapshot done in snapshot.Done)
        {
            bot._done.Add((done.Year, done.Trigger));
        }

        foreach (BotSectionSnapshot section in snapshot.BidIn)
        {
            bot._bidIn.Add((section.Year, section.Section));
        }

        return bot;
    }

    private static BotSettings Rebuild(BotSettingsSnapshot snapshot)
    {
        return new BotSettings(
            snapshot.Difficulty,
            snapshot.AbatementMargin,
            snapshot.BidPriceNoise,
            snapshot.BidVolumeFraction,
            snapshot.OffsetDiscount,
            snapshot.ReservationPriceFactor);
    }

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
            // The first auctions of the year have cleared by now, so the bot re-reads the
            // market before it invests: a price that has climbed makes the longer-payback
            // abatement worth building before the year is out.
            Abate(simulation, year, actions);
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
                // The project only pays back over the years of the run it will actually be
                // running, so its cost per tonne is the capital spread over those years, not
                // over its whole service life. In a short exercise that is what stops a bot
                // building a long project for the couple of years left to play.
                int operatingYears = Math.Min(option.LifetimeYears, remainingYears - option.ImplementationYears);
                decimal costPerTonne = operatingYears < 1
                    ? decimal.MaxValue
                    : option.UpfrontCost / (option.AnnualReduction * operatingYears);

                if (simulation.Abatements.HasImplemented(unit, option)
                    || costPerTonne >= reservation
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

        // Spread the year's need over the auctions that are left, so no single auction is bid
        // up against a supply that is only a slice of the year's.
        int sectionsLeft = parameters.AuctionsPerYear - section + 1;
        decimal noise = _random.NextDecimal(-_settings.BidPriceNoise, _settings.BidPriceNoise);
        decimal price = Clamp(
            WillingnessToPay(simulation, parameters, shortfall) * (1m + noise),
            parameters.AuctionFloorPrice,
            parameters.AuctionCeilingPrice);
        decimal wanted = shortfall * _settings.BidVolumeFraction / sectionsLeft;

        // An auction can hold more than one lot: the year's own vintage, and volume offered
        // again after an earlier auction could not sell it. Only lots the compliance year can
        // actually surrender are bid for - a forward lot is next year's vintage - and the
        // position is split across them so the bot does not bid for the same tonnes twice.
        AuctionLot[] usable = [.. auction.Lots.Where(lot => lot.Vintage <= year)];

        if (usable.Length == 0)
        {
            return;
        }

        decimal perLot = wanted / usable.Length;

        foreach (AuctionLot lot in usable)
        {
            PlaceBid(simulation, auction, lot, price, perLot, actions, year, "position");
        }
    }

    /// <summary>Places one bid if it is worth placing and the company can afford it.</summary>
    private void PlaceBid(
        Simulation simulation,
        Auction auction,
        AuctionLot lot,
        decimal price,
        decimal volume,
        List<BotAction> actions,
        int year,
        string what)
    {
        if (volume <= 0m)
        {
            return;
        }

        decimal affordable = simulation.Cash.Available(_company) + _company.OverdraftLimit;
        // Tonnes are whole in this market, and a whole-tonne bid keeps the price times volume,
        // and so the cash set aside for it, exact to the cent.
        decimal bidVolume = Math.Floor(Math.Min(volume, affordable / price));

        if (bidVolume <= 0m)
        {
            return;
        }

        try
        {
            auction.PlaceBid(_units[0], lot.Vintage, price, bidVolume);
            actions.Add(new BotAction(_company.Name, year, BotTrigger.Auction, $"bid {bidVolume} at {price} ({what})"));
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

    /// <summary>
    /// The most this bot will pay for a tonne it has to find, which is what the marginal tonne
    /// of its position costs to cover: walk the abatement it has left cheapest first, and take
    /// the cost of the project that would take it the last of the way. If even all of it is not
    /// enough, the tonne is worth the cash penalty and no more. Bidding this way means the fleet
    /// competes up the marginal abatement cost curve as the cap tightens, rather than sitting at
    /// the floor.
    /// </summary>
    private decimal WillingnessToPay(Simulation simulation, Parameters parameters, decimal shortfall)
    {
        decimal covered = 0m;

        foreach (AbatementOption option in RemainingAbatement(simulation))
        {
            covered += option.AnnualReduction;

            if (covered >= shortfall)
            {
                return option.NetCostPerTonne;
            }
        }

        return parameters.PenaltyPerTonne;
    }

    /// <summary>The abatement this bot could still build, cheapest net cost per tonne first.</summary>
    private IEnumerable<AbatementOption> RemainingAbatement(Simulation simulation)
    {
        return _units
            .SelectMany(unit => unit.AbatementOptions.Where(option => !simulation.Abatements.HasImplemented(unit, option)))
            .OrderBy(option => option.NetCostPerTonne);
    }

    private static decimal Clamp(decimal value, decimal min, decimal max) =>
        value < min ? min : value > max ? max : Math.Round(value, 2, MidpointRounding.AwayFromZero);
}
