using System.Globalization;
using CarbonSim.Engine.Market;
using CarbonSim.Engine.Run;
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

    /// <summary>
    /// The virtual calendar date for a point in the year: the year runs January 1 to December 31,
    /// month by month, so each of four sections opens on the first of a quarter (Apr 1, Jul 1, Oct 1),
    /// as the original's calendar does. Written the reader's way round ("April 1", "1 tháng 4").
    /// </summary>
    public static string VirtualDate(TimeSpan elapsed, TimeSpan yearLength)
    {
        double share = YearShare(elapsed, yearLength);
        int month = MonthOf(elapsed, yearLength);
        // Any non-leap year: the calendar is the year's shape, not a real date.
        int day = (int)(((share * 12) - (month - 1)) * DateTime.DaysInMonth(2001, month)) + 1;

        return new DateTime(2001, month, day).ToString(CultureInfo.CurrentCulture.DateTimeFormat.MonthDayPattern, CultureInfo.CurrentCulture);
    }

    /// <summary>The virtual month (1 to 12) a point in the year falls in.</summary>
    public static int MonthOf(TimeSpan elapsed, TimeSpan yearLength) => (int)(YearShare(elapsed, yearLength) * 12) + 1;

    private static double YearShare(TimeSpan elapsed, TimeSpan yearLength) =>
        yearLength > TimeSpan.Zero ? Math.Clamp(elapsed / yearLength, 0d, 0.999999d) : 0d;

    /// <summary>Long when the company holds more than it needs, short when it must buy.</summary>
    public static string Position(decimal shortfall, IStringLocalizer<SharedStrings> strings) =>
        shortfall > 0m ? $"{strings["Short"]} {Tonnes(shortfall)}" : $"{strings["Long"]} {Tonnes(-shortfall)}";

    /// <summary>A shut trading window, named in the reader's language.</summary>
    public static string Refusal(TradeRefusal refusal, IStringLocalizer<SharedStrings> strings) =>
        strings[refusal switch
        {
            TradeRefusal.AuctionClosed => "AuctionIsClosed",
            TradeRefusal.MarketClosed => "MarketIsClosed",
            _ => "TradingIsHalted",
        }];

    /// <summary>
    /// Why the market refused an order: the reader's own words for a shut window, and the market's
    /// message for everything else it turned down.
    /// </summary>
    public static string Refusal(OrderRefusal refusal, IStringLocalizer<SharedStrings> strings)
    {
        ArgumentNullException.ThrowIfNull(refusal);

        return refusal.Window is { } window ? Refusal(window, strings) : refusal.Message;
    }

    /// <summary>
    /// The position a screen reads. Once the year has been settled there is no position left to be
    /// in: the company either covered its obligation or was short when the year closed.
    /// </summary>
    public static string Position(PositionView position, IStringLocalizer<SharedStrings> strings)
    {
        ArgumentNullException.ThrowIfNull(position);

        return position.Settled && position.Shortfall <= 0m
            ? strings["Compliant"]
            : Position(-position.Position, strings);
    }

    public static string State(IStringLocalizer<SharedStrings> strings, string state) => strings[$"State_{state}"];

    public static string Product(ProductView product, IStringLocalizer<SharedStrings> strings) =>
        product.IsOffset ? strings["Offset"] : $"{strings["Vintage"]} {product.Vintage}";

    /// <summary>
    /// How a list names a unit: its own name once, the owner only where it says something the
    /// unit's name does not - a scenario usually names a one-unit company after the company
    /// itself - and a bot marked the way the leaderboard marks it.
    /// </summary>
    public static string Unit(string unit, string? owner, bool automated) =>
        $"{Named(unit, owner)}{Mark(automated)}";

    /// <summary>The same unit, company first, for a picker the trainer reads company by company.</summary>
    public static string CompanyUnit(string company, string unit, bool automated) =>
        string.Equals(company, unit, StringComparison.Ordinal)
            ? $"{company}{Mark(automated)}"
            : $"{company} - {unit}{Mark(automated)}";

    private static string Named(string unit, string? owner) =>
        string.IsNullOrWhiteSpace(owner) || string.Equals(unit, owner, StringComparison.Ordinal)
            ? unit
            : $"{unit} ({owner})";

    private static string Mark(bool automated) => automated ? " (AI)" : string.Empty;

    private sealed class CurrencyScope(CurrencyDisplay? previous) : IDisposable
    {
        public void Dispose() => ScopedCurrency.Value = previous;
    }
}
