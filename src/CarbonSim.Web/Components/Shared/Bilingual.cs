using System.Globalization;
using CarbonSim.Web.Resources;
using Microsoft.Extensions.Localization;

namespace CarbonSim.Web.Components.Shared;

/// <summary>
/// Writes a line in both languages. A run's feed is one feed that every player reads whatever
/// language they chose, so a line a trainer's action puts there has to read in both rather than in
/// whichever culture the server happened to be on when it was built.
/// </summary>
internal static class Bilingual
{
    public static string Both(IStringLocalizer<SharedStrings> strings, string key, params object[] arguments)
    {
        ArgumentNullException.ThrowIfNull(strings);

        CultureInfo previous = CultureInfo.CurrentCulture;

        try
        {
            CultureInfo.CurrentCulture = new CultureInfo(CarbonSimCultures.Default);
            string english = strings[key, arguments];

            CultureInfo.CurrentCulture = new CultureInfo(CarbonSimCultures.Vietnamese);
            string vietnamese = strings[key, arguments];

            return $"{english} | {vietnamese}";
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }
}
