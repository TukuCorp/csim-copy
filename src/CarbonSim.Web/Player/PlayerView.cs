namespace CarbonSim.Web.Player;

/// <summary>
/// One moment of a run, arranged the way the player screens read it. The engine's objects are
/// owned by the driver thread; everything the screens show is copied into these plain records
/// while the run's gate is held, so a page never touches live engine state.
/// </summary>
public sealed record PlayerView(
    Guid SimulationId,
    string SimulationName,
    ulong Seed,
    string State,
    int Year,
    int Years,
    int Auction,
    int AuctionsPerYear,
    bool AuctionOpen,
    bool ClockRunning,
    TimeSpan TimeLeftInYear,
    TimeSpan TimeToAuctionOpen,
    TimeSpan TimeToAuctionClose,
    string AccountName,
    string CompanyName,
    string SectorName,
    string PlayerName,
    bool IsAutomated,
    FinancialView Finance,
    ComplianceView Compliance,
    PositionView Position,
    IReadOnlyList<UnitView> Units,
    IReadOnlyList<AbatementView> Abatements,
    IReadOnlyList<AuctionView> Auctions,
    AuctionView? CurrentAuction,
    decimal AllowancesToBeAuctionedThisYear,
    IReadOnlyList<ProductBookView> Books,
    IReadOnlyList<ProductView> TradableProducts,
    AuctionPriceSummaryView AuctionPrices,
    IReadOnlyList<CounterpartyView> Counterparties,
    IReadOnlyList<OtcOfferView> OffersToAnswer,
    IReadOnlyList<OtcOfferView> MyOffers,
    IReadOnlyList<HoldingView> Holdings,
    IReadOnlyList<TradeRow> MyTrades,
    IReadOnlyList<LeaderboardRow> Leaderboard,
    SystemInfoView System,
    IReadOnlyList<ChatLine> Messages,
    bool MessagingEnabled);

/// <summary>A run the host is driving, for the waiting screen's list.</summary>
public sealed record RunSummary(
    Guid Id,
    string Name,
    string State,
    int Year,
    int Years,
    int Companies,
    int HumanCompanies,
    int Units,
    int AuctionsPerYear,
    TimeSpan YearLength,
    decimal Cap,
    decimal FreeAllocationShare,
    decimal OffsetUsageLimit,
    decimal BankingLimit,
    decimal AuctionFloorPrice,
    decimal AuctionCeilingPrice,
    decimal TradingOpenShareOfYear);

/// <summary>What is traded: a vintage-dated allowance or an offset. The key is the label the feed uses.</summary>
public sealed record ProductView(string Key, int Vintage, bool IsOffset)
{
    public static ProductView Offset { get; } = new("Offset", 0, true);

    public static ProductView Allowance(int vintage) => new($"Vintage {vintage}", vintage, false);
}

public sealed record HoldingView(ProductView Product, decimal Available, decimal Escrowed)
{
    public decimal Held => Available + Escrowed;
}

public sealed record FinancialView(
    decimal Capital,
    decimal Available,
    decimal Escrowed,
    decimal OverdraftLimit,
    decimal AvailableOverdraft,
    decimal NormalOperatingProfit,
    decimal AbatementNetRevenue,
    decimal NopWithAbatement,
    decimal NetRevenueThisYear,
    decimal InterestThisYear,
    decimal ForecastNetProfit);

public sealed record ComplianceView(
    decimal ThisYearCostOfCompliance,
    decimal OverallCostOfCompliance,
    decimal ThisYearMarginalCost,
    decimal OverallMarginalCost,
    decimal Emissions,
    decimal Obligation,
    decimal Covered,
    decimal Allowed,
    decimal Banked,
    decimal Forfeited,
    decimal Shortfall,
    bool HasResult);

public sealed record PositionView(
    decimal FreeAllocation,
    decimal ForecastEmissions,
    decimal Reduction,
    decimal Shortfall,
    decimal OverallShortfall);

public sealed record UnitView(
    int Id,
    string Name,
    string SectorName,
    decimal BaselineEmissions,
    decimal FreeAllocationThisYear,
    decimal ForecastEmissions,
    decimal EmissionReductions,
    decimal NormalOperatingProfit,
    bool ShutDownThisYear,
    bool AutoTrade,
    int ImplementedCount,
    IReadOnlyList<AbatementOpportunityView> Opportunities);

