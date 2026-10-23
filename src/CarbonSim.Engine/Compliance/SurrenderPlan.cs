using CarbonSim.Engine.Domain;

namespace CarbonSim.Engine.Compliance;

/// <summary>
/// What a human company has said it will surrender for a year when surrender is manual. The year
/// end surrenders this much and no more, so a company that sets nothing surrenders nothing.
/// </summary>
public sealed record SurrenderInstruction(Company Company, int Year, decimal Allowances, decimal Offsets);

/// <summary>
/// What the year end would surrender for a company if it ran now, and what that would cost. The year
/// end acts on the same plan, so the penalty a player is warned about is the one the year end charges.
/// </summary>
/// <param name="Manual">True when this company surrenders only what it has set.</param>
/// <param name="HasInstruction">True when the company has set a surrender for the year.</param>
/// <param name="OffsetsAvailable">Offsets the year may count, held and inside the usage limit.</param>
/// <param name="AllowancesAvailable">Allowances of the year's vintage and older.</param>
/// <param name="Offsets">Offsets that would be surrendered.</param>
/// <param name="Allowances">Allowances that would be surrendered.</param>
public sealed record SurrenderPlan(
    Company Company,
    int Year,
    bool Manual,
    bool HasInstruction,
    decimal Obligation,
    decimal OffsetsAvailable,
    decimal AllowancesAvailable,
    decimal Offsets,
    decimal Allowances,
    decimal Shortfall,
    decimal EstimatedPenalty)
{
    /// <summary>Held for the year less what it emits: positive is long, as on every screen.</summary>
    public decimal ResultingPosition => OffsetsAvailable + AllowancesAvailable - Obligation;
}
