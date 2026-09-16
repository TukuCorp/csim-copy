namespace CarbonSim.Engine.Scenarios;

/// <summary>
/// Wire format of a scenario file. Every value is nullable so the loader can report what a
/// file is missing rather than failing on the first absent property.
/// </summary>
internal sealed class ScenarioFile
{
    public string? Name { get; set; }

    /// <summary>Seed for the simulation's random stream; the same seed reproduces the run.</summary>
    public ulong? Seed { get; set; }

    public ParametersFile? Parameters { get; set; }

    public List<SectorFile>? Sectors { get; set; }

    public List<CompanyFile>? Companies { get; set; }
}

internal sealed class ParametersFile
{
    public decimal? Cap { get; set; }

    public decimal? AnnualCapReductionRate { get; set; }

    public decimal? FreeAllocationShare { get; set; }

    public int? Years { get; set; }

    public decimal? OffsetUsageLimit { get; set; }

    public decimal? BankingLimit { get; set; }

    public decimal? PenaltyPerTonne { get; set; }

    public decimal? PenaltyAllowanceDebit { get; set; }

    public decimal? AuctionFloorPrice { get; set; }

    public decimal? AuctionCeilingPrice { get; set; }

    public int? AuctionsPerYear { get; set; }

    public decimal? YearLengthMinutes { get; set; }

    public decimal? AuctionDurationMinutes { get; set; }

    public decimal? TradingOpenShareOfYear { get; set; }

    public decimal? VolatilityBand { get; set; }

    /// <summary>Yearly interest on a negative balance, as a fraction.</summary>
    public decimal? OverdraftInterestRate { get; set; }

    public List<BausGrowthFile>? BausGrowthBySector { get; set; }
}

internal sealed class BausGrowthFile
{
    public string? Sector { get; set; }

    public decimal? MinAnnualRate { get; set; }

    public decimal? MaxAnnualRate { get; set; }
}

internal sealed class SectorFile
{
    public string? Name { get; set; }

    public decimal? EmissionShare { get; set; }

    public List<AbatementOptionFile>? AbatementOptions { get; set; }
}

/// <summary>
/// An abatement project as a scenario author writes it: sized as a share of the unit's
/// baseline emissions and priced per tonne, so one menu serves every unit in a sector. The
/// loader materialises it into per-unit absolute tonnes and money.
/// </summary>
internal sealed class AbatementOptionFile
{
    public string? Code { get; set; }

    public string? Name { get; set; }

    public decimal? AnnualReductionShare { get; set; }

    public decimal? UpfrontCostPerTonne { get; set; }

    public decimal? AnnualNetRevenuePerTonne { get; set; }

    public int? ImplementationYears { get; set; }

    public int? LifetimeYears { get; set; }
}

internal sealed class CompanyFile
{
    public string? Name { get; set; }

    public string? Sector { get; set; }

    /// <summary>"human" or "ai"; anything else is rejected.</summary>
    public string? OwnerKind { get; set; }

    /// <summary>Starting cash, in simulation currency.</summary>
    public decimal? Capital { get; set; }

    /// <summary>How far below zero the company's balance may go.</summary>
    public decimal? OverdraftLimit { get; set; }

    public List<UnitFile>? Units { get; set; }
}

internal sealed class UnitFile
{
    public string? Name { get; set; }

    public decimal? BaselineEmissions { get; set; }

    /// <summary>What the unit earns in a normal year, before abatement and compliance.</summary>
    public decimal? NormalOperatingProfit { get; set; }
}
