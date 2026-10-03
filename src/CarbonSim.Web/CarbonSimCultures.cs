namespace CarbonSim.Web;

/// <summary>
/// The languages the screens can speak. English is complete today; Vietnamese terminology arrives
/// with the Vietnam localisation, and an untranslated key falls back to the neutral resource.
/// </summary>
public static class CarbonSimCultures
{
    public const string Default = "en";

    public const string CookieName = "carbonsim.culture";

    public static IReadOnlyList<string> Supported { get; } = [Default, "vi"];
}