public sealed record AbatementOpportunityView(
    int UnitId,
    string UnitName,
    int Index,
    string Code,
    string Name,
    decimal UpfrontCost,
    decimal AnnualReduction,
    int ImplementationYears,
    int LifetimeYears,
    decimal AnnualNetRevenue,
    decimal ForecastRoi,
    decimal CostPerTonne,
    bool Implemented);

public sealed record AbatementView(
    int UnitId,
    string UnitName,
    string Code,
    string Name,
    int ImplementedIn,
    int OperatingFromYear,
    int ExpiresAfterYear,
    string Status,
    decimal AnnualReduction,
    decimal UpfrontCost);

public sealed record AuctionLotView(int Vintage, decimal Volume, bool IsForward);

public sealed record MyBidView(int Vintage, decimal Price, decimal Volume, decimal Awarded, decimal Cost);

public sealed record AuctionAwardView(int Vintage, decimal Volume, decimal Price, decimal Cost);

public sealed record AuctionView(
    int Year,
    int Sequence,
    bool IsCleared,
    decimal OfferedVolume,
    decimal UnsoldVolume,
    decimal? ClearingPrice,
    decimal VolumeSold,
    IReadOnlyList<AuctionLotView> Lots,
    IReadOnlyList<MyBidView> MyBids,
    IReadOnlyList<AuctionAwardView> MyAwards);

public sealed record OrderView(
    long Id,
    int UnitId,
    string Product,
    string Side,
    string Kind,
    decimal Volume,
    decimal Filled,
    decimal? Price,
    decimal? StopPrice,
    string Status);

public sealed record TradeRow(
    int Year,
    string Channel,
    string Product,
    decimal Price,
    decimal Volume,
    string Buyer,
    string Seller,
    bool IsMine);

public sealed record ProductBookView(
    ProductView Product,
    string Label,
    decimal? BestBid,
    decimal? BestOffer,
    decimal LastTrade,
    decimal BandFloor,
    decimal BandCeiling,
    IReadOnlyList<OrderView> OpenOrders,
    IReadOnlyList<OrderView> MyOrders,
    IReadOnlyList<TradeRow> RecentTrades,
    IReadOnlyList<YearPriceView> YearPrices);

/// <summary>One product's price range in one virtual year, for the price chart's candles.</summary>
public sealed record YearPriceView(int Year, decimal Open, decimal High, decimal Low, decimal Close, decimal Volume);

/// <summary>Where the auctions have cleared, for the exchange screen's summary strip.</summary>
public sealed record AuctionPriceSummaryView(decimal? Low, decimal? High, decimal? Average, decimal? Last, decimal? ThisYearAverage);

/// <summary>A unit another company owns, which an offer may be addressed to.</summary>
public sealed record CounterpartyView(int UnitId, string UnitName, string CompanyName, string SectorName);

public sealed record OtcOfferView(
    long Id,
    string SellerUnit,
    string BuyerUnit,
    string SellerCompany,
    string BuyerCompany,
    ProductView Product,
    decimal Price,
    decimal Volume,
    decimal Consideration,
    string State,
    bool IAmBuyer,
    bool IAmSeller);

public sealed record LeaderboardRow(
    int Rank,
    string Company,
    string Unit,
    string Player,
    decimal OverallCostOfCompliance,
    decimal MarginalCostOfCompliance,
    decimal FinalPosition,
    bool IsAutomated,
    bool IsMine);

public sealed record ChatLine(int Year, string From, string Text);

public sealed record SectorView(string Name, decimal EmissionShare, int Units, decimal FreeAllocationThisYear);

public sealed record SystemTotalsView(
    decimal ForecastEmissions,
    decimal AllowancesSurrendered,
    decimal OffsetsSurrendered,
    decimal Banked,
    decimal Forfeited,
    decimal AllowancesSold,
    decimal AllowancesBought,
    decimal OffsetsSold,
    decimal OffsetsBought,
    decimal AuctionVolume,
    decimal AuctionRevenue,
    decimal AverageAllowancePrice,
    decimal AverageOffsetPrice,
    int AbatementsImplemented,
    decimal AbatementTonnes,
    decimal EmissionsReduced,
    int PenaltyCount,
    decimal PenaltyValue,
    decimal FineValue,
    decimal Cap,
    decimal FreeAllocation,
    decimal AuctionableVolume);

public sealed record SystemInfoView(
    string Name,
    decimal Cap,
    decimal AnnualCapReductionRate,
    decimal FreeAllocationShare,
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
    IReadOnlyList<SectorView> Sectors,
    SystemTotalsView ThisYear,
    SystemTotalsView ToDate,
    decimal GovernmentRevenue,
    IReadOnlyList<HoldingView> GovernmentReserve);
