using CarbonSim.Engine.Scenarios;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CarbonSim.Web.Simulations;

/// <summary>
/// Development only. Loads the configured scenario and puts it on the shelf so a developer
/// opening the host finds an exercise to open and start, without a console of their own to build
/// one with. Beginning the first year is the administrator's control and stays with them: a year
/// that started itself would spend its twenty minutes before anybody looked, and a run whose time
/// is up is a run that has to be closed rather than one to watch. It is registered only when the
/// Development configuration asks for it, and a failure to seed never stops the host: the sign-in
/// page still works with no run on the shelf.
/// </summary>
public sealed class DemoSimulationSeeder : IHostedService
{
    private readonly SimulationRegistry _runs;
    private readonly CarbonSimHostOptions _options;
    private readonly IHostEnvironment _environment;
    private readonly ILogger<DemoSimulationSeeder> _log;

    public DemoSimulationSeeder(
        SimulationRegistry runs,
        CarbonSimHostOptions options,
        IHostEnvironment environment,
        ILogger<DemoSimulationSeeder> log)
    {
        _runs = runs ?? throw new ArgumentNullException(nameof(runs));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _environment = environment ?? throw new ArgumentNullException(nameof(environment));
        _log = log ?? throw new ArgumentNullException(nameof(log));
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        string path = _options.ScenarioPath(_environment);

        try
        {
            Guid id = _runs.Start(ScenarioLoader.LoadFile(path));

            _log.LogInformation(
                "Demo simulation {SimulationId} is on the shelf, waiting for its first year, from {Path}.", id, path);
        }
        catch (Exception exception)
        {
            _log.LogError(exception, "The demo simulation could not be started from {Path}.", path);
        }

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
