using CarbonSim.Engine.Domain;
using CarbonSim.Engine.Market;

namespace CarbonSim.Engine.Snapshot;

/// <summary>
/// Everything needed to rebuild one running simulation exactly, as plain records. A hosted
/// service writes this somewhere and reads it back; nothing here is a live engine object, so
/// the snapshot can be serialised, diffed or compared without dragging the engine along. The
/// sub-records hang off this one so a whole run is saved and reloaded in a single unit.
/// </summary>
/// <remarks>
/// Two rules keep a restore faithful. First, every value the engine ever drew from
/// <see cref="Randomness.SimulationRandom"/> is carried here, so a restore never re-draws and
/// cannot drift from the original. Second, <see cref="RandomState"/> is the stream position at
/// capture time, so the very next draw after a restore is the draw the original would have
/// made. Balances and sequences are stored as they stood rather than replayed, so replaying a
/// year's movements is never part of loading a run.
/// </remarks>
public sealed record SimulationSnapshot(
    Guid Id,
    string Name,
    ulong Seed,
    ulong RandomState,
    SimulationState State,
    int CurrentYear,
    TradingSystemSnapshot TradingSystem,
    IReadOnlyList<SectorSnapshot> Sectors,
    IReadOnlyList<PlayerSnapshot> Players,
    IReadOnlyList<CompanySnapshot> Companies,
    IReadOnlyList<UnitSnapshot> Units,
    AllocationPlanSnapshot Allocation,
    AbatementPortfolioSnapshot Abatements,
    AllowanceLedgerSnapshot Ledger,
    GovernmentSnapshot Government,
    CashLedgerSnapshot Cash,
    ComplianceRegisterSnapshot Compliance,
    MarketJournalSnapshot Journal,
    ExchangeSnapshot Exchange,
    OtcMarketSnapshot Otc,
    AuctionScheduleSnapshot Auctions,
    ClockSnapshot Clock,
    BotsSnapshot? Bots);

/// <summary>
/// The trading system the run is played under. A simulation runs one system today: the
/// allocation plan, the clock and the bots all read <c>TradingSystems.Single()</c>, so the
/// sectors and companies it covers are simply <see cref="SimulationSnapshot.Sectors"/> and
/// <see cref="SimulationSnapshot.Companies"/>.
/// </summary>
public sealed record TradingSystemSnapshot(int Id, string Name, ParametersSnapshot Parameters);

/// <summary>The administrator's numbers for the trading system, as configured for this run.</summary>
public sealed record ParametersSnapshot(
    decimal Cap,
    decimal AnnualCapReductionRate,
    decimal FreeAllocationShare,
    int Years,
    IReadOnlyList<SectorBausGrowthSnapshot> BausGrowthBySector,
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
    decimal OverdraftInterestRate);

/// <summary>The growth band a sector's business-as-usual emissions were drawn from.</summary>
public sealed record SectorBausGrowthSnapshot(string Sector, decimal MinAnnualRate, decimal MaxAnnualRate);

/// <summary>A covered sector, with the abatement menu every unit in it draws from.</summary>
public sealed record SectorSnapshot(
    string Name,
    decimal EmissionShare,
    IReadOnlyList<AbatementMenuSnapshot> AbatementMenu);

/// <summary>An abatement project as the scenario defines it for a sector, before being sized for a unit.</summary>
public sealed record AbatementMenuSnapshot(
    string Code,
    string Name,
    decimal AnnualReductionShare,
    decimal UpfrontCostPerTonne,
    decimal AnnualNetRevenuePerTonne,
    int ImplementationYears,
    int LifetimeYears);

/// <summary>A participant. Its companies are the ones naming it as owner in <see cref="CompanySnapshot"/>.</summary>
public sealed record PlayerSnapshot(int Id, string Name, PlayerKind Kind);

/// <summary>
/// A company as it stands: the identity, the sector and owner it belongs to, and the balances
/// its cash and allowance activity has left it with.
/// </summary>
/// <remarks>
/// Capital and escrow live here rather than on <see cref="CashLedgerSnapshot"/>, which holds
/// the movements that produced them; the two together are the ledger and its closing balance.
/// </remarks>
public sealed record CompanySnapshot(
    int Id,
    string Name,
    string SectorName,
    int OwnerPlayerId,
    decimal Capital,
    decimal EscrowedCash,
    decimal OverdraftLimit);

/// <summary>A unit and the abatement menu it was given, sized to its own baseline.</summary>
public sealed record UnitSnapshot(
    int Id,
    string Name,
    int CompanyId,
    decimal BaselineEmissions,
    decimal NormalOperatingProfit,
    bool AutoTrade,
    IReadOnlyList<AbatementOptionSnapshot> AbatementOptions);

/// <summary>An abatement project sized for the unit that can buy it.</summary>
public sealed record AbatementOptionSnapshot(
    string Code,
    string Name,
    decimal UpfrontCost,
    decimal AnnualReduction,
    int ImplementationYears,
    int LifetimeYears,
    decimal AnnualNetRevenue);

/// <summary>
/// What is being traded, as a kind and a vintage. <c>Product</c> is a struct the engine passes
/// around; this is its persisted form, never a dictionary key.
/// </summary>
public sealed record ProductSnapshot(ProductKind Kind, int Vintage)
{
    /// <summary>The persisted form of a vintaged allowance.</summary>
    public static ProductSnapshot Allowance(int vintage) => new(ProductKind.Allowance, vintage);

    /// <summary>The persisted form of an offset, which carries no vintage.</summary>
    public static ProductSnapshot Offset => new(ProductKind.Offset, 0);

    /// <summary>The engine product this snapshot stands for.</summary>
    public Product ToProduct() => new(Kind, Vintage);

    public override string ToString() => Kind == ProductKind.Offset ? "Offset" : $"Vintage {Vintage}";
}
