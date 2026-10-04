using System.Text.Json;
using System.Text.Json.Serialization;

namespace CarbonSim.Web.Admin;

/// <summary>
/// A scenario the administrator is composing, in the same shape as the scenario file the loader
/// reads. The console loads the configured scenario into one of these, the administrator changes
/// the parameters, sectors and abatement menus, and the draft is written back out as JSON for the
/// engine's own loader, so the console never builds engine objects itself and the same validation
/// the engine applies to a file applies to a form.
/// </summary>
public sealed class ScenarioDraft
{
    public string? Name { get; set; }

    public ulong? Seed { get; set; }

    public DraftParameters Parameters { get; set; } = new();

    public List<DraftSector> Sectors { get; set; } = [];

    public List<DraftCompany> Companies { get; set; } = [];

    private static readonly JsonSerializerOptions ReadOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    private static readonly JsonSerializerOptions WriteOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = true,
    };

    public static ScenarioDraft Parse(string json)
    {
        ArgumentNullException.ThrowIfNull(json);

        return JsonSerializer.Deserialize<ScenarioDraft>(json, ReadOptions)
            ?? throw new InvalidOperationException("That scenario document is empty.");
    }

    public string ToJson() => JsonSerializer.Serialize(this, WriteOptions);

    /// <summary>A copy, so an editor never mutates the draft the loader handed it.</summary>
    public ScenarioDraft Clone() => Parse(ToJson());
}

public sealed class DraftParameters
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

    public decimal? OverdraftInterestRate { get; set; }

    public decimal? GovernmentReserveToAuctionPercent { get; set; }

    public List<DraftGrowth> BausGrowthBySector { get; set; } = [];
}

public sealed class DraftGrowth
{
    public string? Sector { get; set; }

    public decimal? MinAnnualRate { get; set; }

    public decimal? MaxAnnualRate { get; set; }
}

public sealed class DraftSector
{
    public string? Name { get; set; }

    public decimal? EmissionShare { get; set; }

    public List<DraftAbatementOption> AbatementOptions { get; set; } = [];
}

public sealed class DraftAbatementOption
{
    public string? Code { get; set; }

    public string? Name { get; set; }

    public decimal? AnnualReductionShare { get; set; }

    public decimal? UpfrontCostPerTonne { get; set; }

    public decimal? AnnualNetRevenuePerTonne { get; set; }

    public int? ImplementationYears { get; set; }

    public int? LifetimeYears { get; set; }
}

public sealed class DraftCompany
{
    public string? Name { get; set; }

    public string? Sector { get; set; }

    public string? OwnerKind { get; set; }

    public decimal? Capital { get; set; }

    public decimal? OverdraftLimit { get; set; }

    public List<DraftUnit> Units { get; set; } = [];
}

public sealed class DraftUnit
{
    public string? Name { get; set; }

    public decimal? BaselineEmissions { get; set; }

    public decimal? NormalOperatingProfit { get; set; }
}
