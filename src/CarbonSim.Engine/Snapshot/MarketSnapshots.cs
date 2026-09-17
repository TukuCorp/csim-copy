using CarbonSim.Engine.Market.Exchange;
using CarbonSim.Engine.Market.Otc;

namespace CarbonSim.Engine.Snapshot;

/// <summary>
/// The order books. A book exists from the first time a product is quoted, so an empty book is
/// state worth keeping: it carries the last price the band is anchored to. The last order
/// number handed out is stored too, or a restored exchange would start numbering again and
/// hand out ids that a cancelled order already used.
/// </summary>
public sealed record ExchangeSnapshot(IReadOnlyList<OrderBookSnapshot> Books, long LastOrderId);

/// <summary>
/// One product's book: the price it last traded at, the orders still working on it, and its
/// trade tape. The reference price the band was anchored to is not stored — it comes from the
/// trading system's auction floor, which the snapshot already carries.
/// </summary>
public sealed record OrderBookSnapshot(
    ProductSnapshot Product,
    decimal LastTradePrice,
    IReadOnlyList<OrderSnapshot> RestingOrders,
    IReadOnlyList<BookTradeSnapshot> Trades);

/// <summary>
/// An order as the book holds it, with how much of it has been served and what it still has
/// set aside. Resting orders are the ones that are still working; the rest were filled or
/// cancelled and are only visible in the trade tape.
/// </summary>
public sealed record OrderSnapshot(
    long Id,
    int UnitId,
    int CompanyId,
    ProductSnapshot Product,
    OrderSide Side,
    OrderKind Kind,
    FillPolicy FillPolicy,
    decimal Volume,
    decimal? Price,
    decimal? StopPrice,
    decimal FilledVolume,
    OrderStatus Status,
    decimal EscrowedCash);

/// <summary>One match inside a book, numbered from 1 within that book.</summary>
public sealed record BookTradeSnapshot(
    long Sequence,
    ProductSnapshot Product,
    long BuyOrderId,
    long SellOrderId,
    decimal Price,
    decimal Volume);

/// <summary>Every offer ever made on the over-the-counter market, in the order it was made.</summary>
public sealed record OtcMarketSnapshot(IReadOnlyList<OtcOfferSnapshot> Offers);

/// <summary>One sell-side offer and where it ended up.</summary>
public sealed record OtcOfferSnapshot(
    long Id,
    int SellerUnitId,
    int BuyerUnitId,
    ProductSnapshot Product,
    decimal Price,
    decimal Volume,
    OtcOfferState State,
    int Year);

/// <summary>The whole run's auctions, in year and section order.</summary>
public sealed record AuctionScheduleSnapshot(IReadOnlyList<AuctionSnapshot> Auctions);

/// <summary>
/// One auction: what it offered, what was bid, and what clearing did to it. A cleared auction
/// keeps its results here, which is the only place the clearing prices survive — the bids
/// alone cannot say what the uniform price came out at.
/// </summary>
public sealed record AuctionSnapshot(
    int Year,
    int Sequence,
    IReadOnlyList<AuctionLotSnapshot> Lots,
    bool IsCleared,
    decimal UnsoldVolume,
    IReadOnlyList<AuctionBidSnapshot> Bids,
    IReadOnlyList<AuctionResultSnapshot> Results);

/// <summary>One vintage's slice of an auction, and whether it was a forward sale.</summary>
public sealed record AuctionLotSnapshot(int Vintage, decimal Volume, bool IsForward);

/// <summary>A sealed bid. The flag records whether it was served when the auction cleared; the
/// volume and cost it was served at are on the result it appears in.</summary>
public sealed record AuctionBidSnapshot(
    int Id,
    int UnitId,
    int CompanyId,
    int Vintage,
    decimal Price,
    decimal Volume,
    bool Won);

/// <summary>What one vintage of an auction did: the uniform price, who was served, and who missed out.</summary>
public sealed record AuctionResultSnapshot(
    int Vintage,
    decimal OfferedVolume,
    decimal? ClearingPrice,
    IReadOnlyList<AuctionAwardSnapshot> Awards,
    IReadOnlyList<int> RejectedBidIds);

/// <summary>How much of a bid was served, and what it cost at the clearing price.</summary>
public sealed record AuctionAwardSnapshot(int BidId, decimal Volume, decimal Cost);
