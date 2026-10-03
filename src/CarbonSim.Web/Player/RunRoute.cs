namespace CarbonSim.Web.Player;

/// <summary>
/// Reads the run a page belongs to out of its address, <c>/run/{id}/screen</c>. The layout needs
/// the same answer the page's route parameter carries, and a layout cannot take a route parameter,
/// so the address is the one place both agree on.
/// </summary>
public static class RunRoute
{
    public const string Prefix = "run";

    public static Guid? SimulationId(string? uri)
    {
        if (string.IsNullOrWhiteSpace(uri) || !Uri.TryCreate(uri, UriKind.RelativeOrAbsolute, out Uri? parsed))
        {
            return null;
        }

        string path = parsed.IsAbsoluteUri ? parsed.AbsolutePath : uri;
        string[] parts = path.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);

        return parts.Length >= 2
            && string.Equals(parts[0], Prefix, StringComparison.OrdinalIgnoreCase)
            && Guid.TryParse(parts[1], out Guid simulationId)
                ? simulationId
                : null;
    }

    public static string Screen(Guid simulationId, string screen) => $"/{Prefix}/{simulationId:D}/{screen}";
}
