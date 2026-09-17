using CarbonSim.Data.Accounts;
using CarbonSim.Data.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CarbonSim.Data;

/// <summary>
/// The one database of the application: the accounts people log in with, and the saved
/// simulations. The engine knows nothing about it.
/// </summary>
public sealed class CarbonSimDbContext(DbContextOptions<CarbonSimDbContext> options) : DbContext(options)
{
    public DbSet<PlayerAccount> Accounts => Set<PlayerAccount>();

    /// <summary>Saved runs; every other simulation table hangs off this one by foreign key.</summary>
    public DbSet<SimulationRecord> Simulations => Set<SimulationRecord>();

    public DbSet<TradingSystemRecord> TradingSystems => Set<TradingSystemRecord>();

    public DbSet<ParametersRecord> SimulationParameters => Set<ParametersRecord>();

    public DbSet<BausGrowthRecord> BausGrowthBands => Set<BausGrowthRecord>();

    public DbSet<SectorRecord> Sectors => Set<SectorRecord>();

    public DbSet<AbatementMenuRecord> AbatementMenu => Set<AbatementMenuRecord>();

    public DbSet<PlayerRecord> Players => Set<PlayerRecord>();

    public DbSet<CompanyRecord> Companies => Set<CompanyRecord>();

    public DbSet<UnitRecord> Units => Set<UnitRecord>();

    public DbSet<AbatementOptionRecord> AbatementOptions => Set<AbatementOptionRecord>();

    public DbSet<AllocationYearRecord> AllocationYears => Set<AllocationYearRecord>();

    public DbSet<UnitAllocationRecord> UnitAllocations => Set<UnitAllocationRecord>();

    public DbSet<UnitAllocationYearRecord> UnitAllocationYears => Set<UnitAllocationYearRecord>();

    public DbSet<ImplementedAbatementRecord> ImplementedAbatements => Set<ImplementedAbatementRecord>();

    public DbSet<ShutdownRecord> UnitShutdowns => Set<ShutdownRecord>();

    public DbSet<LedgerAvailableRecord> LedgerAvailable => Set<LedgerAvailableRecord>();

    public DbSet<LedgerEscrowedRecord> LedgerEscrowed => Set<LedgerEscrowedRecord>();

    public DbSet<LedgerGrantedYearRecord> LedgerGrantedYears => Set<LedgerGrantedYearRecord>();

    public DbSet<GovernmentRecord> Government => Set<GovernmentRecord>();

    public DbSet<GovernmentReserveRecord> GovernmentReserves => Set<GovernmentReserveRecord>();

    public DbSet<GovernmentIssuedRecord> GovernmentIssued => Set<GovernmentIssuedRecord>();

    public DbSet<CashMovementRecord> CashMovements => Set<CashMovementRecord>();

    public DbSet<CompanyComplianceRecord> CompanyCompliances => Set<CompanyComplianceRecord>();

    public DbSet<FineRecord> Fines => Set<FineRecord>();

    public DbSet<ReconciledCompanyYearRecord> ReconciledCompanyYears => Set<ReconciledCompanyYearRecord>();

    public DbSet<JournalTradeRecord> JournalTrades => Set<JournalTradeRecord>();

    public DbSet<ExchangeRecord> Exchanges => Set<ExchangeRecord>();

    public DbSet<OrderBookRecord> OrderBooks => Set<OrderBookRecord>();

    public DbSet<OrderRecord> Orders => Set<OrderRecord>();

    public DbSet<BookTradeRecord> BookTrades => Set<BookTradeRecord>();

    public DbSet<OtcOfferRecord> OtcOffers => Set<OtcOfferRecord>();

    public DbSet<AuctionRecord> Auctions => Set<AuctionRecord>();

    public DbSet<AuctionLotRecord> AuctionLots => Set<AuctionLotRecord>();

    public DbSet<AuctionBidRecord> AuctionBids => Set<AuctionBidRecord>();

    public DbSet<AuctionResultRecord> AuctionResults => Set<AuctionResultRecord>();

    public DbSet<AuctionAwardRecord> AuctionAwards => Set<AuctionAwardRecord>();

    public DbSet<AuctionRejectedBidRecord> AuctionRejectedBids => Set<AuctionRejectedBidRecord>();

    public DbSet<ClockRecord> Clocks => Set<ClockRecord>();

    public DbSet<BotRecord> Bots => Set<BotRecord>();

    public DbSet<BotUnitRecord> BotUnits => Set<BotUnitRecord>();

    public DbSet<BotTriggerTimeRecord> BotTriggerTimes => Set<BotTriggerTimeRecord>();

    public DbSet<BotTriggerDoneRecord> BotTriggerDone => Set<BotTriggerDoneRecord>();

    public DbSet<BotBidSectionRecord> BotBidSections => Set<BotBidSectionRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(CarbonSimDbContext).Assembly);
    }
}
