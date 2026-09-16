using CarbonSim.Engine.Domain;

namespace CarbonSim.Engine.Finance;

/// <summary>Why money moved, so the reports can add it up the way the original does.</summary>
public enum CashCategory
{
    /// <summary>The company's starting capital, or a top-up from the administrator.</summary>
    Capital,

    /// <summary>Normal operating profit for a year.</summary>
    OperatingProfit,

    /// <summary>Capital spent on an abatement project.</summary>
    AbatementCapital,

    /// <summary>An abatement project's running revenue or cost.</summary>
    AbatementNetRevenue,

    AuctionPurchase,
    SecondaryPurchase,
    OtcPurchase,

    /// <summary>Money in from selling allowances or offsets on any market.</summary>
    InstrumentSale,

    /// <summary>Cash penalty for a short position.</summary>
    Penalty,

    /// <summary>A fine imposed by the administrator.</summary>
    Fine,

    /// <summary>Interest on a negative balance.</summary>
    Interest,
}

/// <summary>One movement of money, signed: positive is money in, negative is money out.</summary>
public sealed record CashMovement(
    long Sequence,
    int Year,
    Company Company,
    CashCategory Category,
    decimal Amount,
    string Description)
{
    public override string ToString() => $"Year {Year} {Company.Name} {Category} {Amount} ({Description})";
}
