using CarbonSim.Engine.Reporting;
using CarbonSim.Web.Components.Shared;

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
    bool MessagingEnabled)
{
    /// <summary>
    /// The auction the run cleared most recently. A section's result is worth reading after the
    /// clock has moved on to the next one, so the screen shows this rather than the current
    /// section's auction, which is only cleared for the moment between its close and the next
    /// section opening.
    /// </summary>
    public AuctionView? LastClearedAuction => Auctions.LastOrDefault(auction => auction.IsCleared);

    /// <summary>
    /// Whether there is any time left to count down. A year that has been closed, or that ran out,
    /// has none: what is left is the settlement, not a countdown to the next auction.
    /// </summary>
    public bool YearIsOver => State is "YearEnded" or "GameEnded" || TimeLeftInYear <= TimeSpan.Zero;

    /// <summary>How long until the trainer's warned halt stops trading, or null when none is pending.</summary>
    public TimeSpan? TimeToHalt { get; init; }

    /// <summary>The company's end-of-year report for each year that has started.</summary>
    public IReadOnlyList<CarbonSim.Engine.Finance.CompanyYearReport> CompanyReports { get; init; } = [];

    /// <summary>The board for each year that has started, beside the whole-run board in <see cref="Leaderboard"/>.</summary>
    public IReadOnlyList<LeaderboardYearView> LeaderboardYears { get; init; } = [];

    /// <summary>What the year end would surrender now, or null before the first year and once it is settled.</summary>
    public SurrenderView? Surrender { get; init; }
}

/// <summary>
/// The engine's surrender plan for the live year, as the Surrender and Banking screen reads it: what
/// the company can surrender, what it will surrender, and the penalty that would follow.
/// </summary>
public sealed record SurrenderView(
    bool Manual,
    bool HasInstruction,
    decimal Obligation,
    decimal AllowancesAvailable,
    decimal OffsetsAvailable,
    decimal OffsetLimit,
    decimal Allowances,
    decimal Offsets,
    decimal ResultingPosition,
    decimal Shortfall,
    decimal EstimatedPenalty,
    bool UnlimitedBanking);

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

/// <summary>
/// Who is at the front door of the host, and where they go from there: a trainer to the console,
/// a player to the run their company plays in, and a guest to the sign-in page.
/// </summary>
public sealed record FrontDoorView(string? AccountName, string? CompanyName, bool IsAdministrator, Guid? RunId)
{
    /// <summary>Nobody is signed in, so the front door offers the way in and nothing else.</summary>
    public static FrontDoorView Guest { get; } = new(null, null, IsAdministrator: false, null);
}

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

/// <summary>
/// The company's position for the year: what it was granted free, everything it holds that could
/// be surrendered against the year, what it forecasts emitting, and the gap between them.
/// </summary>
public sealed record PositionView(
    decimal FreeAllocation,
    decimal Held,
    decimal ForecastEmissions,
    decimal Reduction,
    decimal Shortfall,
    bool Settled)
{
    /// <summary>
    /// The position the screens read: what the company holds less what it forecasts emitting, so
    /// long is positive, the way the engine's Standing has it. Shortfall is the same gap the other
    /// way up and never goes below zero, so a covered year owes nothing.
    /// </summary>
    public decimal Position => Held - ForecastEmissions;
}

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
    decimal ImplementationYears,
    int LifetimeYears,
    decimal AnnualNetRevenue,
    decimal ForecastRoi,
    decimal CostPerTonne,
    bool Implemented)
{
    /// <summary>Tonnes it would remove before the run ends if bought now (the "Simulation" view).</summary>
    public decimal SimulationTonnes { get; init; }

    /// <summary>Cost per tonne over what is left of the run; null when it would remove nothing in time.</summary>
    public decimal? SimulationCostPerTonne { get; init; }

    /// <summary>Return over what is left of the run; -100% when it would operate for none of it.</summary>
    public decimal? SimulationRoi { get; init; }
}

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

