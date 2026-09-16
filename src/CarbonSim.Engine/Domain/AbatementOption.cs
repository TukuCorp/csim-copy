namespace CarbonSim.Engine.Domain;

/// <summary>
/// One abatement project a unit can buy: an upfront cost, the tonnes CO2e it removes each
/// year once running, how long it takes to build, how long it lasts, and its annual net
/// revenue (negative when the project costs money to run). Reductions are additive.
/// </summary>
public sealed record AbatementOption
{
    public AbatementOption(
        string code,
        string name,
        decimal upfrontCost,
        decimal annualReduction,
        int implementationYears,
        int lifetimeYears,
        decimal annualNetRevenue)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            throw new ArgumentException("An abatement option needs a code.", nameof(code));
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("An abatement option needs a name.", nameof(name));
        }

        if (upfrontCost < 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(upfrontCost), upfrontCost, "An abatement option cannot have a negative upfront cost.");
        }

        if (annualReduction <= 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(annualReduction), annualReduction, "An abatement option must reduce at least some emissions.");
        }

        if (implementationYears < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(implementationYears), implementationYears, "Implementation time cannot be negative.");
        }

        if (lifetimeYears < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(lifetimeYears), lifetimeYears, "An abatement option must last at least one year.");
        }

        Code = code.Trim();
        Name = name.Trim();
        UpfrontCost = upfrontCost;
        AnnualReduction = annualReduction;
        ImplementationYears = implementationYears;
        LifetimeYears = lifetimeYears;
        AnnualNetRevenue = annualNetRevenue;
    }

    /// <summary>Scenario-local identifier, unique within a unit's menu (for example "P1").</summary>
    public string Code { get; }

    public string Name { get; }

    /// <summary>Capital spent when the project is implemented, in simulation currency.</summary>
    public decimal UpfrontCost { get; }

    /// <summary>Tonnes CO2e removed per year once the project is operating.</summary>
    public decimal AnnualReduction { get; }

    /// <summary>Virtual years between implementation and the start of operation.</summary>
    public int ImplementationYears { get; }

    /// <summary>Operating years before the reduction stops.</summary>
    public int LifetimeYears { get; }

    /// <summary>Annual revenue (positive) or running cost (negative) once operating.</summary>
    public decimal AnnualNetRevenue { get; }

    /// <summary>Capital spent per tonne removed over the project's life.</summary>
    public decimal CapitalCostPerTonne => UpfrontCost / (AnnualReduction * LifetimeYears);

    /// <summary>
    /// What a tonne removed really costs once the project's running revenue or cost is counted;
    /// negative means the project earns money per tonne. This is the figure the marginal
    /// abatement cost curve and the bots rank on.
    /// </summary>
    public decimal NetCostPerTonne => CapitalCostPerTonne - (AnnualNetRevenue / AnnualReduction);

    /// <summary>
    /// Return over the project's lifetime against the capital it ties up, or null when it
    /// needs no capital at all.
    /// </summary>
    public decimal? ForecastRoi =>
        UpfrontCost == 0m ? null : ((AnnualNetRevenue * LifetimeYears) - UpfrontCost) / UpfrontCost;

    public override string ToString() => $"{Code} {Name}";
}
