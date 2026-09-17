using CarbonSim.Engine.Allocation;
using CarbonSim.Engine.Bots;
using CarbonSim.Engine.Clock;
using CarbonSim.Engine.Domain;
using CarbonSim.Engine.Market;
using CarbonSim.Engine.Market.Exchange;
using CarbonSim.Engine.Market.Otc;
using CarbonSim.Engine.Reporting;
using CarbonSim.Engine.Scenarios;
using CarbonSim.Engine.Snapshot;
using FluentAssertions;

namespace CarbonSim.Engine.Tests.Snapshot;

/// <summary>
/// The snapshot boundary against the shipped scenario: a seeded run is driven the way the
/// fidelity run drives it, captured, and restored into a fresh object graph. Everything a
/// player or an administrator can see has to come back the same — the leaderboard, the reports,
/// the journal, every ledger balance and every company balance, the reconciliations, the
/// abatement books, the clock, the auctions and the order books — and the run has to carry on
/// from there identically, which is what the random stream's position and the bots' own
/// progress are there to guarantee.
/// </summary>
public sealed class SnapshotRoundTripTests
{
    private const int MidYearTicks = 17;

    [Fact]
    public void A_run_captured_at_the_end_of_a_year_restores_into_the_same_run()
    {
        Run original = new();
        original.Start();
        original.BeginYear(1);
        original.ApplyAdministratorActions(1);
        original.TradeOverTheCounter(1);
        original.FinishYear(1);

        Run restored = Restore(original);

        AssertCommonCapture(original);
        original.Auctions.Auctions.Should().Contain(auction => auction.IsCleared, "the year's auctions cleared");
        original.Simulation.Compliance.Results.Should().HaveCount(
            original.Simulation.Companies.Count,
            "year 1 was reconciled for every company");

        AssertSameRun(original, restored, 1);
        AssertSameNextDraws(original, restored);

        original.BeginNextYear();
        restored.BeginNextYear();
        original.BeginYear(2);
        restored.BeginYear(2);
        original.TradeOverTheCounter(2);
        restored.TradeOverTheCounter(2);
        original.FinishYear(2);
        restored.FinishYear(2);

        AssertSameRun(original, restored, 2);
    }

    [Fact]
    public void A_run_captured_mid_year_restores_into_the_same_run()
    {
        Run original = new();
        original.Start();
        original.BeginYear(1);
        original.ApplyAdministratorActions(1);
        original.FinishYear(1);
        original.BeginNextYear();
        original.BeginYear(2);
        original.TradeOverTheCounter(2);

        for (int tick = 0; tick < MidYearTicks; tick++)
        {
            original.Tick();
        }

        original.Clock.State.Should().Be(SimulationState.Running, "the capture point sits inside a running year");
        original.Clock.IsAuctionOpen.Should().BeTrue("the capture point sits inside an auction window");

        Run restored = Restore(original);

        AssertCommonCapture(original);
        RestingOrdersOf(original).Should().NotBeEmpty("bots that have traded are resting orders on the book");
        BidsOf(original).Should().NotBeEmpty("bots bid into the auction that is open");

        AssertSameRun(original, restored, 2);
        AssertSameNextDraws(original, restored);

        original.FinishYear(2);
        restored.FinishYear(2);

        AssertSameRun(original, restored, 2);
    }

    private static Run Restore(Run original)
    {
        SimulationSnapshot snapshot = SimulationSnapshots.Capture(
            original.Simulation,
            original.Clock,
            original.Exchange,
            original.Otc,
            original.Auctions,
            original.Bots);

        return new Run(snapshot, original.WallClock);
    }

