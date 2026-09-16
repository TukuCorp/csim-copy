namespace CarbonSim.Engine.Domain;

/// <summary>
/// An emitting facility. Emissions are not tracked here yet: this is the identity,
/// ownership and baseline shape that allocation, abatement and trading build on.
/// </summary>
public sealed class Unit
{
    public Unit(
        int id,
        string name,
        Company company,
        decimal baselineEmissions,
        IReadOnlyList<AbatementOption>? abatementOptions = null,
        decimal normalOperatingProfit = 0m)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("A unit needs a name.", nameof(name));
        }

        ArgumentNullException.ThrowIfNull(company);

        if (baselineEmissions < 0m)
        {
            throw new ArgumentOutOfRangeException(
                nameof(baselineEmissions),
                baselineEmissions,
                "Baseline emissions cannot be negative.");
        }

        Id = id;
        Name = name.Trim();
        Company = company;
        BaselineEmissions = baselineEmissions;
        AbatementOptions = abatementOptions ?? [];
        NormalOperatingProfit = normalOperatingProfit;
    }

    public int Id { get; }

    public string Name { get; }

    public Company Company { get; }

    /// <summary>The sector the unit is covered under, taken from its company.</summary>
    public Sector Sector => Company.Sector;

    /// <summary>Year-0 emissions in tonnes CO2e, before business-as-usual growth.</summary>
    public decimal BaselineEmissions { get; }

    /// <summary>Abatement projects this unit may implement, cheapest first is not guaranteed.</summary>
    public IReadOnlyList<AbatementOption> AbatementOptions { get; }

    /// <summary>
    /// Whether a bot trades this unit for its owner. Human players switch it on per unit to get
    /// the original's trade assistance; AI companies are automated whether this is set or not.
    /// </summary>
    public bool AutoTrade { get; set; }

    /// <summary>
    /// What the unit earns in a normal year before abatement and compliance, in simulation
    /// currency. Scenario data: the clone does not model the unit's physical output.
    /// </summary>
    public decimal NormalOperatingProfit { get; }

    public override string ToString() => Name;
}
