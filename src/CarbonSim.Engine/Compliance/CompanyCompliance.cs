using CarbonSim.Engine.Domain;

namespace CarbonSim.Engine.Compliance;

/// <summary>
/// What one company's year amounted to: what it had to cover, how it covered it, what it could
/// keep, and what it lost. Every figure is in tonnes CO2e except the penalty, which is money.
/// </summary>
public sealed record CompanyCompliance(
    int Year,
    Company Company,
    decimal Obligation,
    decimal OffsetsSurrendered,
    decimal AllowancesSurrendered,
    decimal Banked,
    decimal Forfeited,
    decimal Shortfall,
    decimal PenaltyCash,
    decimal PenaltyAllowanceDebit)
{
    /// <summary>True when the company covered its whole obligation.</summary>
    public bool IsCompliant => Shortfall == 0m;

    /// <summary>Tonnes covered by surrendering something, offsets plus allowances.</summary>
    public decimal Covered => OffsetsSurrendered + AllowancesSurrendered;

    public override string ToString() =>
        $"Year {Year} {Company.Name}: {Covered} of {Obligation} covered, shortfall {Shortfall}, banked {Banked}, forfeited {Forfeited}";
}

/// <summary>A penalty an administrator has imposed on a unit.</summary>
public sealed record Fine(int Id, Unit Unit, Company Company, decimal Amount, string Description, int Year)
{
    public override string ToString() => $"Fine {Id} on {Unit.Name}: {Amount} for {Description}";
}
