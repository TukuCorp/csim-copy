using System.Globalization;
using CarbonSim.Web.Player;
using CarbonSim.Web.Resources;
using Microsoft.Extensions.Localization;

namespace CarbonSim.Web.Components.Shared;

/// <summary>How the screens write numbers, prices and durations; one place so they agree.</summary>
public static class Display
{
    public static string Money(decimal value) => value.ToString("N0", CultureInfo.CurrentCulture);

    public static string Money2(decimal value) => value.ToString("N2", CultureInfo.CurrentCulture);

    public static string Tonnes(decimal value) => value.ToString("N0", CultureInfo.CurrentCulture);

    public static string Number(decimal value) => value.ToString("N2", CultureInfo.CurrentCulture);

    public static string Signed(decimal value) => (value >= 0m ? "+" : string.Empty) + Number(value);

    public static string Price(decimal? value) => value is null ? "-" : value.Value.ToString("N2", CultureInfo.CurrentCulture);

    public static string Percent(decimal share) => (share * 100m).ToString("N1", CultureInfo.CurrentCulture) + "%";

    public static string Percent2(decimal share) => (share * 100m).ToString("N2", CultureInfo.CurrentCulture) + "%";

    public static string Duration(TimeSpan value)
    {
        return value.TotalHours >= 1d
            ? $"{(int)value.TotalHours}h {value.Minutes}m {value.Seconds}s"
            : $"{value.Minutes}m {value.Seconds:D2}s";
    }

    /// <summary>Long when the company holds more than it needs, short when it must buy.</summary>
    public static string Position(decimal shortfall, IStringLocalizer<SharedStrings> strings) =>
        shortfall > 0m ? $"{strings["Short"]} {Tonnes(shortfall)}" : $"{strings["Long"]} {Tonnes(-shortfall)}";

    public static string State(IStringLocalizer<SharedStrings> strings, string state) => strings[$"State_{state}"];

    public static string Product(ProductView product, IStringLocalizer<SharedStrings> strings) =>
        product.IsOffset ? strings["Offset"] : $"{strings["Vintage"]} {product.Vintage}";
}
