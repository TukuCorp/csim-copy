using CarbonSim.Engine.Scenarios;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CarbonSim.Web.Simulations;

/// <summary>
/// Development only. Loads the configured scenario and starts it so a developer opening the host
/// finds a live game to click through, without an administrator console to drive one. It is
/// registered only when the Development configuration asks for it, and a failure to seed never
/// stops the host: the sign-in page still works with no run on the shelf.
/// </summary>
public sealed class DemoSimulationSeeder : IHostedService
{
    private readonly SimulationRegistry _runs;
    private readonly SimulationClockService _driver;
    private readonly CarbonSimHostOptions _options;
    private readonly IHostEnvironment _environment;
    private readonly ILogger<DemoSimulationSeeder> _log;

    public DemoSimulationSeeder(
        SimulationRegistry runs,
        SimulationClockService driver,
        CarbonSimHostOptions options,
        IHostEnvironment environment,
        ILogger<DemoSimulationSeeder> log)
    {
        _runs = runs ?? throw new ArgumentNullException(nameof(runs));
        _driver = driver ?? throw new ArgumentNullException(nameof(driver));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _environment = environment ?? throw new ArgumentNullException(nameof(environment));
        _log = log ?? throw new ArgumentNullException(nameof(log));
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        string path = Path.IsPathRooted(_options.ScenarioFile)
            ? _options.ScenarioFile
            : Path.Combine(_environment.ContentRootPath, _options.ScenarioFile);

        try
        {
            Guid id = _runs.Start(ScenarioLoader.LoadFile(path));
            await _driver.StartAsync(id, cancellationToken).ConfigureAwait(false);

            _log.LogInformation("Demo simulation {SimulationId} started from {Path}.", id, path);
        }
        catch (Exception exception)
        {
            _log.LogError(exception, "The demo simulation could not be started from {Path}.", path);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
