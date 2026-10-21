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
    /// <summary>
    /// How wide a bot quotes around the price it expects: it bids this much below and offers this
    /// much above, so the book has two sides to it instead of a queue of orders all at one price.
    /// </summary>
    private const decimal QuoteSpread = 0.04m;

    private readonly Company _company;
    private readonly List<Unit> _units;
    private readonly BotSettings _settings;
    private readonly SimulationRandom _random;
    private readonly Dictionary<BotTrigger, decimal> _triggerAt;
    private readonly HashSet<(int Year, BotTrigger Trigger)> _done = [];
    private readonly HashSet<(int Year, int Section)> _bidIn = [];
    private decimal _expectedPrice;
    private int _seenRevision;

    /// <summary>
    /// This bot's own rung on the ladder, spread across the band by company so two hundred bots do
    /// not all quote the same two prices into a market that is meant to be read as a book.
    /// </summary>
    private decimal QuoteRung => (((_company.Id * 7) % 11) - 5) / 100m;

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
                // Just after the year's first auction has cleared (its window opens with the year)
                // and still inside the first quarter: the bot invests and quotes on the price the
                // year has just set, and a room watching the exchange early in the year sees a market.
                [BotTrigger.Trade] = random.NextDecimal(0.12m, 0.20m),
                // And a second pass late in the year, when a bot stops quoting and starts dealing.
                [BotTrigger.TradeAgain] = random.NextDecimal(0.60m, 0.80m),
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

        // A change to the rules is news every bot has to act on: one that hears it re-opens the
        // passes it has already used this year and takes the new collar into its price on the spot,
        // so a floor or ceiling moved by the regulator reaches the book inside the same month.
        if (simulation.RulesRevision != _seenRevision)
        {
            _seenRevision = simulation.RulesRevision;
            _done.Remove((year, BotTrigger.Trade));
            _done.Remove((year, BotTrigger.TradeAgain));

            Parameters shocked = simulation.Rules;

            _expectedPrice = Math.Clamp(_expectedPrice, shocked.AuctionFloorPrice, shocked.AuctionCeilingPrice);
        }

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
            Quote(simulation, year, markets, actions);
        }

        if (Due(year, BotTrigger.TradeAgain, elapsed))
        {
            Settle(simulation, year, markets, actions);
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

        // The last clearing of any kind, not a blend: an expected price pulled towards the day's
        // auction clearing makes every bot hold out for a higher price, and a fleet that holds out
        // ends the year short. The late pass is where the two venues are brought together, because
        // that is where a bot that still needs tonnes pays the price it can see.
        if (lastAuction is not null)
        {
            _expectedPrice = lastAuction.Price;
        }
        else if (lastTrade is not null)
        {
            _expectedPrice = lastTrade.Price;
        }

        // The collar is the regulator's word on where a tonne may trade, so it bounds the estimate.
        Parameters parameters = simulation.Rules;

        _expectedPrice = Math.Clamp(_expectedPrice, parameters.AuctionFloorPrice, parameters.AuctionCeilingPrice);
    }

    private void Abate(Simulation simulation, int year, List<BotAction> actions)
    {
        decimal reservation = _expectedPrice * _settings.AbatementMargin;
        int remainingYears = simulation.Allocation.Years.Count - year + 1;
        decimal shareOfYear = Math.Clamp((decimal)(simulation.YearOffset / simulation.Rules.YearLength), 0m, 1m);

        foreach (Unit unit in _units)
        {
            foreach (AbatementOption option in unit.AbatementOptions.OrderBy(option => option.NetCostPerTonne))
            {
                // What a tonne costs the company by this route: the project's own cost per tonne
                // over its service life, which is the figure the scenario prices the menu with.
                // Amortising it over only the years left in the run instead is what stopped every
                // bot from ever touching the mid-priced half of its menu: a project that was worth
                // building at 180 a tonne looked like 900 when two years were left to play, so the
                // fleet paid 240 at auction and accepted penalties rather than abate.
                decimal costPerTonne = option.NetCostPerTonne;

                // A project that only starts operating after the last year never cuts a tonne
                // (FAQ #4): the build runs from now, so it has to finish inside the years left.
                if (simulation.Abatements.HasImplemented(unit, option)
                    || costPerTonne >= reservation
                    || shareOfYear + option.ImplementationYears >= remainingYears)
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
        Parameters parameters = simulation.Rules;
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

        // An auction can hold more than one lot: the year's own vintage, volume offered again after
        // an earlier auction could not sell it, and forward vintages. The position is split across
        // the lots the compliance year can actually surrender, so a bot never bids for the same
        // tonnes twice.
        AuctionLot[] current = [.. auction.Lots.Where(lot => lot.Vintage <= year)];

        foreach (AuctionLot lot in current)
        {
            PlaceBid(simulation, auction, lot, price, wanted / current.Length, actions, year, "position");
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

    /// <summary>
    /// Puts the bot's quotes on the exchange: it bids below the price it expects and offers above
    /// it, so the book carries both sides, and it offers any offset it cannot use at what an
    /// offset is worth to it. Nothing here has to trade today: the late pass re-quotes.
    /// </summary>
    private void Quote(Simulation simulation, int year, BotMarkets markets, List<BotAction> actions)
    {
        Parameters parameters = simulation.Rules;
        decimal shortfall = Shortfall(simulation, year);
        decimal offsetValue = _expectedPrice * (1m - _settings.OffsetDiscount);
        decimal pricingOffsetEstimate = simulation.Allocation.BausEmissionsFor(_company, year) * parameters.OffsetUsageLimit;
        decimal offsetsHeld = simulation.Ledger.Held(_company, Product.Offset);
        decimal bid = _expectedPrice * _settings.ReservationPriceFactor * (1m - QuoteSpread + QuoteRung);
        decimal ask = _expectedPrice * _settings.ReservationPriceFactor * (1m + QuoteSpread + QuoteRung);

        // A quarter of any unusable offset is offered now and the rest later in the year, so the
        // surplus dribbles out across the year rather than all of it landing at one price. The
        // rest is not offered twice: an offer escrows the offsets it names, so the later pass can
        // only reach what is still available.
        if (offsetsHeld > pricingOffsetEstimate)
        {
            Sell(
                markets,
                Product.Offset,
                (offsetsHeld - pricingOffsetEstimate) / 4m,
                offsetValue,
                actions,
                year,
                "surplus offsets");
        }

        if (shortfall <= 0m)
        {
            // Long: offer what cannot be banked.
            decimal held = HeldAllowances(simulation, year);
            decimal bankable = Math.Round(simulation.Allocation.BausEmissionsFor(_company, year) * parameters.BankingLimit, 0, MidpointRounding.AwayFromZero);
            decimal surplus = held - bankable;

            if (surplus > 0m)
            {
                Sell(markets, Product.Allowance(year), surplus, ask, actions, year, "surplus allowances");
            }

            return;
        }

        decimal offsetsWanted = Math.Min(shortfall, pricingOffsetEstimate - offsetsHeld);

        if (offsetsWanted > 0m)
        {
            Buy(markets, Product.Offset, offsetsWanted, offsetValue, actions, year, "offsets");
        }

        Buy(markets, Product.Allowance(year), shortfall - offsetsWanted, bid, actions, year, "allowances");
    }

    /// <summary>
    /// Late in the year the bot stops waiting for its own price: it takes its resting quotes off
    /// the book and re-quotes at prices that trade - a short bot up to what the tonne is worth to
    /// it, a long one at the best bid it can see. This is what turns a book of two-sided quotes
    /// into trades; without it every buyer waits for every seller and nothing ever changes hands.
    /// </summary>
    private void Settle(Simulation simulation, int year, BotMarkets markets, List<BotAction> actions)
    {
        Parameters parameters = simulation.Rules;
        Product allowance = Product.Allowance(year);
        decimal shortfall = Shortfall(simulation, year);
        decimal offsetsHeld = simulation.Ledger.Held(_company, Product.Offset);
        decimal pricingOffsetEstimate = simulation.Allocation.BausEmissionsFor(_company, year) * parameters.OffsetUsageLimit;

        if (shortfall <= 0m)
        {
            // Long: the tonnes it cannot bank are worth nothing after the year end, so where a buyer
            // has shown a bid it meets it. Where none has, the offer it made early in the year stays
            // exactly where it is: pulling it would leave the market with nothing to buy at all.
            decimal bankable = Math.Round(
                simulation.Allocation.BausEmissionsFor(_company, year) * parameters.BankingLimit,
                0,
                MidpointRounding.AwayFromZero);
            decimal surplus = HeldAllowances(simulation, year) - bankable;
            OrderBook book = markets.Exchange.Book(allowance);

            if (surplus > 0m && book.BestBid is { } bestBid)
            {
                CancelQuotes(markets, allowance, actions, year);
                Sell(markets, allowance, surplus, bestBid, actions, year, "what it cannot bank");
            }

            // Surplus offsets are worth more sold than kept, because only a share of them can be
            // used, and they go at the discount the bot itself puts on an offset - the price
            // another company with room under its own limit is willing to pay.
            if (offsetsHeld > pricingOffsetEstimate)
            {
                Sell(
                    markets,
                    Product.Offset,
                    offsetsHeld - pricingOffsetEstimate,
                    _expectedPrice * (1m - _settings.OffsetDiscount),
                    actions,
                    year,
                    "surplus offsets");
            }

            return;
        }

        // Short: the quote it made early in the year was deliberately below the market, so it comes
        // off the book and the bot pays up for what it still needs, up to what a tonne is worth to
        // the last of its position and never above the collar.
        CancelQuotes(markets, allowance, actions, year);

        // The offset bid from the early pass comes off too, or the one below would sit beside it and
        // the bot would end up bidding for twice the offsets it can surrender.
        CancelQuotes(markets, Product.Offset, actions, year, OrderSide.Buy);

        decimal offsetsWanted = Math.Min(shortfall, Math.Max(0m, pricingOffsetEstimate - offsetsHeld));

        if (offsetsWanted > 0m)
        {
            Buy(markets, Product.Offset, offsetsWanted, _expectedPrice * (1m - _settings.OffsetDiscount), actions, year, "offsets");
        }

        decimal limit = Math.Min(WillingnessToPay(simulation, parameters, shortfall), parameters.AuctionCeilingPrice);

        Buy(markets, allowance, shortfall - offsetsWanted, limit, actions, year, "the rest of its position");
    }

    /// <summary>Takes this company's own resting orders for a product off the book, one side or both.</summary>
    private void CancelQuotes(BotMarkets markets, Product product, List<BotAction> actions, int year, OrderSide? side = null)
    {
        foreach (Order order in markets.Exchange.Book(product).OpenOrders
            .Where(order => ReferenceEquals(order.Company, _company) && (side is null || order.Side == side))
            .ToList())
        {
            try
            {
                markets.Exchange.Cancel(_company, order.Id);
            }
            catch (InvalidOperationException exception)
            {
                actions.Add(new BotAction(_company.Name, year, BotTrigger.TradeAgain, $"could not pull order {order.Id}: {exception.Message}"));
            }
        }
    }

    private void Buy(BotMarkets markets, Product product, decimal volume, decimal limitPrice, List<BotAction> actions, int year, string what)
    {
        OrderBook book = markets.Exchange.Book(product);
        decimal price = book.FitToBand(limitPrice);
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
        catch (ArgumentException exception)
        {
            // FitToBand already lands the price on a whole cent inside the band, so a refusal here
            // is not the rounding slip past an edge that used to sit under this catch; the book
            // turned the order down for some other reason. The bot notes it and moves on.
            actions.Add(new BotAction(_company.Name, year, BotTrigger.Trade, $"could not buy {what}: {exception.Message}"));
        }
    }

    private void Sell(BotMarkets markets, Product product, decimal volume, decimal limitPrice, List<BotAction> actions, int year, string what)
    {
        OrderBook book = markets.Exchange.Book(product);
        decimal price = book.FitToBand(limitPrice);
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
        catch (ArgumentException exception)
        {
            actions.Add(new BotAction(_company.Name, year, BotTrigger.Trade, $"could not sell {what}: {exception.Message}"));
        }
    }

    /// <summary>What the company still has to find this year, after what it already holds.</summary>
    public decimal Shortfall(Simulation simulation, int year)
    {
        return new Standing(simulation).Shortfall(_company, year);
    }

    private decimal HeldAllowances(Simulation simulation, int year)
    {
        return new Standing(simulation).HeldAllowances(_company, year);
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