/// <summary>One of the company's bids in an auction; the id is what cancelling it names.</summary>
public sealed record MyBidView(int Vintage, decimal Price, decimal Volume, decimal Awarded, decimal Cost, int Id = 0);

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
    IReadOnlyList<AuctionAwardView> MyAwards)
{
    /// <summary>How many bids the auction drew; kept back until it has cleared, because the bids are sealed.</summary>
    public int? BidCount { get; init; }
}

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
    IReadOnlyList<BookLevelView> BidLevels,
    IReadOnlyList<BookLevelView> AskLevels,
    IReadOnlyList<OrderView> MyOrders,
    IReadOnlyList<TradeRow> RecentTrades,
    IReadOnlyList<PeriodPriceView> Prices)
{
    /// <summary>The price of the product's latest OTC deal, or null before it has had one.</summary>
    public decimal? OtcLastTrade { get; init; }

    /// <summary>The price of the product's latest deal in either market, exchange or OTC.</summary>
    public decimal? MarketLastTrade { get; init; }
}

/// <summary>One price level of the book: what is on offer at that price, added up.</summary>
public sealed record BookLevelView(decimal Price, decimal Volume);

/// <summary>The best price levels of one product's book, each side best-first.</summary>
public sealed record BookLevelsView(IReadOnlyList<BookLevelView> Bids, IReadOnlyList<BookLevelView> Asks);

/// <summary>One product's price range in one virtual month of a year, for the price chart's candles.</summary>
public sealed record PeriodPriceView(int Year, int Month, decimal Open, decimal High, decimal Low, decimal Close, decimal Volume);

/// <summary>Where the auctions have cleared, for the exchange screen's summary strip.</summary>
public sealed record AuctionPriceSummaryView(decimal? Low, decimal? High, decimal? Average, decimal? Last, decimal? ThisYearAverage);

/// <summary>A unit another company owns, which an offer may be addressed to.</summary>
public sealed record CounterpartyView(int UnitId, string UnitName, string CompanyName, string SectorName, bool Automated)
{
    /// <summary>How the offer form names this counterparty: the unit once, and "(AI)" for a bot.</summary>
    public string Label => Display.Unit(UnitName, CompanyName, Automated);
}

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
    bool IsMine)
{
    /// <summary>The unit and whoever plays it, named once: "(AI)" marks a bot.</summary>
    public string UnitLabel => Display.Unit(Unit, Player, IsAutomated);

    /// <summary>The company's sector, for the board's sector filter.</summary>
    public string Sector { get; init; } = string.Empty;

    /// <summary>The cash penalties the company paid over the board's period.</summary>
    public decimal Penalties { get; init; }
}

/// <summary>The leaderboard for one year of the run (catalog `LeaderboardYear_X`).</summary>
public sealed record LeaderboardYearView(int Year, IReadOnlyList<LeaderboardRow> Rows);

public sealed record ChatLine(int Year, string From, string Text);

public sealed record SectorView(string Name, decimal EmissionShare, int Units, decimal FreeAllocationThisYear);

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
    decimal GovernmentReserveToAuctionPercent,
    decimal ForwardVintageShare,
    IReadOnlyList<SectorView> Sectors,
    SystemTotals ThisYear,
    SystemTotals ToDate,
    decimal GovernmentRevenue,
    IReadOnlyList<HoldingView> GovernmentReserve)
{
    /// <summary>Whether the exchange is open to players in this exercise.</summary>
    public bool ExchangeAccess { get; init; } = true;

    /// <summary>Whether the OTC market is open to players in this exercise.</summary>
    public bool OtcAccess { get; init; } = true;

    /// <summary>Whether players may see the leaderboard in this exercise.</summary>
    public bool LeaderboardVisible { get; init; } = true;

    /// <summary>Whether players may put their own units on AutoTrade in this exercise.</summary>
    public bool AiTradeAssistance { get; init; }
}
