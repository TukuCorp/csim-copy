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
        decimal normalOperatingProfit = 0m,
        decimal allocationFactor = 1m,
        decimal offsetSupplyShare = 0m)
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

        if (allocationFactor <= 0m)
        {
            throw new ArgumentOutOfRangeException(
                nameof(allocationFactor),
                allocationFactor,
                "A unit's allocation factor has to be greater than zero.");
        }

        if (offsetSupplyShare < 0m)
        {
            throw new ArgumentOutOfRangeException(
                nameof(offsetSupplyShare),
                offsetSupplyShare,
                "A unit's offset supply cannot be negative.");
        }

        Id = id;
        Name = name.Trim();
        Company = company;
        BaselineEmissions = baselineEmissions;
        AbatementOptions = abatementOptions ?? [];
        NormalOperatingProfit = normalOperatingProfit;
        AllocationFactor = allocationFactor;
        OffsetSupplyShare = offsetSupplyShare;
    }

    public int Id { get; }

    public string Name { get; }

    public Company Company { get; }

    /// <summary>The sector the unit is covered under, taken from its company.</summary>
    public Sector Sector => Company.Sector;

    /// <summary>Year-0 emissions in tonnes CO2e, before business-as-usual growth.</summary>
    public decimal BaselineEmissions { get; }

    /// <summary>
    /// How this unit's baseline is weighted when its sector's free cake is shared out. One is a
    /// plain baseline share; a unit above one was allocated on a more generous historic basis than
    /// its baseline implies, which is what leaves it holding more allowances than it needs, and a
    /// unit below one was allocated on a tighter basis. Scenario data, and the only thing in the
    /// clone that produces a two-sided market: with every unit on the same basis the whole fleet
    /// is short by the same fraction and nobody has anything to sell.
    /// </summary>
    public decimal AllocationFactor { get; }

    /// <summary>
    /// The share of its business-as-usual emissions the unit is handed in offsets each year, with
    /// its free allocation: the scenario's offset supply. A unit above the offset usage limit is an
    /// offset provider, holding more than it may surrender and selling the rest.
    /// </summary>
    public decimal OffsetSupplyShare { get; }

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
