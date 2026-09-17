using CarbonSim.Engine.Domain;
using CarbonSim.Engine.Scenarios;

namespace CarbonSim.Web.Accounts;

/// <summary>
/// The companies a scenario holds for human players, read once per host from the configured
/// scenario file. Loading is deferred to the first request that needs it, so a host whose
/// registration is never opened does not pay for parsing the scenario, and a broken file fails
/// that request with the loader's own list of problems instead of the whole host.
/// </summary>
public sealed class ScenarioCompanyCatalog
{
    private readonly Lazy<IReadOnlyList<string>> _humanCompanies;

    public ScenarioCompanyCatalog(IHostEnvironment environment, CarbonSimHostOptions options)
    {
        ArgumentNullException.ThrowIfNull(environment);
        ArgumentNullException.ThrowIfNull(options);

        if (string.IsNullOrWhiteSpace(options.ScenarioFile))
        {
            throw new InvalidOperationException(
                $"'{CarbonSimHostOptions.SectionName}:ScenarioFile' must name a scenario file.");
        }

        string path = Path.IsPathRooted(options.ScenarioFile)
            ? options.ScenarioFile
            : Path.Combine(environment.ContentRootPath, options.ScenarioFile);

        _humanCompanies = new Lazy<IReadOnlyList<string>>(
            () => ReadHumanCompanies(path),
            LazyThreadSafetyMode.ExecutionAndPublication);
    }

    /// <summary>The companies a human player owns, in the order the scenario lists them.</summary>
    public IReadOnlyList<string> HumanCompanies => _humanCompanies.Value;

    private static IReadOnlyList<string> ReadHumanCompanies(string path)
    {
        return
        [
            .. ScenarioLoader
                .LoadFile(path)
                .Companies
                .Where(company => company.Owner.Kind == PlayerKind.Human)
                .Select(company => company.Name),
        ];
    }
}