    /// <summary>
    /// What the captured run has to contain for the comparison to mean anything. A round trip
    /// over an empty ledger, a silent journal or a bot fleet that never acted would pass without
    /// proving anything, so the state being compared is checked to be real state first.
    /// </summary>
    private static void AssertCommonCapture(Run run)
    {
        run.Simulation.Units.Should().NotBeEmpty();
        run.Simulation.Journal.Trades.Should().NotBeEmpty("the fleet traded");
        run.Simulation.Abatements.All.Should().NotBeEmpty("the bots implemented abatement");
        run.Simulation.Compliance.Fines.Should().ContainSingle("the administrator imposed a fine");
        ShutdownsOf(run).Should().ContainSingle("the administrator shut a unit down for a year");
        LedgerOf(run).Should().Contain(row => row.Available > 0m || row.Escrowed > 0m, "instruments are held");
        BalancesOf(run).Should().OnlyContain(row => row.Capital != 0m);
        run.Otc.Offers.Should().HaveCount(3, "one offer was accepted, one rejected and one left pending");
        run.Exchange.Products.Should().NotBeEmpty("bots have quoted a book");
        run.Simulation.Cash.Movements.Should().NotBeEmpty("money moved");
    }

    private static void AssertSameRun(Run expected, Run actual, int year)
    {
        expected.Simulation.Id.Should().Be(actual.Simulation.Id);
        expected.Simulation.Name.Should().Be(actual.Simulation.Name);
        expected.Simulation.Seed.Should().Be(actual.Simulation.Seed);

        LeaderboardOf(actual).Should().Equal(LeaderboardOf(expected));
        ReportOf(actual, year).Should().Be(ReportOf(expected, year));
        JournalOf(actual).Should().Equal(JournalOf(expected));
        LedgerOf(actual).Should().Equal(LedgerOf(expected));
        BalancesOf(actual).Should().Equal(BalancesOf(expected));
        ComplianceOf(actual).Should().Equal(ComplianceOf(expected));
        FinesOf(actual).Should().Equal(FinesOf(expected));
        AbatementsOf(actual).Should().Equal(AbatementsOf(expected));
        ShutdownsOf(actual).Should().Equal(ShutdownsOf(expected));
        ClockOf(actual).Should().Equal(ClockOf(expected));
        LotsOf(actual).Should().Equal(LotsOf(expected));
        AuctionStateOf(actual).Should().Equal(AuctionStateOf(expected));
        BidsOf(actual).Should().Equal(BidsOf(expected));
        ClearingOf(actual).Should().Equal(ClearingOf(expected));
        AwardsOf(actual).Should().Equal(AwardsOf(expected));
        RejectedOf(actual).Should().Equal(RejectedOf(expected));
        BooksOf(actual).Should().Equal(BooksOf(expected));
        RestingOrdersOf(actual).Should().Equal(RestingOrdersOf(expected));
        BookTradesOf(actual).Should().Equal(BookTradesOf(expected));
        OffersOf(actual).Should().Equal(OffersOf(expected));
        BotsOf(actual).Should().Equal(BotsOf(expected));
    }

    /// <summary>
    /// What each bot is holding: when it acts, what it thinks instruments are worth, and the
    /// progress it has made. The done-set and bid-set are not readable from outside the bot, so
    /// they are covered by the run carrying on identically below; the trigger times and the
    /// expected price are the drawn and learned values, and are compared directly.
    /// </summary>
    private static List<(string Company, decimal AbatementAt, decimal TradeAt, decimal ExpectedPrice)> BotsOf(Run run)
    {
        return
        [
            .. run.Bots!.Bots.Select(bot => (
                bot.Company.Name,
                bot.TriggerTime(BotTrigger.Abatement),
                bot.TriggerTime(BotTrigger.Trade),
                bot.ExpectedAllowancePrice)),
        ];
    }

    /// <summary>
    /// The point of storing the stream position: the first draws after a restore are the draws
    /// the original would have made next. Both runs draw the same values, so they stay in step
    /// for the rest of the run.
    /// </summary>
    private static void AssertSameNextDraws(Run expected, Run actual)
    {
        for (int draw = 0; draw < 5; draw++)
        {
            ulong fromOriginal = expected.Simulation.Random.NextUInt64();
            actual.Simulation.Random.NextUInt64().Should().Be(fromOriginal);
        }

        for (int draw = 0; draw < 3; draw++)
        {
            decimal fromOriginal = expected.Simulation.Random.NextDecimal(0m, 1m);
            actual.Simulation.Random.NextDecimal(0m, 1m).Should().Be(fromOriginal);
        }

        for (int draw = 0; draw < 3; draw++)
        {
            int fromOriginal = expected.Simulation.Random.NextInt(1_000);
            actual.Simulation.Random.NextInt(1_000).Should().Be(fromOriginal);
        }
    }

