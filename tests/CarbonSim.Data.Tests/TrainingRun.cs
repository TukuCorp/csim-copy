using CarbonSim.Engine.Allocation;
using CarbonSim.Engine.Bots;
using CarbonSim.Engine.Clock;
using CarbonSim.Engine.Domain;
using CarbonSim.Engine.Market;
using CarbonSim.Engine.Market.Exchange;
using CarbonSim.Engine.Market.Otc;
using CarbonSim.Engine.Run;
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
    private readonly TimeSpan _tick;

    private TrainingRun(SimulationRun module, TestTimeProvider time, SimulationBots bots, TimeSpan tick)
    {
        Module = module;
        Time = time;
        _tick = tick;
    }

    /// <summary>The run module: the same one the host drives, so a saved state is a played state.</summary>
    public SimulationRun Module { get; }

    public Simulation Simulation => Module.Simulation;

    public SimulationClock Clock => Module.Clock;

    public TestTimeProvider Time { get; }

    public Exchange Exchange => Module.Exchange;

    public OtcMarket Otc => Module.Otc;

    public AuctionSchedule Auctions => Module.Auctions;

    public SimulationBots Bots => Module.Bots ?? throw new InvalidOperationException("A training run always has bots.");

    /// <summary>
    /// Starts a run from the scenario file. Every unit is automated, which is how a published
    /// exercise is set up: the whole fleet trades.
    /// </summary>
    public static TrainingRun Start(
        string scenarioPath,
        BotDifficulty difficulty = BotDifficulty.Normal,
        TimeSpan? tick = null)
    {
        Simulation simulation = ScenarioLoader.LoadFile(scenarioPath);

        foreach (Unit unit in simulation.Units)
        {
            unit.AutoTrade = true;
        }

        SimulationBots bots = SimulationBots.Create(simulation, BotSettings.For(difficulty));
        TestTimeProvider time = new();
        SimulationRun module = SimulationRun.Create(simulation, time, bots);

        // Year 1 begins the way the host begins it: the clock runs and its free allocation is granted.
        module.Start();

        return new TrainingRun(module, time, bots, tick ?? TimeSpan.FromSeconds(30));
    }

    /// <summary>Plays whole virtual years: allocate, trade, clear, reconcile, close the books.</summary>
    public void PlayYears(int count, decimal offsetShareOfEmissions = 0.03m, decimal lumpyOffsetShare = 0.25m)
    {
        for (int played = 0; played < count; played++)
        {
            PlayYear(offsetShareOfEmissions, lumpyOffsetShare);

            if (played < count - 1)
            {
                // The next year begins the way the host begins it: the clock moves on and the year's
                // free allocation is granted once, here, rather than at the top of the play loop.
                Module.BeginNextYear();
            }
        }
    }

    /// <summary>Plays the year the clock is currently in, up to and including the books closing.</summary>
    public void PlayYear(decimal offsetShareOfEmissions = 0.03m, decimal lumpyOffsetShare = 0.25m)
    {
        Play(Module, Time, _tick, offsetShareOfEmissions, lumpyOffsetShare);
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

        // The restored pieces go back into a module, which is exactly what the host does on a reload.
        Play(
            SimulationRun.Restore(
                restored.Simulation,
                restored.Clock,
                restored.Exchange,
                restored.Otc,
                restored.Auctions,
                restored.Bots),
            time,
            tick ?? TimeSpan.FromSeconds(30),
            offsetShareOfEmissions,
            lumpyOffsetShare);
    }

    /// <summary>
    /// One virtual year through the run module: hand out the offsets the administrator would, then
    /// tick to the year end, clearing the auctions that close and closing the year the way the host
    /// closes it. The year's free allocation was granted when the year began, by Start or
    /// BeginNextYear, so this does not grant it a second time.
    /// </summary>
    private static void Play(
        SimulationRun run,
        TestTimeProvider time,
        TimeSpan tick,
        decimal offsetShareOfEmissions,
        decimal lumpyOffsetShare)
    {
        int year = run.Clock.CurrentYear;
        Standing standing = new(run.Simulation);

        DisburseOffsets(run.Simulation, standing, year, offsetShareOfEmissions, lumpyOffsetShare);

        while (run.Clock.State != SimulationState.YearEnded)
        {
            time.Advance(tick);
            run.Advance();

            if (run.Clock.State == SimulationState.TradingHalted)
            {
                run.EndYear();
            }
        }
    }

    /// <summary>
    /// Stands in for the administrator's Disburse Offsets action: a share of every company's
    /// emissions, plus a lumpy extra to every fifth company so that some have more offsets than
    /// they may surrender and the offset market has sellers.
    /// </summary>
    private static void DisburseOffsets(
        Simulation simulation,
        Standing standing,
        int year,
        decimal share,
        decimal lumpyShare)
    {
        foreach (Company company in simulation.Companies)
        {
            decimal emissions = standing.Emissions(company, year);
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
