using CarbonSim.Engine.Domain;
using CarbonSim.Web;
using CarbonSim.Web.Simulations;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;

namespace CarbonSim.Web.Tests.Simulations;

/// <summary>
/// The development demo run, which a host seeds when StartDemoSimulation is on. It puts the
/// configured scenario on the shelf and stops there: beginning year one is the administrator's
/// control, so a run nobody has opened yet still has its whole year ahead of it.
/// </summary>
public sealed class DemoSeederTests : IClassFixture<SimulationTestHost>
{
    private const string SeededRun = "Vietnam ETS Pilot 2024";

    private readonly SimulationTestHost _host;

    public DemoSeederTests(SimulationTestHost host)
    {
        _host = host;
    }

    [Fact]
    public async Task The_demo_run_is_seeded_waiting_for_its_first_year()
    {
        SimulationRegistry runs = _host.Server.Services.GetRequiredService<SimulationRegistry>();
        DemoSimulationSeeder seeder = new(
            runs,
            new CarbonSimHostOptions { ScenarioFile = ScenarioPath() },
            _host.Server.Services.GetRequiredService<IHostEnvironment>(),
            NullLogger<DemoSimulationSeeder>.Instance);

        await seeder.StartAsync(CancellationToken.None);

        // Seeding starts the run again from the scenario, so the run under test is the one this
        // call put on the shelf, whatever was there before it.
        HostedRun run = runs.Ids
            .Select(runs.Run)
            .Single(candidate => string.Equals(candidate.Simulation.Name, SeededRun, StringComparison.Ordinal));

        run.Simulation.State.Should().Be(SimulationState.Pending, "the trainer begins the year, not the boot");
        run.Simulation.CurrentYear.Should().Be(0);
        run.Clock.ElapsedInYear.Should().Be(TimeSpan.Zero);
        run.Clock.TimeLeftInYear.Should().Be(run.Clock.YearLength);
        run.Clock.CurrentAuction.Should().Be(1);
    }

    private static string ScenarioPath() => Path.Combine(RepositoryRoot(), "scenarios", "vietnam-2024.json");

    /// <summary>Walks up from the test's own output folder to the folder holding the solution.</summary>
    private static string RepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "CarbonSim.sln")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName
            ?? throw new InvalidOperationException("The repository root could not be found from the test output folder.");
    }
}