    private static List<(string Company, int Rank, decimal Cost, decimal PerTonne, decimal Position, bool Automated)> LeaderboardOf(Run run)
    {
        return
        [
            .. Leaderboard.Rank(run.Simulation).Select(entry => (
                entry.Company.Name,
                entry.Rank,
                entry.OverallCostOfCompliance,
                entry.OverallMarginalCostOfCompliance,
                entry.FinalPosition,
                entry.IsAutomated)),
        ];
    }

    private static (int Year, SystemTotals ThisYear, SystemTotals ToDate) ReportOf(Run run, int year)
    {
        SystemReport report = SystemReport.For(run.Simulation, year);

        return (report.Year, report.ThisYear, report.ToDate);
    }

    private static List<(long Sequence, int Year, TradeChannel Channel, string Product, decimal Price, decimal Volume, string Buyer, string? Seller)> JournalOf(Run run)
    {
        return
        [
            .. run.Simulation.Journal.Trades.Select(trade => (
                trade.Sequence,
                trade.Year,
                trade.Channel,
                trade.Product.ToString(),
                trade.Price,
                trade.Volume,
                trade.Buyer.Name,
                trade.Seller?.Name)),
        ];
    }

    private static List<(int Company, string Product, decimal Available, decimal Escrowed)> LedgerOf(Run run)
    {
        List<(int Company, string Product, decimal Available, decimal Escrowed)> rows = [];

        foreach (Company company in run.Simulation.Companies)
        {
            rows.Add((
                company.Id,
                Product.Offset.ToString(),
                run.Simulation.Ledger.Available(company, Product.Offset),
                run.Simulation.Ledger.Escrowed(company, Product.Offset)));

            for (int vintage = 0; vintage <= run.Simulation.Allocation.Years.Count; vintage++)
            {
                Product product = Product.Allowance(vintage);

                rows.Add((
                    company.Id,
                    product.ToString(),
                    run.Simulation.Ledger.Available(company, product),
                    run.Simulation.Ledger.Escrowed(company, product)));
            }
        }

        return rows;
    }

    private static List<(int Company, decimal Capital, decimal EscrowedCash)> BalancesOf(Run run)
    {
        return
        [
            .. run.Simulation.Companies.Select(company => (company.Id, company.Capital, company.EscrowedCash)),
        ];
    }

    private static List<(int Year, string Company, decimal Obligation, decimal Offsets, decimal Allowances, decimal Banked, decimal Forfeited, decimal Shortfall, decimal PenaltyCash, decimal PenaltyAllowanceDebit)> ComplianceOf(Run run)
    {
        return
        [
            .. run.Simulation.Compliance.Results.Select(result => (
                result.Year,
                result.Company.Name,
                result.Obligation,
                result.OffsetsSurrendered,
                result.AllowancesSurrendered,
                result.Banked,
                result.Forfeited,
                result.Shortfall,
                result.PenaltyCash,
                result.PenaltyAllowanceDebit)),
        ];
    }

    private static List<(int Id, string Unit, string Company, decimal Amount, string Description, int Year)> FinesOf(Run run)
    {
        return
        [
            .. run.Simulation.Compliance.Fines.Select(fine => (
                fine.Id,
                fine.Unit.Name,
                fine.Company.Name,
                fine.Amount,
                fine.Description,
                fine.Year)),
        ];
    }

    private static List<(string Unit, string Option, int ImplementedIn, int OperatingFromYear, int ExpiresAfterYear)> AbatementsOf(Run run)
    {
        return
        [
            .. run.Simulation.Abatements.All.Select(project => (
                project.Unit.Name,
                project.Option.Code,
                project.ImplementedIn,
                project.OperatingFromYear,
                project.ExpiresAfterYear)),
        ];
    }

