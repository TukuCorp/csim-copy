using System.Globalization;

namespace CarbonSim.Web;

/// <summary>
/// How the screens write money. The engine keeps every amount in one internal unit, which for the
/// Vietnam exercise is the US dollar the trainer deck quotes the collar and penalty in; the host
/// chooses whether the screens show that unit or convert it to Vietnamese dong at the configured
/// rate. The conversion lives here, so nothing below the UI ever sees a second currency.
/// </summary>
public sealed class CurrencyDisplay
{
    /// <summary>US dollars per tonne. The exercise's internal unit and the deck's own unit.</summary>
    public const string Usd = "USD";

    /// <summary>Vietnamese dong, shown by converting the internal dollars at <see cref="VndPerUsd"/>.</summary>
    public const string Vnd = "VND";

    /// <summary>A dong rate the training box can change without touching the engine.</summary>
    public const decimal DefaultVndPerUsd = 25_000m;

    public CurrencyDisplay(string code, decimal vndPerUsd = DefaultVndPerUsd)
    {
        Code = Normalise(code);
        VndPerUsd = vndPerUsd;
    }

    /// <summary>What a host that says nothing about currency shows.</summary>
    public static CurrencyDisplay UsdDefault { get; } = new(Usd);

    /// <summary>The ISO code written beside every amount, so a figure is never ambiguous.</summary>
    public string Code { get; }

    /// <summary>How many dong one internal (US dollar) unit is worth.</summary>
    public decimal VndPerUsd { get; }

    public bool IsVnd => string.Equals(Code, Vnd, StringComparison.Ordinal);

    /// <summary>Turns an engine amount into the number a screen shows.</summary>
    public decimal ToDisplay(decimal internalAmount) => IsVnd ? internalAmount * VndPerUsd : internalAmount;

    /// <summary>Turns a number a person typed into the engine's internal unit.</summary>
    public decimal ToInternal(decimal displayAmount) =>
        IsVnd && VndPerUsd != 0m ? displayAmount / VndPerUsd : displayAmount;

    /// <summary>A whole amount, grouped for the reader, with its currency code.</summary>
    public string Money(decimal internalAmount) => Text(internalAmount, "N0");

    /// <summary>An amount to two decimal places, for figures that carry cents.</summary>
    public string Money2(decimal internalAmount) => Text(internalAmount, "N2");

    /// <summary>A price per tonne with its code: cents in dollars, whole dong in dong.</summary>
    public string Price(decimal internalAmount) => Text(internalAmount, IsVnd ? "N0" : "N2");

    /// <summary>"USD" or "VND"; anything else is a configuration mistake the host reports.</summary>
    public static bool IsKnown(string? code) =>
        string.Equals(code?.Trim(), Usd, StringComparison.OrdinalIgnoreCase)
        || string.Equals(code?.Trim(), Vnd, StringComparison.OrdinalIgnoreCase);

    private static string Normalise(string? code) =>
        string.Equals(code?.Trim(), Vnd, StringComparison.OrdinalIgnoreCase) ? Vnd : Usd;

    private string Text(decimal internalAmount, string format) =>
        ToDisplay(internalAmount).ToString(format, CultureInfo.CurrentCulture) + " " + Code;
}
