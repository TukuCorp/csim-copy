using CarbonSim.Data.Persistence;
using CarbonSim.Engine.Reporting;
using CarbonSim.Engine.Snapshot;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace CarbonSim.Data.Tests;

/// <summary>
/// The restore drill a trainer follows after a crash: back the live SQLite file up while the host
/// is still running, then open the copy on its own and load the saved run back out of it. The
/// backup is taken with <c>VACUUM INTO</c>, which is SQLite's online-safe copy - copying the file
/// with the file system can catch a write in progress, this cannot.
/// </summary>
public sealed class SqliteBackupRestoreTests
{
    private const string Scenario = "scenarios/vietnam-2024.json";

    [Fact]
    public async Task A_live_database_backs_up_and_the_copy_reloads_the_run()
    {
        string directory = Path.Combine(Path.GetTempPath(), "carbonsim-backup", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        string live = Path.Combine(directory, "carbonsim.sqlite");
        string backup = Path.Combine(directory, "carbonsim-backup.sqlite");
        string connectionString = $"Data Source={live}";
        DbContextOptions<CarbonSimDbContext> options = new DbContextOptionsBuilder<CarbonSimDbContext>()
            .UseSqlite(connectionString)
            .Options;

        try
        {
            using (CarbonSimDbContext schema = new(options))
            {
                await schema.Database.MigrateAsync();
            }

            TrainingRun run = TrainingRun.Start(Scenario);
            run.PlayYears(1);
            SimulationSnapshot saved = SimulationSnapshots.Capture(
                run.Simulation,
                run.Clock,
                run.Exchange,
                run.Otc,
                run.Auctions,
                run.Bots);

            using (CarbonSimDbContext context = new(options))
            {
                await new EfSimulationRepository(context).SaveAsync(saved);
            }

            // The online-safe copy: consistent even while another connection is writing.
            await using (SqliteConnection source = new(connectionString))
            {
                await source.OpenAsync();
                await using SqliteCommand command = source.CreateCommand();
                command.CommandText = "VACUUM INTO $target";
                command.Parameters.AddWithValue("$target", backup);
                await command.ExecuteNonQueryAsync();
            }

            File.Exists(backup).Should().BeTrue("the drill must leave a usable copy of the database");

            // Open the copy on its own, the way a host pointed at the restored file would.
            DbContextOptions<CarbonSimDbContext> restoredOptions = new DbContextOptionsBuilder<CarbonSimDbContext>()
                .UseSqlite($"Data Source={backup}")
                .Options;

            using CarbonSimDbContext restoredContext = new(restoredOptions);
            SimulationSnapshot? reloaded = await new EfSimulationRepository(restoredContext).LoadAsync(saved.Id);

            reloaded.Should().NotBeNull("the saved run must be in the backup");
            RestoredSimulation restored = SimulationSnapshots.Restore(reloaded!, run.Time);

            LeaderboardDigest(restored.Simulation).Should().Equal(LeaderboardDigest(run.Simulation));
            restored.Clock.State.Should().Be(run.Clock.State);
            restored.Clock.CurrentYear.Should().Be(run.Clock.CurrentYear);
            restored.Simulation.Units.Should().HaveCount(run.Simulation.Units.Count);
        }
        finally
        {
            // SQLite pools keep the file handles open on Windows, so they are cleared before the
            // directory is removed.
            SqliteConnection.ClearAllPools();

            try
            {
                Directory.Delete(directory, recursive: true);
            }
            catch (IOException)
            {
                // A handle still held is a tidiness problem, not a test failure.
            }
        }
    }

    private static (string Company, decimal Cost, decimal MarginalCost, decimal Position)[] LeaderboardDigest(
        CarbonSim.Engine.Domain.Simulation simulation) =>
    [
        .. Leaderboard.Rank(simulation).Select(entry => (
            entry.Company.Name,
            entry.OverallCostOfCompliance,
            entry.OverallMarginalCostOfCompliance,
            entry.FinalPosition)),
    ];
}