    private static List<(string Unit, int Year)> ShutdownsOf(Run run)
    {
        List<(string Unit, int Year)> rows = [];

        foreach (Unit unit in run.Simulation.Units)
        {
            foreach (int year in run.Simulation.Allocation.Years)
            {
                if (run.Simulation.Abatements.IsShutDown(unit, year))
                {
                    rows.Add((unit.Name, year));
                }
            }
        }

        return rows;
    }

    private static List<(SimulationState State, int Year, TimeSpan Elapsed, int Section, bool AuctionOpen, TimeSpan ToOpen, TimeSpan ToClose, bool Auctionable)> ClockOf(Run run)
    {
        return
        [
            (
                run.Clock.State,
                run.Clock.CurrentYear,
                run.Clock.ElapsedInYear,
                run.Clock.CurrentAuction,
                run.Clock.IsAuctionOpen,
                run.Clock.TimeToAuctionOpen,
                run.Clock.TimeToAuctionClose,
                run.Clock.AuctionsInYear == run.Simulation.TradingSystems.Single().Parameters.AuctionsPerYear),
        ];
    }

    private static List<(int Year, int Sequence, int Vintage, decimal Volume, bool Forward)> LotsOf(Run run)
    {
        return
        [
            .. run.Auctions.Auctions.SelectMany(auction => auction.Lots.Select(lot => (
                auction.Year,
                auction.Sequence,
                lot.Vintage,
                lot.Volume,
                lot.IsForward))),
        ];
    }

    private static List<(int Year, int Sequence, bool Cleared, decimal Unsold)> AuctionStateOf(Run run)
    {
        return
        [
            .. run.Auctions.Auctions.Select(auction => (
                auction.Year,
                auction.Sequence,
                auction.IsCleared,
                auction.UnsoldVolume)),
        ];
    }

    private static List<(int Year, int Sequence, int Id, int Unit, string Company, int Vintage, decimal Price, decimal Volume)> BidsOf(Run run)
    {
        return
        [
            .. run.Auctions.Auctions.SelectMany(auction => auction.Bids.Select(bid => (
                auction.Year,
                auction.Sequence,
                bid.Id,
                bid.Unit.Id,
                bid.Unit.Company.Name,
                bid.Vintage,
                bid.Price,
                bid.Volume))),
        ];
    }

    private static List<(int Year, int Sequence, int Vintage, decimal Offered, decimal? ClearingPrice)> ClearingOf(Run run)
    {
        return
        [
            .. run.Auctions.Auctions.SelectMany(auction => auction.Results.Select(result => (
                auction.Year,
                auction.Sequence,
                result.Vintage,
                result.OfferedVolume,
                result.ClearingPrice))),
        ];
    }

    private static List<(int Year, int Sequence, int Vintage, int BidId, decimal Volume, decimal Cost)> AwardsOf(Run run)
    {
        return
        [
            .. run.Auctions.Auctions.SelectMany(auction => auction.Results.SelectMany(result => result.Awards.Select(award => (
                auction.Year,
                auction.Sequence,
                result.Vintage,
                award.Bid.Id,
                award.Volume,
                award.Cost)))),
        ];
    }

    private static List<(int Year, int Sequence, int Vintage, int BidId)> RejectedOf(Run run)
    {
        return
        [
            .. run.Auctions.Auctions.SelectMany(auction => auction.Results.SelectMany(result => result.Rejected.Select(bid => (
                auction.Year,
                auction.Sequence,
                result.Vintage,
                bid.Id)))),
        ];
    }

    private static List<(string Product, decimal LastTradePrice, bool Traded, decimal? BestBid, decimal? BestOffer, decimal Floor, decimal Ceiling)> BooksOf(Run run)
    {
        return
        [
            .. OrderedProducts(run.Exchange).Select(product =>
            {
                OrderBook book = run.Exchange.Book(product);

                return (
                    product.ToString(),
                    book.LastTradePrice,
                    book.HasTraded,
                    book.BestBid,
                    book.BestOffer,
                    book.BandFloor,
                    book.BandCeiling);
            }),
        ];
    }

