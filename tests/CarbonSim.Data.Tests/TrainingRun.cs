using CarbonSim.Engine.Allocation;
using CarbonSim.Engine.Bots;
using CarbonSim.Engine.Clock;
using CarbonSim.Engine.Domain;
using CarbonSim.Engine.Market;
using CarbonSim.Engine.Market.Exchange;
using CarbonSim.Engine.Market.Otc;
using CarbonSim.Engine.Scenarios;
using CarbonSim.Engine.Snapshot;

namespace CarbonSim.Data.Tests;

/// <summary>
/// One training run driven the way the web host drives it: a scenario, a clock over injected
/// wall time, the markets, and a fleet of bots. The persistence tests use it so that what they
/// save and reload is a real mid-run state rather than a hand-built one.
/// </summary>
internal sealed class TrainingRun
{
    private readonly BotMarkets _markets;
    private readonly TimeSpan _tick;

    private TrainingRun(
        Simulation simulation,
        SimulationClock clock,
        TestTimeProvider time,
        Exchange exchange,
        OtcMarket otc,
        AuctionSchedule auctions,
        SimulationBots bots,
        TimeSpan tick)
    {
        Simulation = simulation;
        Clock = clock;
        Time = time;
        Exchange = exchange;
        Otc = otc;
        Auctions = auctions;
        Bots = bots;
        _tick = tick;
        _markets = SimulationBots.Markets(simulation, exchange, otc, auctions);
    }

    public Simulation Simulation { get; }

    public SimulationClock Clock { get; }

    public TestTimeProvider Time { get; }

    public Exchange Exchange { get; }

    public OtcMarket Otc { get; }

    public AuctionSchedule Auctions { get; }

    public SimulationBots Bots { get; }

    /// <summary>
    /// Starts a run from the scenario file. Every unit is automated, which is how a published
    /// exercise is set up: the whole fleet trades.
    /// </summary>
    public static TrainingRun Start(
        string scenarioPath,
        BotDifficulty difficulty = BotDifficulty.Normal,
        TimeSpan? tick = null,
        decimal forwardVintageShare = 0.20m)
    {
        Simulation simulation = ScenarioLoader.LoadFile(scenarioPath);

        foreach (Unit unit in simulation.Units)
        {
            unit.AutoTrade = true;
        }

        SimulationBots bots = SimulationBots.Create(simulation, BotSettings.For(difficulty));
        AuctionSchedule auctions = AuctionSchedule.Build(simulation, forwardVintageShare);
        Exchange exchange = new(simulation);
        OtcMarket otc = new(simulation);
        TestTimeProvider time = new();
        SimulationClock clock = new(simulation, time);

        clock.Start();

        return new TrainingRun(
            simulation,
            clock,
            time,
            exchange,
            otc,
            auctions,
            bots,
            tick ?? TimeSpan.FromSeconds(30));
    }

    /// <summary>Plays whole virtual years: allocate, trade, clear, reconcile, close the books.</summary>
    public void PlayYears(int count, decimal offsetShareOfEmissions = 0.03m, decimal lumpyOffsetShare = 0.25m)
    {
        for (int played = 0; played < count; played++)
        {
            PlayYear(offsetShareOfEmissions, lumpyOffsetShare);

            if (played < count - 1)
            {
                Clock.BeginNextYear();
            }
        }
    }

    /// <summary>Plays the year the clock is currently in, up to and including the books closing.</summary>
    public void PlayYear(decimal offsetShareOfEmissions = 0.03m, decimal lumpyOffsetShare = 0.25m)
    {
        Play(
            Simulation,
            Clock,
            Bots,
            _markets,
            Auctions,
            Time,
            _tick,
            offsetShareOfEmissions,
            lumpyOffsetShare);
    }

    /// <summary>
    /// Plays one year on a restored run. It is the same loop as a live run, over the objects the
    /// snapshot restored rather than the ones this instance was built with, because that is
    /// exactly what the host does after a reload.
    /// </summary>
    public static void Play(
        RestoredSimulation restored,
        TestTimeProvider time,
        decimal offsetShareOfEmissions = 0.03m,
        decimal lumpyOffsetShare = 0.25m,
        TimeSpan? tick = null)
    {
        ArgumentNullException.ThrowIfNull(restored);

        Play(
            restored.Simulation,
            restored.Clock,
            restored.Bots,
            SimulationBots.Markets(restored.Simulation, restored.Exchange, restored.Otc, restored.Auctions),
            restored.Auctions,
            time,
            tick ?? TimeSpan.FromSeconds(30),
            offsetShareOfEmissions,
            lumpyOffsetShare);
    }

    /// <summary>
    /// One virtual year: grant the free allocation, hand out the offsets the administrator
    /// would, tick the clock to the year end clearing auctions as they close, then reconcile and
    /// close the books.
    /// </summary>
    private static void Play(
        Simulation simulation,
        SimulationClock clock,
        SimulationBots? bots,
        BotMarkets markets,
        AuctionSchedule auctions,
        TestTimeProvider time,
        TimeSpan tick,
        decimal offsetShareOfEmissions,
        decimal lumpyOffsetShare)
    {
        int year = clock.CurrentYear;
        CompliancePosition position = new(simulation);

        simulation.Ledger.GrantFreeAllocation(year);
        DisburseOffsets(simulation, position, year, offsetShareOfEmissions, lumpyOffsetShare);

        while (clock.State != SimulationState.YearEnded)
        {
            time.Advance(tick);
            IReadOnlyList<ClockEvent> events = clock.Advance();

            foreach (ClockEvent closed in events.Where(item => item.Kind == ClockEventKind.AuctionClosed))
            {
                auctions.ForSection(closed.Year, closed.Auction).Clear();
            }

            bots?.Act(simulation, clock, markets);

            if (clock.State == SimulationState.TradingHalted)
            {
                clock.EndYear();
            }
        }

        simulation.Compliance.Reconcile(year);
        simulation.Finance.CloseYear(year);
    }

    /// <summary>
    /// Stands in for the administrator's Disburse Offsets action: a share of every company's
    /// emissions, plus a lumpy extra to every fifth company so that some have more offsets than
    /// they may surrender and the offset market has sellers.
    /// </summary>
    private static void DisburseOffsets(
        Simulation simulation,
        CompliancePosition position,
        int year,
        decimal share,
        decimal lumpyShare)
    {
        foreach (Company company in simulation.Companies)
        {
            decimal emissions = position.EmissionsFor(company, year);
            decimal volume = emissions * share;

            if (company.Id % 5 == 0)
            {
                volume += emissions * lumpyShare;
            }

            if (volume > 0m)
            {
                simulation.Ledger.Grant(company, Product.Offset, Math.Round(volume, 0, MidpointRounding.AwayFromZero));
            }
        }
    }
}
