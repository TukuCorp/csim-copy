using System.Globalization;
using CarbonSim.Web.Player;
using CarbonSim.Web.Resources;
using Microsoft.Extensions.Localization;

namespace CarbonSim.Web.Components.Shared;

/// <summary>How the screens write numbers, prices and durations; one place so they agree.</summary>
public static class Display
{
    private static CurrencyDisplay HostCurrency = CurrencyDisplay.UsdDefault;
    private static readonly AsyncLocal<CurrencyDisplay?> ScopedCurrency = new();

    /// <summary>
    /// The currency the screens show. The host sets it once from configuration; it is ambient the
    /// way <see cref="CultureInfo.CurrentCulture"/> is, because every money figure on every screen
    /// has to agree and the alternative is threading a currency through each one.
    /// </summary>
    public static CurrencyDisplay Currency
    {
        get => ScopedCurrency.Value ?? HostCurrency;
        set => HostCurrency = value ?? throw new ArgumentNullException(nameof(value));
    }

    /// <summary>
    /// Shows a different currency for the current async flow only, until the returned scope is
    /// disposed, so a test can render a dong screen without changing what parallel tests see.
    /// </summary>
    public static IDisposable UseCurrency(CurrencyDisplay currency)
    {
        ArgumentNullException.ThrowIfNull(currency);

        CurrencyDisplay? previous = ScopedCurrency.Value;
        ScopedCurrency.Value = currency;

        return new CurrencyScope(previous);
    }

    public static string Money(decimal value) => Currency.Money(value);

    public static string Money2(decimal value) => Currency.Money2(value);

    public static string Tonnes(decimal value) => value.ToString("N0", CultureInfo.CurrentCulture);

    public static string Number(decimal value) => value.ToString("N2", CultureInfo.CurrentCulture);

    public static string Signed(decimal value) => (value >= 0m ? "+" : string.Empty) + Number(value);

    public static string Price(decimal? value) => value is null ? "-" : Currency.Price(value.Value);

    /// <summary>A price in the engine's unit, as the number a price box shows.</summary>
    public static decimal PriceInput(decimal internalPrice) => Currency.ToDisplay(internalPrice);

    /// <summary>A number typed into a price box, back in the engine's unit.</summary>
    public static decimal PriceFromInput(decimal typed) => Currency.ToInternal(typed);

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

    private sealed class CurrencyScope(CurrencyDisplay? previous) : IDisposable
    {
        public void Dispose() => ScopedCurrency.Value = previous;
    }
}
