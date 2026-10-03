using CarbonSim.Web.Player;

namespace CarbonSim.Web.Admin;

/// <summary>
/// One run as the administrator's list sees it: enough to choose between runs without reading
/// any of them in full.
/// </summary>
public sealed record AdminRunSummary(
    Guid Id,
    string Name,
    string State,
    int Year,
    int Years,
    int Companies,
    int HumanCompanies,
    int Units,
    bool MessagingEnabled);

/// <summary>One saved run on the shelf, as the repository reports it.</summary>
public sealed record AdminSavedRun(Guid Id, string Name, string State, int CurrentYear, bool Live);

public sealed record AdminGrowthView(string Sector, decimal MinAnnualRate, decimal MaxAnnualRate);

/// <summary>The parameters the run is governed by, for the console's read-only summary.</summary>
public sealed record AdminParametersView(
    decimal Cap,
    decimal AnnualCapReductionRate,
    decimal FreeAllocationShare,
    int Years,
    decimal OffsetUsageLimit,
    decimal BankingLimit,
    decimal PenaltyPerTonne,
    decimal PenaltyAllowanceDebit,
    decimal AuctionFloorPrice,
    decimal AuctionCeilingPrice,
    int AuctionsPerYear,
    TimeSpan YearLength,
    TimeSpan AuctionDuration,
    decimal TradingOpenShareOfYear,
    decimal VolatilityBand,
    decimal OverdraftInterestRate,
    IReadOnlyList<AdminGrowthView> BausGrowth);

public sealed record AdminAbatementOptionView(
    int Index,
    string Code,
    string Name,
    decimal AnnualReduction,
    decimal UpfrontCost,
    int ImplementationYears,
    int LifetimeYears,
    decimal AnnualNetRevenue,
    bool Implemented);

public sealed record AdminSectorView(
    string Name,
    decimal EmissionShare,
    int Units,
    decimal FreeAllocationThisYear);

public sealed record AdminCompanyView(
    int Id,
    string Name,
    string Sector,
    string Owner,
    bool Automated,
    decimal Capital,
    decimal Emissions,
    decimal Obligation,
    decimal FreeAllocation,
    decimal Shortfall,
    decimal CostOfCompliance,
    decimal MarginalCost,
    decimal FinalPosition,
    bool Reconciled,
    decimal PenaltyCash);

public sealed record AdminUnitView(
    int Id,
    string Name,
    string Company,
    string Sector,
    decimal BaselineEmissions,
    decimal FreeAllocation,
    decimal ForecastEmissions,
    decimal Reduction,
    bool AutoTrade,
    bool ShutDown,
    int ImplementedCount,
    IReadOnlyList<AdminAbatementOptionView> Opportunities);

public sealed record AdminLeaderView(
    int Rank,
    string Company,
    string Unit,
    string Player,
    decimal MarginalCost,
    decimal FinalPosition,
    bool Automated);

/// <summary>One virtual year's trading, for the historical average price report.</summary>
public sealed record AdminPriceView(
    int Year,
    decimal AllowanceVolume,
    decimal AllowanceAverage,
    decimal OffsetVolume,
    decimal OffsetAverage,
    decimal? AuctionAverage,
    decimal? AuctionLow,
    decimal? AuctionHigh,
    int Trades);

public sealed record AdminSurrenderView(
    int CompanyId,
    string Company,
    string Units,
    decimal Emissions,
    decimal Obligation,
    decimal Covered,
    decimal AllowancesSurrendered,
    decimal OffsetsSurrendered,
    decimal Banked,
    decimal Forfeited,
    decimal Shortfall,
    decimal PenaltyCash,
    bool Reconciled);

/// <summary>Everything the console shows for one run, gathered while its gate was held.</summary>
public sealed record AdminRunDetail(
    Guid Id,
    string Name,
    string State,
    int Year,
    int Years,
    int Auction,
    int AuctionsPerYear,
    bool AuctionOpen,
    bool ClockRunning,
    bool MessagingEnabled,
    TimeSpan TimeLeftInYear,
    TimeSpan TimeToAuctionOpen,
    TimeSpan TimeToAuctionClose,
    AdminParametersView Parameters,
    IReadOnlyList<AdminSectorView> Sectors,
    IReadOnlyList<AdminCompanyView> Companies,
    IReadOnlyList<AdminUnitView> Units,
    IReadOnlyList<AdminLeaderView> Leaderboard,
    IReadOnlyList<AdminPriceView> Prices,
    IReadOnlyList<AdminSurrenderView> Surrender,
    SystemTotalsView ThisYear,
    SystemTotalsView ToDate,
    decimal GovernmentRevenue,
    IReadOnlyList<HoldingView> GovernmentReserve,
    bool CanEndSimulation);

/// <summary>What the registration gate currently allows, for the console's setup screen.</summary>
public sealed record AdminRegistrationView(bool Open, string Pin);

/// <summary>The console's whole picture: who is signed in, the runs on offer, one run in detail.</summary>
public sealed record AdminSnapshot(
    bool IsAdministrator,
    string? AccountName,
    string? Error,
    string? Status,
    IReadOnlyList<AdminRunSummary> Runs,
    IReadOnlyList<AdminSavedRun> Saved,
    Guid? RunId,
    AdminRunDetail? Run,
    AdminRegistrationView Registration)
{
    public static AdminSnapshot Denied(string? accountName) => new(
        false,
        accountName,
        null,
        null,
        [],
        [],
        null,
        null,
        new AdminRegistrationView(false, string.Empty));
}

/// <summary>One report the console can export as CSV.</summary>
public static class AdminReport
{
    public const string System = "system";
    public const string Companies = "companies";
    public const string Units = "units";
    public const string Prices = "prices";
    public const string Leaderboard = "leaderboard";

    public static readonly IReadOnlyList<string> All = [System, Companies, Units, Prices, Leaderboard];

    public static bool IsKnown(string report) => All.Contains(report, StringComparer.OrdinalIgnoreCase);
}