    private static List<(string Product, long Id, int Unit, string Company, OrderSide Side, OrderKind Kind, FillPolicy Fill, decimal Volume, decimal? Price, decimal? StopPrice, decimal Filled, string Status, decimal Escrowed)> RestingOrdersOf(Run run)
    {
        return
        [
            .. OrderedProducts(run.Exchange).SelectMany(product => run.Exchange.Book(product).OpenOrders.Select(order => (
                product.ToString(),
                order.Id,
                order.Unit.Id,
                order.Company.Name,
                order.Side,
                order.Kind,
                order.FillPolicy,
                order.Volume,
                order.Price,
                order.StopPrice,
                order.FilledVolume,
                order.Status.ToString(),
                order.EscrowedCash))),
        ];
    }

    private static List<(string Product, long Sequence, long BuyOrderId, long SellOrderId, decimal Price, decimal Volume)> BookTradesOf(Run run)
    {
        return
        [
            .. OrderedProducts(run.Exchange).SelectMany(product => run.Exchange.Book(product).Trades.Select(trade => (
                product.ToString(),
                trade.Sequence,
                trade.BuyOrderId,
                trade.SellOrderId,
                trade.Price,
                trade.Volume))),
        ];
    }

    private static List<(long Id, int Seller, int Buyer, string Product, decimal Price, decimal Volume, string State, int Year)> OffersOf(Run run)
    {
        return
        [
            .. run.Otc.Offers.Select(offer => (
                offer.Id,
                offer.Seller.Id,
                offer.Buyer.Id,
                offer.Product.ToString(),
                offer.Price,
                offer.Volume,
                offer.State.ToString(),
                offer.Year)),
        ];
    }

    private static IEnumerable<Product> OrderedProducts(Exchange exchange)
    {
        return exchange.Products.OrderBy(product => product.Kind).ThenBy(product => product.Vintage);
    }

    /// <summary>
    /// One seeded run of the shipped scenario, driven exactly as the fidelity run drives it: a
    /// tick at a time against the injected clock, the administrator's actions where a test asks
    /// for them, and the year closed with reconciliation and the cash books.
    /// </summary>
    private sealed class Run
    {
        private static readonly TimeSpan Step = TimeSpan.FromSeconds(30);
        private const decimal OtcPrice = 150m;
        private const decimal OtcVolume = 100m;

        private readonly TestTimeProvider _time;
        private readonly BotMarkets _markets;
        private readonly CompliancePosition _position;

        /// <summary>Starts a fresh run of the shipped scenario, with the wall clock where the caller wants it.</summary>
        public Run(TimeSpan wallClockOffset = default)
        {
            _time = new TestTimeProvider();
            _time.Advance(wallClockOffset);
            Simulation = ScenarioLoader.LoadFile(Path.Combine("scenarios", "vietnam-2024.json"));
            Clock = new SimulationClock(Simulation, _time);

            // The published exercises ran the whole fleet this way: every enterprise automated.
            foreach (Unit unit in Simulation.Units)
            {
                unit.AutoTrade = true;
            }

            Bots = SimulationBots.Create(Simulation, BotSettings.For(BotDifficulty.Normal));
            Auctions = AuctionSchedule.Build(Simulation, forwardVintageShare: 0.20m);
            Exchange = new Exchange(Simulation);
            Otc = new OtcMarket(Simulation);
            _markets = new BotMarkets(Exchange, Otc, Auctions);
            _position = new CompliancePosition(Simulation);
        }

        /// <summary>Reloads a captured run, against a wall clock put back where the original's was.</summary>
        public Run(SimulationSnapshot snapshot, TimeSpan wallClockOffset)
        {
            _time = new TestTimeProvider();
            _time.Advance(wallClockOffset);

            RestoredSimulation restored = SimulationSnapshots.Restore(snapshot, _time);

            Simulation = restored.Simulation;
            Clock = restored.Clock;
            Exchange = restored.Exchange;
            Otc = restored.Otc;
            Auctions = restored.Auctions;
            Bots = restored.Bots;
            _markets = new BotMarkets(Exchange, Otc, Auctions);
            _position = new CompliancePosition(Simulation);
            WallClock = wallClockOffset;
        }

