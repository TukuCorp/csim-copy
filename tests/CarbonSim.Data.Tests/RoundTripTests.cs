using CarbonSim.Data.Persistence;
using CarbonSim.Engine.Domain;
using CarbonSim.Engine.Market;
using CarbonSim.Engine.Reporting;
using CarbonSim.Engine.Snapshot;
using FluentAssertions;

namespace CarbonSim.Data.Tests;

/// <summary>
/// A run that goes into the database and comes back out again has to be the same run. The
/// scenario is the shipped one, the year is played by the same driver the host uses, and what
/// is compared afterwards is what a trainer looks at: the leaderboard, the system report, and
/// the state underneath them.
/// </summary>
public sealed class RoundTripTests
{
    private const string Scenario = "scenarios/vietnam-2024.json";

    [Fact]
    public async Task A_seeded_year_survives_being_saved_and_reloaded()
    {
        using SqliteTestDatabase database = new();
        TrainingRun run = TrainingRun.Start(Scenario);
        run.PlayYears(1);

        SimulationSnapshot saved = SimulationSnapshots.Capture(
            run.Simulation,
            run.Clock,
            run.Exchange,
            run.Otc,
            run.Auctions,
            run.Bots);

        SimulationSnapshot loaded = await SaveAndLoad(database, saved);
        RestoredSimulation restored = SimulationSnapshots.Restore(loaded, run.Time);

        LeaderboardDigest(restored.Simulation).Should().Equal(LeaderboardDigest(run.Simulation));
        SystemReport.For(restored.Simulation, 1).Should().BeEquivalentTo(SystemReport.For(run.Simulation, 1));
        LedgerDigest(restored.Simulation).Should().Equal(LedgerDigest(run.Simulation));
        CapitalDigest(restored.Simulation).Should().Equal(CapitalDigest(run.Simulation));
        ComplianceDigest(restored.Simulation).Should().Equal(ComplianceDigest(run.Simulation));

        restored.Clock.State.Should().Be(run.Clock.State);
        restored.Clock.CurrentYear.Should().Be(run.Clock.CurrentYear);
        restored.Clock.ElapsedInYear.Should().Be(run.Clock.ElapsedInYear);
        restored.Simulation.Id.Should().Be(run.Simulation.Id);
        restored.Simulation.Name.Should().Be(run.Simulation.Name);
        restored.Simulation.Units.Should().HaveCount(run.Simulation.Units.Count);
        restored.Simulation.Companies.Should().HaveCount(run.Simulation.Companies.Count);

        // The stream position is part of the state: the next draw after a reload has to be the
        // draw the original would have made, or the two runs drift apart from here on.
        restored.Simulation.Random.NextUInt64().Should().Be(run.Simulation.Random.NextUInt64());
    }

    [Fact]
    public async Task The_whole_state_survives_the_trip_not_just_the_reports()
    {
        using SqliteTestDatabase database = new();
        TrainingRun run = TrainingRun.Start(Scenario);
        run.PlayYears(1);

        SimulationSnapshot saved = SimulationSnapshots.Capture(
            run.Simulation,
            run.Clock,
            run.Exchange,
            run.Otc,
            run.Auctions,
            run.Bots);

        SimulationSnapshot loaded = await SaveAndLoad(database, saved);
        RestoredSimulation restored = SimulationSnapshots.Restore(loaded, run.Time);

        SimulationSnapshot recaptured = SimulationSnapshots.Capture(
            restored.Simulation,
            restored.Clock,
            restored.Exchange,
            restored.Otc,
            restored.Auctions,
            restored.Bots);

        // Capturing what was restored has to produce the snapshot that was saved. Collection
        // order is not compared, so this is about the content of every table.
        recaptured.Should().BeEquivalentTo(saved);
    }

    [Fact]
    public async Task A_restored_run_carries_on_exactly_where_the_original_stopped()
    {
        using SqliteTestDatabase database = new();
        TrainingRun run = TrainingRun.Start(Scenario);
        run.PlayYears(1);

        DateTimeOffset captured = run.Time.GetUtcNow();

        SimulationSnapshot saved = SimulationSnapshots.Capture(
            run.Simulation,
            run.Clock,
            run.Exchange,
            run.Otc,
            run.Auctions,
            run.Bots);

        SimulationSnapshot loaded = await SaveAndLoad(database, saved);

        // The restored clock carries on from the instant the snapshot was taken, so it gets wall
        // time starting there rather than sharing the original's provider, which is about to move.
        TestTimeProvider restoredTime = new(captured);
        RestoredSimulation restored = SimulationSnapshots.Restore(loaded, restoredTime);

        // Both runs play year two from here, each on its own provider, both starting from the
        // same instant and advancing by the same ticks.
        run.Clock.BeginNextYear();
        restored.Clock.BeginNextYear();
        run.PlayYear();
        TrainingRun.Play(restored, restoredTime);

        LeaderboardDigest(restored.Simulation).Should().Equal(LeaderboardDigest(run.Simulation));
        SystemReport.For(restored.Simulation, 2).Should().BeEquivalentTo(SystemReport.For(run.Simulation, 2));
        LedgerDigest(restored.Simulation).Should().Equal(LedgerDigest(run.Simulation));
    }

