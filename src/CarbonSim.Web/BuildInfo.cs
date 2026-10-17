using System.Reflection;

namespace CarbonSim.Web;

/// <summary>
/// Which build is serving the page: the assembly version and the commit it was built from, which the
/// SDK writes into the informational version as "1.0.0+&lt;sha&gt;". Shortened to seven characters of
/// the commit, it is what a tester copies into a report so the report can be tied to the code.
/// </summary>
public static class BuildInfo
{
    public static string Version { get; } = Read();

    private static string Read()
    {
        string version = typeof(BuildInfo).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion ?? "unknown";

        int plus = version.IndexOf('+', StringComparison.Ordinal);

        return plus >= 0 && version.Length > plus + 8 ? version[..(plus + 8)] : version;
    }
}
