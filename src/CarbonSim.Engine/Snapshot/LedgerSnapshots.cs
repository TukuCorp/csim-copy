using CarbonSim.Engine.Finance;
using CarbonSim.Engine.Reporting;

namespace CarbonSim.Engine.Snapshot;

/// <summary>
/// What each company holds. Balances are stored per company and product, in the two buckets
/// the engine keeps them in, because a restore has to know what was free to spend and what was
/// already promised to a resting order or a trade offer.
/// </summary>
public sealed record AllowanceLedgerSnapshot(
    IReadOnlyList<LedgerBalanceSnapshot> Available,
    IReadOnlyList<LedgerBalanceSnapshot> Escrowed,
    IReadOnlyList<int> GrantedYears);

/// <summary>One balance for one company and one product, in one of the two buckets.</summary>
public sealed record LedgerBalanceSnapshot(int CompanyId, ProductSnapshot Product, decimal Volume);

/// <summary>
/// The government's side: the money it raised and the allowances of each vintage it still
/// holds. The reserve is the authoritative figure — it is what the auctions draw from and pay
/// back into — while the issued volumes record what the cap handed over in the first place.
/// </summary>
public sealed record GovernmentSnapshot(
    decimal Revenue,
    bool HasIssued,
    IReadOnlyList<VintageVolumeSnapshot> Reserve,
    IReadOnlyList<VintageVolumeSnapshot> IssuedVolumes);

/// <summary>A volume of allowances of one vintage.</summary>
public sealed record VintageVolumeSnapshot(int Vintage, decimal Volume);

/// <summary>
/// Every movement of money, in the order it happened. The balances those movements produced
/// are on <see cref="CompanySnapshot.Capital"/> and <see cref="CompanySnapshot.EscrowedCash"/>.
/// </summary>
public sealed record CashLedgerSnapshot(IReadOnlyList<CashMovementSnapshot> Movements);

/// <summary>One movement of money, signed, with the sequence number it was given.</summary>
public sealed record CashMovementSnapshot(
    long Sequence,
    int Year,
    int CompanyId,
    CashCategory Category,
    decimal Amount,
    string Description);

/// <summary>
/// Year-end reconciliation. The reconciled set is stored alongside the results because it is
/// what stops a company-year being reconciled a second time, whether or not a result was
/// written for it.
/// </summary>
public sealed record ComplianceRegisterSnapshot(
    IReadOnlyList<CompanyComplianceSnapshot> Results,
    IReadOnlyList<FineSnapshot> Fines,
    IReadOnlyList<CompanyYearSnapshot> Reconciled);

/// <summary>One company's obligations and outcomes for one compliance year.</summary>
public sealed record CompanyComplianceSnapshot(
    int Year,
    int CompanyId,
    decimal Obligation,
    decimal OffsetsSurrendered,
    decimal AllowancesSurrendered,
    decimal Banked,
    decimal Forfeited,
    decimal Shortfall,
    decimal PenaltyCash,
    decimal PenaltyAllowanceDebit);

/// <summary>A fine the administrator imposed on a unit.</summary>
public sealed record FineSnapshot(
    int Id,
    int UnitId,
    int CompanyId,
    decimal Amount,
    string Description,
    int Year);

/// <summary>A company-year the register has already reconciled.</summary>
public sealed record CompanyYearSnapshot(int CompanyId, int Year);

/// <summary>Every trade the run has seen, in the order it happened.</summary>
public sealed record MarketJournalSnapshot(IReadOnlyList<MarketTradeSnapshot> Trades);

/// <summary>
/// One trade on any channel. The buyer and seller are companies; a null seller means the
/// government, which is how an auction sale is recorded. Nothing here points at the orders
/// that produced the trade, so a journal survives those orders being cancelled or filled.
/// </summary>
public sealed record MarketTradeSnapshot(
    long Sequence,
    int Year,
    TradeChannel Channel,
    ProductSnapshot Product,
    decimal Price,
    decimal Volume,
    int BuyerCompanyId,
    int? SellerCompanyId);