    [Fact]
    public async Task Saving_a_run_twice_replaces_the_first_save_rather_than_duplicating_it()
    {
        using SqliteTestDatabase database = new();
        TrainingRun run = TrainingRun.Start(Scenario);
        run.PlayYears(1);

        SimulationSnapshot saved = SimulationSnapshots.Capture(
            run.Simulation,
            run.Clock,
            run.Exchange,
            run.Otc,
            run.Auctions,
            run.Bots);

        await using (CarbonSimDbContext context = database.CreateContext())
        {
            EfSimulationRepository repository = new(context);
            await repository.SaveAsync(saved);
        }

        run.Clock.BeginNextYear();
        run.PlayYear();

        SimulationSnapshot later = SimulationSnapshots.Capture(
            run.Simulation,
            run.Clock,
            run.Exchange,
            run.Otc,
            run.Auctions,
            run.Bots);

        await using (CarbonSimDbContext context = database.CreateContext())
        {
            EfSimulationRepository repository = new(context);
            await repository.SaveAsync(later);

            IReadOnlyList<SimulationSummary> summaries = await repository.ListAsync();
            summaries.Should().ContainSingle("a run is one row, however often it is saved");
            summaries[0].CurrentYear.Should().Be(2);
        }

        SimulationSnapshot loaded = await SaveAndLoad(database, later);

        loaded.CurrentYear.Should().Be(2);
        loaded.Journal.Trades.Count.Should().Be(later.Journal.Trades.Count);
        loaded.Cash.Movements.Count.Should().Be(later.Cash.Movements.Count);
    }

    [Fact]
    public async Task Loading_a_run_that_was_never_saved_returns_nothing()
    {
        using SqliteTestDatabase database = new();

        await using CarbonSimDbContext context = database.CreateContext();
        EfSimulationRepository repository = new(context);

        (await repository.LoadAsync(Guid.NewGuid())).Should().BeNull();
        (await repository.ListAsync()).Should().BeEmpty();
        (await repository.DeleteAsync(Guid.NewGuid())).Should().BeFalse();
    }

    private static async Task<SimulationSnapshot> SaveAndLoad(SqliteTestDatabase database, SimulationSnapshot snapshot)
    {
        await using (CarbonSimDbContext context = database.CreateContext())
        {
            EfSimulationRepository repository = new(context);
            await repository.SaveAsync(snapshot);
        }

        await using (CarbonSimDbContext context = database.CreateContext())
        {
            EfSimulationRepository repository = new(context);
            SimulationSnapshot? loaded = await repository.LoadAsync(snapshot.Id);

            loaded.Should().NotBeNull();

            return loaded!;
        }
    }

    /// <summary>
    /// The leaderboard as values rather than text. Decimals are compared as decimals: a value
    /// that survives a database trip keeps its amount but not always its trailing zeros, and
    /// the amount is what a trainer's screen shows and what every later sum uses.
    /// </summary>
    private static List<(int Rank, string Company, decimal Cost, decimal PerTonne, decimal Position, bool Automated)> LeaderboardDigest(
        Simulation simulation)
    {
        return
        [
            .. Leaderboard.Rank(simulation).Select(
                entry => (
                    entry.Rank,
                    entry.Company.Name,
                    entry.OverallCostOfCompliance,
                    entry.OverallMarginalCostOfCompliance,
                    entry.FinalPosition,
                    entry.IsAutomated)),
        ];
    }

    private static List<(string Company, int Vintage, decimal Available, decimal Escrowed)> LedgerDigest(Simulation simulation)
    {
        List<(string, int, decimal, decimal)> lines = [];

        foreach (Company company in simulation.Companies)
        {
            for (int vintage = 1; vintage <= simulation.Allocation.Years.Count; vintage++)
            {
                Product allowance = Product.Allowance(vintage);
                lines.Add((
                    company.Name,
                    vintage,
                    simulation.Ledger.Available(company, allowance),
                    simulation.Ledger.Escrowed(company, allowance)));
            }

            lines.Add((
                company.Name,
                0,
                simulation.Ledger.Available(company, Product.Offset),
                simulation.Ledger.Escrowed(company, Product.Offset)));
        }

        return lines;
    }

    private static List<(string Company, decimal Capital, decimal Escrowed)> CapitalDigest(Simulation simulation)
    {
        return
        [
            .. simulation.Companies.Select(
                company => (company.Name, company.Capital, company.EscrowedCash)),
        ];
    }

    private static List<(int Year, string Company, decimal Obligation, decimal Offsets, decimal Allowances, decimal Banked, decimal Forfeited, decimal Shortfall, decimal Penalty, decimal Debit)> ComplianceDigest(
        Simulation simulation)
    {
        return
        [
            .. simulation.Compliance.Results.Select(
                result => (
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
}
