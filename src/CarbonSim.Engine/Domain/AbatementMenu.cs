namespace CarbonSim.Engine.Domain;

/// <summary>
/// An abatement project as a scenario author or administrator defines it for a sector: the
/// reduction is a share of whatever unit it lands on, and the economics are per tonne, so one
/// menu serves every unit in the sector. <see cref="ForBaseline"/> turns it into the absolute
/// tonnes and money a specific unit would spend and earn.
/// </summary>
public sealed record AbatementMenu
{
    public AbatementMenu(
        string code,
        string name,
        decimal annualReductionShare,
        decimal upfrontCostPerTonne,
        decimal annualNetRevenuePerTonne,
        int implementationYears,
        int lifetimeYears)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            throw new ArgumentException("An abatement menu entry needs a code.", nameof(code));
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("An abatement menu entry needs a name.", nameof(name));
        }

        if (annualReductionShare is <= 0m or > 1m)
        {
            throw new ArgumentOutOfRangeException(
                nameof(annualReductionShare),
                annualReductionShare,
                "A share of the unit's baseline emissions must be greater than 0 and at most 1.");
        }

        if (upfrontCostPerTonne < 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(upfrontCostPerTonne), upfrontCostPerTonne, "The upfront cost per tonne cannot be negative.");
        }

        if (implementationYears < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(implementationYears), implementationYears, "Implementation time cannot be negative.");
        }

        if (lifetimeYears < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(lifetimeYears), lifetimeYears, "A project must last at least one year.");
        }

        Code = code.Trim();
        Name = name.Trim();
        AnnualReductionShare = annualReductionShare;
        UpfrontCostPerTonne = upfrontCostPerTonne;
        AnnualNetRevenuePerTonne = annualNetRevenuePerTonne;
        ImplementationYears = implementationYears;
        LifetimeYears = lifetimeYears;
    }

    public string Code { get; }

    public string Name { get; }

    /// <summary>Share of the unit's baseline emissions the project removes each year.</summary>
    public decimal AnnualReductionShare { get; }

    /// <summary>Capital cost per tonne of annual reduction.</summary>
    public decimal UpfrontCostPerTonne { get; }

    /// <summary>Running revenue (positive) or cost (negative) per tonne removed.</summary>
    public decimal AnnualNetRevenuePerTonne { get; }

    public int ImplementationYears { get; }

    public int LifetimeYears { get; }

    /// <summary>Sizes this project for the unit that would implement it.</summary>
    public AbatementOption ForBaseline(decimal baselineEmissions)
    {
        decimal reduction = Round(AnnualReductionShare * baselineEmissions);

        if (reduction <= 0m)
        {
            throw new ArgumentOutOfRangeException(
                nameof(baselineEmissions),
                baselineEmissions,
                $"The baseline is too small for abatement '{Code}' to reduce anything.");
        }

        return new AbatementOption(
            Code,
            Name,
            Round(UpfrontCostPerTonne * reduction),
            reduction,
            ImplementationYears,
            LifetimeYears,
            Round(AnnualNetRevenuePerTonne * reduction));
    }

    /// <summary>The whole menu sized for one unit.</summary>
    public static IReadOnlyList<AbatementOption> For(Sector sector, decimal baselineEmissions)
    {
        ArgumentNullException.ThrowIfNull(sector);

        return [.. sector.AbatementMenu.Select(menu => menu.ForBaseline(baselineEmissions))];
    }

    private static decimal Round(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);

    public override string ToString() => $"{Code} {Name}";
}
