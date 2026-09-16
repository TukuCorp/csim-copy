namespace CarbonSim.Engine.Scenarios;

/// <summary>
/// Thrown when a scenario file cannot be turned into a simulation. Carries every problem it
/// found, so an administrator can fix a file in one pass instead of one error at a time.
/// </summary>
public sealed class ScenarioLoadException : Exception
{
    public ScenarioLoadException(string sourceName, IReadOnlyList<string> errors)
        : base($"{sourceName} could not be loaded:{Environment.NewLine} - {string.Join($"{Environment.NewLine} - ", errors)}")
    {
        SourceName = sourceName;
        Errors = errors;
    }

    /// <summary>Name of the file the problems came from, without its directory.</summary>
    public string SourceName { get; }

    public IReadOnlyList<string> Errors { get; }
}
