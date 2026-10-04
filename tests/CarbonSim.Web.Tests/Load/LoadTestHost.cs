using CarbonSim.Web;
using CarbonSim.Web.Simulations;
using CarbonSim.Web.Tests.Simulations;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CarbonSim.Web.Tests.Load;

/// <summary>
/// The host the load harness boots: the real one, against a throwaway database, playing the load
/// scenario generated for the run. Its clock and driver are the production ones unless the harness
/// asks to hand-drive them, which is what the fast smoke test does so it never sleeps.
/// </summary>
public sealed class LoadTestHost : CarbonSimWebHost
{
    private readonly string _scenarioPath;

    public LoadTestHost(string scenarioJson, bool handDriven)
    {
        ArgumentNullException.ThrowIfNull(scenarioJson);

        string directory = Path.Combine(Path.GetTempPath(), "carbonsim-load");
        Directory.CreateDirectory(directory);
        _scenarioPath = Path.Combine(directory, $"{Guid.NewGuid():N}.json");
        File.WriteAllText(_scenarioPath, scenarioJson);

        HandDriven = handDriven;
        FakeTime = handDriven ? new FakeTimeProvider() : null;
    }

    /// <summary>True when the test advances the clock itself instead of the hosted one-second loop.</summary>
    public bool HandDriven { get; }

    /// <summary>The hand-driven clock, or null when the host uses real time.</summary>
    public FakeTimeProvider? FakeTime { get; }

    public override string? ScenarioFile => _scenarioPath;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        base.ConfigureWebHost(builder);

        // The load scenario is started by the harness, not seeded at boot, so the demo run is
        // switched off whatever environment the test host is running in.
        builder.UseSetting($"{CarbonSimHostOptions.SectionName}:StartDemoSimulation", "false");

        if (!HandDriven)
        {
            return;
        }

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(FakeTime!);
            services.RemoveAll<SimulationDriverOptions>();
            services.AddSingleton(new SimulationDriverOptions { PollInterval = TimeSpan.Zero });
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (!disposing)
        {
            return;
        }

        try
        {
            File.Delete(_scenarioPath);
        }
        catch (IOException)
        {
            // A file still held open is a tidiness problem, not a test failure.
        }
    }
}
