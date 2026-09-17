namespace CarbonSim.Data.Persistence;

/// <summary>
/// One saved run as it comes out of the database: the root row plus every child table filtered
/// to that simulation. The mapper turns this into an engine snapshot and back, so the repository
/// only has to query and write.
/// </summary>
/// <remarks>
/// Nothing here holds navigation properties, because a save writes every table from one flat
/// list and a load reads one table at a time. The engine's own identifier order is what the
/// list order is rebuilt from: players, companies and units were numbered in the order the
/// scenario listed them, so ordering by identifier restores the order the run was built in.
/// </remarks>
public sealed class SavedRun
{
    public SimulationRecord Root { get; init; } = new();

    public IReadOnlyList<TradingSystemRecord> TradingSystems { get; init; } = [];

    public IReadOnlyList<ParametersRecord> Parameters { get; init; } = [];

    public IReadOnlyList<BausGrowthRecord> BausGrowth { get; init; } = [];

    public IReadOnlyList<SectorRecord> Sectors { get; init; } = [];

    public IReadOnlyList<AbatementMenuRecord> AbatementMenu { get; init; } = [];

    public IReadOnlyList<PlayerRecord> Players { get; init; } = [];

    public IReadOnlyList<CompanyRecord> Companies { get; init; } = [];

    public IReadOnlyList<UnitRecord> Units { get; init; } = [];

    public IReadOnlyList<AbatementOptionRecord> AbatementOptions { get; init; } = [];

    public IReadOnlyList<AllocationYearRecord> AllocationYears { get; init; } = [];

    public IReadOnlyList<UnitAllocationRecord> UnitAllocations { get; init; } = [];

    public IReadOnlyList<UnitAllocationYearRecord> UnitAllocationYears { get; init; } = [];

    public IReadOnlyList<ImplementedAbatementRecord> ImplementedAbatements { get; init; } = [];

    public IReadOnlyList<ShutdownRecord> Shutdowns { get; init; } = [];

    public IReadOnlyList<LedgerAvailableRecord> LedgerAvailable { get; init; } = [];

    public IReadOnlyList<LedgerEscrowedRecord> LedgerEscrowed { get; init; } = [];

    public IReadOnlyList<LedgerGrantedYearRecord> LedgerGrantedYears { get; init; } = [];

    public GovernmentRecord? Government { get; init; }

    public IReadOnlyList<GovernmentReserveRecord> GovernmentReserves { get; init; } = [];

    public IReadOnlyList<GovernmentIssuedRecord> GovernmentIssued { get; init; } = [];

    public IReadOnlyList<CashMovementRecord> CashMovements { get; init; } = [];

    public IReadOnlyList<CompanyComplianceRecord> Compliances { get; init; } = [];

    public IReadOnlyList<FineRecord> Fines { get; init; } = [];

    public IReadOnlyList<ReconciledCompanyYearRecord> Reconciled { get; init; } = [];

    public IReadOnlyList<JournalTradeRecord> JournalTrades { get; init; } = [];

    public ExchangeRecord? Exchange { get; init; }

    public IReadOnlyList<OrderBookRecord> OrderBooks { get; init; } = [];

    public IReadOnlyList<OrderRecord> Orders { get; init; } = [];

    public IReadOnlyList<BookTradeRecord> BookTrades { get; init; } = [];

    public IReadOnlyList<OtcOfferRecord> OtcOffers { get; init; } = [];

    public IReadOnlyList<AuctionRecord> Auctions { get; init; } = [];

    public IReadOnlyList<AuctionLotRecord> AuctionLots { get; init; } = [];

    public IReadOnlyList<AuctionBidRecord> AuctionBids { get; init; } = [];

    public IReadOnlyList<AuctionResultRecord> AuctionResults { get; init; } = [];

    public IReadOnlyList<AuctionAwardRecord> AuctionAwards { get; init; } = [];

    public IReadOnlyList<AuctionRejectedBidRecord> AuctionRejectedBids { get; init; } = [];

    public ClockRecord? Clock { get; init; }

    public IReadOnlyList<BotRecord> Bots { get; init; } = [];

    public IReadOnlyList<BotUnitRecord> BotUnits { get; init; } = [];

    public IReadOnlyList<BotTriggerTimeRecord> BotTriggerTimes { get; init; } = [];

    public IReadOnlyList<BotTriggerDoneRecord> BotTriggerDone { get; init; } = [];

    public IReadOnlyList<BotBidSectionRecord> BotBidSections { get; init; } = [];
}
