using System.Globalization;

namespace CarbonSim.Web.Tests;

/// <summary>
/// Runs a test under a named culture and puts the ambient one back afterwards, so a screen that
/// formats numbers for Vietnam does not leak that culture into a test running beside it.
/// </summary>
internal sealed class CultureScope : IDisposable
{
    private readonly CultureInfo _culture;
    private readonly CultureInfo _uiCulture;

    public CultureScope(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        _culture = CultureInfo.CurrentCulture;
        _uiCulture = CultureInfo.CurrentUICulture;

        CultureInfo culture = new(name);
        CultureInfo.CurrentCulture = culture;
        CultureInfo.CurrentUICulture = culture;
    }

    public void Dispose()
    {
        CultureInfo.CurrentCulture = _culture;
        CultureInfo.CurrentUICulture = _uiCulture;
    }
}