        public Simulation Simulation { get; private set; }

        public SimulationClock Clock { get; private set; }

        public Exchange Exchange { get; private set; }

        public OtcMarket Otc { get; private set; }

        public AuctionSchedule Auctions { get; private set; }

        public SimulationBots? Bots { get; private set; }

        /// <summary>How much wall time this run has advanced, so a restored run can line up with it.</summary>
        public TimeSpan WallClock { get; private set; }

        public void Start() => Clock.Start();

        public void BeginNextYear() => Clock.BeginNextYear();

        /// <summary>The administrative acts that open a year: the free allocation and offsets.</summary>
        public void BeginYear(int year)
        {
            Simulation.Ledger.GrantFreeAllocation(year);

            foreach (Company company in Simulation.Companies)
            {
                decimal emissions = _position.EmissionsFor(company, year);
                decimal volume = emissions * 0.03m;

                if (company.Id % 5 == 0)
                {
                    volume += emissions * 0.25m;
                }

                if (volume > 0m)
                {
                    Simulation.Ledger.Grant(company, Product.Offset, Math.Round(volume, 0, MidpointRounding.AwayFromZero));
                }
            }
        }

        /// <summary>Closes the year: every company reconciled, then the cash books closed.</summary>
        public void FinishYear(int year)
        {
            while (Clock.State != SimulationState.YearEnded)
            {
                Tick();
            }

            Simulation.Compliance.Reconcile(year);
            Simulation.Finance.CloseYear(year);
        }

        /// <summary>One tick of the clock and whatever became due with it.</summary>
        public void Tick()
        {
            _time.Advance(Step);
            WallClock += Step;

            foreach (ClockEvent closed in Clock.Advance().Where(@event => @event.Kind == ClockEventKind.AuctionClosed))
            {
                Auctions.ForSection(closed.Year, closed.Auction).Clear();
            }

            if (Bots is { } bots)
            {
                bots.Act(Simulation, Clock, _markets);
            }

            if (Clock.State == SimulationState.TradingHalted)
            {
                Clock.EndYear();
            }
        }

        /// <summary>Stands in for the administrator's abatement and enforcement actions.</summary>
        public void ApplyAdministratorActions(int year)
        {
            Simulation.Abatements.ShutdownForYear(Simulation.Units[0], year);
            Simulation.Compliance.IssueFine(Simulation.Units[1], 10_000m, "Late monitoring report");
        }

        /// <summary>
        /// Stands in for players using the over-the-counter market, which the bots never touch:
        /// one offer left pending, one accepted, one rejected, so the escrowed and settled sides
        /// of the market are both on the table when the snapshot is taken.
        /// </summary>
        public void TradeOverTheCounter(int year)
        {
            Product product = Product.Allowance(year);
            Company? seller = Simulation.Companies.FirstOrDefault(company =>
                Simulation.Ledger.Available(company, product) >= OtcVolume * 3m);
            Company? buyer = Simulation.Companies.FirstOrDefault(company =>
                !ReferenceEquals(company, seller) && Simulation.Cash.CanAfford(company, OtcVolume * OtcPrice));

            if (seller is null || buyer is null)
            {
                return;
            }

            Unit sellerUnit = seller.Units[0];
            Unit buyerUnit = buyer.Units[0];

            Otc.Send(sellerUnit, buyerUnit, product, OtcPrice, OtcVolume);
            Otc.Accept(buyerUnit, Otc.Send(sellerUnit, buyerUnit, product, OtcPrice, OtcVolume).Id);
            Otc.Reject(buyerUnit, Otc.Send(sellerUnit, buyerUnit, product, OtcPrice, OtcVolume).Id);
        }
    }
}
