using CarbonSim.Engine.Market;
using CarbonSim.Engine.Market.Exchange;
using CarbonSim.Engine.Market.Otc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CarbonSim.Data.Persistence;

/// <summary>The exchange's own bookkeeping: the last order number it handed out.</summary>
public sealed class ExchangeRecord
{
    public Guid SimulationId { get; set; }

    /// <summary>Stored so a restored exchange does not re-issue an id a cancelled order used.</summary>
    public long LastOrderId { get; set; }
}

/// <summary>
/// One product's order book. A book exists from the first time the product is quoted, and its
/// last trade price is what the volatility band is anchored to, so an empty book is still state.
/// </summary>
public sealed class OrderBookRecord
{
    public Guid SimulationId { get; set; }

    public ProductKind ProductKind { get; set; }

    public int Vintage { get; set; }

    public decimal LastTradePrice { get; set; }
}

/// <summary>An order as the book holds it, with what it still has set aside.</summary>
public sealed class OrderRecord
{
    public Guid SimulationId { get; set; }

    public long OrderId { get; set; }

    public int UnitId { get; set; }

    /// <summary>The company that owns the unit, carried so an order joins straight to its books.</summary>
    public int CompanyId { get; set; }

    public ProductKind ProductKind { get; set; }

    public int Vintage { get; set; }

    public OrderSide Side { get; set; }

    public OrderKind Kind { get; set; }

    public FillPolicy FillPolicy { get; set; }

    public decimal Volume { get; set; }

    public decimal? Price { get; set; }

    public decimal? StopPrice { get; set; }

    public decimal FilledVolume { get; set; }

    public OrderStatus Status { get; set; }

    public decimal EscrowedCash { get; set; }
}

/// <summary>One match inside a book, numbered from 1 within that book.</summary>
public sealed class BookTradeRecord
{
    public Guid SimulationId { get; set; }

    public ProductKind ProductKind { get; set; }

    public int Vintage { get; set; }

    public long Sequence { get; set; }

    public long BuyOrderId { get; set; }

    public long SellOrderId { get; set; }

    public decimal Price { get; set; }

    public decimal Volume { get; set; }
}

/// <summary>One sell-side offer and where it ended up.</summary>
public sealed class OtcOfferRecord
{
    public Guid SimulationId { get; set; }

    public long OfferId { get; set; }

    public int SellerUnitId { get; set; }

    public int BuyerUnitId { get; set; }

    public ProductKind ProductKind { get; set; }

    public int Vintage { get; set; }

    public decimal Price { get; set; }

    public decimal Volume { get; set; }

    public OtcOfferState State { get; set; }

    /// <summary>The virtual year the offer was sent in.</summary>
    public int Year { get; set; }
}

/// <summary>One auction: what it offered, and what clearing did to it.</summary>
public sealed class AuctionRecord
{
    public Guid SimulationId { get; set; }

    public int Year { get; set; }

    /// <summary>The auction's position inside its year, counting from 1.</summary>
    public int Sequence { get; set; }

    public bool IsCleared { get; set; }

    public decimal UnsoldVolume { get; set; }
}

/// <summary>One vintage's slice of an auction, and whether it was a forward sale.</summary>
public sealed class AuctionLotRecord
{
    public Guid SimulationId { get; set; }

    public int Year { get; set; }

    public int Sequence { get; set; }

    public int Vintage { get; set; }

    public decimal Volume { get; set; }

    public bool IsForward { get; set; }
}

/// <summary>A sealed bid, and whether clearing served it.</summary>
public sealed class AuctionBidRecord
{
    public Guid SimulationId { get; set; }

    public int Year { get; set; }

    public int Sequence { get; set; }

    public int BidId { get; set; }

    public int UnitId { get; set; }

    /// <summary>The company that owns the bidding unit.</summary>
    public int CompanyId { get; set; }

    public int Vintage { get; set; }

    public decimal Price { get; set; }

    public decimal Volume { get; set; }

    public bool Won { get; set; }
}

/// <summary>What one vintage of an auction did: the uniform price it cleared at, if any.</summary>
public sealed class AuctionResultRecord
{
    public Guid SimulationId { get; set; }

    public int Year { get; set; }

    public int Sequence { get; set; }

    public int Vintage { get; set; }

    public decimal OfferedVolume { get; set; }

    /// <summary>Null when nothing was served, so no uniform price exists.</summary>
    public decimal? ClearingPrice { get; set; }
}

/// <summary>How much of a bid was served, and what it cost at the clearing price.</summary>
public sealed class AuctionAwardRecord
{
    public Guid SimulationId { get; set; }

    public int Year { get; set; }

    public int Sequence { get; set; }

    public int Vintage { get; set; }

    public int BidId { get; set; }

    public decimal Volume { get; set; }

    public decimal Cost { get; set; }
}

/// <summary>A bid that missed out when its auction cleared.</summary>
public sealed class AuctionRejectedBidRecord
{
    public Guid SimulationId { get; set; }

    public int Year { get; set; }

    public int Sequence { get; set; }

    public int Vintage { get; set; }

    public int BidId { get; set; }
}

internal sealed class ExchangeRecordConfiguration : IEntityTypeConfiguration<ExchangeRecord>
{
    public void Configure(EntityTypeBuilder<ExchangeRecord> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("Exchanges");
        builder.HasKey(exchange => exchange.SimulationId);

        builder
            .HasOne<SimulationRecord>()
            .WithMany()
            .HasForeignKey(exchange => exchange.SimulationId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class OrderBookRecordConfiguration : IEntityTypeConfiguration<OrderBookRecord>
{
    public void Configure(EntityTypeBuilder<OrderBookRecord> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("OrderBooks");
        builder.HasKey(book => new { book.SimulationId, book.ProductKind, book.Vintage });

        builder
            .HasOne<ExchangeRecord>()
            .WithMany()
            .HasForeignKey(book => book.SimulationId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class OrderRecordConfiguration : IEntityTypeConfiguration<OrderRecord>
{
    public void Configure(EntityTypeBuilder<OrderRecord> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("Orders");
        builder.HasKey(order => new { order.SimulationId, order.OrderId });

        builder
            .HasOne<UnitRecord>()
            .WithMany()
            .HasForeignKey(order => new { order.SimulationId, order.UnitId })
            .OnDelete(DeleteBehavior.Cascade);

        builder
            .HasOne<CompanyRecord>()
            .WithMany()
            .HasForeignKey(order => new { order.SimulationId, order.CompanyId })
            .OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class BookTradeRecordConfiguration : IEntityTypeConfiguration<BookTradeRecord>
{
    public void Configure(EntityTypeBuilder<BookTradeRecord> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("BookTrades");
        builder.HasKey(trade => new { trade.SimulationId, trade.ProductKind, trade.Vintage, trade.Sequence });

        builder
            .HasOne<OrderBookRecord>()
            .WithMany()
            .HasForeignKey(trade => new { trade.SimulationId, trade.ProductKind, trade.Vintage })
            .OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class OtcOfferRecordConfiguration : IEntityTypeConfiguration<OtcOfferRecord>
{
    public void Configure(EntityTypeBuilder<OtcOfferRecord> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("OtcOffers");
        builder.HasKey(offer => new { offer.SimulationId, offer.OfferId });

        builder
            .HasOne<SimulationRecord>()
            .WithMany()
            .HasForeignKey(offer => offer.SimulationId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class AuctionRecordConfiguration : IEntityTypeConfiguration<AuctionRecord>
{
    public void Configure(EntityTypeBuilder<AuctionRecord> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("Auctions");
        builder.HasKey(auction => new { auction.SimulationId, auction.Year, auction.Sequence });

        builder
            .HasOne<SimulationRecord>()
            .WithMany()
            .HasForeignKey(auction => auction.SimulationId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class AuctionLotRecordConfiguration : IEntityTypeConfiguration<AuctionLotRecord>
{
    public void Configure(EntityTypeBuilder<AuctionLotRecord> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("AuctionLots");
        builder.HasKey(lot => new { lot.SimulationId, lot.Year, lot.Sequence, lot.Vintage });

        builder
            .HasOne<AuctionRecord>()
            .WithMany()
            .HasForeignKey(lot => new { lot.SimulationId, lot.Year, lot.Sequence })
            .OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class AuctionBidRecordConfiguration : IEntityTypeConfiguration<AuctionBidRecord>
{
    public void Configure(EntityTypeBuilder<AuctionBidRecord> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("AuctionBids");
        builder.HasKey(bid => new { bid.SimulationId, bid.Year, bid.Sequence, bid.BidId });

        builder
            .HasOne<AuctionRecord>()
            .WithMany()
            .HasForeignKey(bid => new { bid.SimulationId, bid.Year, bid.Sequence })
            .OnDelete(DeleteBehavior.Cascade);

        builder
            .HasOne<CompanyRecord>()
            .WithMany()
            .HasForeignKey(bid => new { bid.SimulationId, bid.CompanyId })
            .OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class AuctionResultRecordConfiguration : IEntityTypeConfiguration<AuctionResultRecord>
{
    public void Configure(EntityTypeBuilder<AuctionResultRecord> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("AuctionResults");
        builder.HasKey(result => new { result.SimulationId, result.Year, result.Sequence, result.Vintage });

        builder
            .HasOne<AuctionRecord>()
            .WithMany()
            .HasForeignKey(result => new { result.SimulationId, result.Year, result.Sequence })
            .OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class AuctionAwardRecordConfiguration : IEntityTypeConfiguration<AuctionAwardRecord>
{
    public void Configure(EntityTypeBuilder<AuctionAwardRecord> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("AuctionAwards");
        builder.HasKey(award => new { award.SimulationId, award.Year, award.Sequence, award.Vintage, award.BidId });

        builder
            .HasOne<AuctionResultRecord>()
            .WithMany()
            .HasForeignKey(award => new { award.SimulationId, award.Year, award.Sequence, award.Vintage })
            .OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class AuctionRejectedBidRecordConfiguration : IEntityTypeConfiguration<AuctionRejectedBidRecord>
{
    public void Configure(EntityTypeBuilder<AuctionRejectedBidRecord> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("AuctionRejectedBids");
        builder.HasKey(rejected => new { rejected.SimulationId, rejected.Year, rejected.Sequence, rejected.Vintage, rejected.BidId });

        builder
            .HasOne<AuctionResultRecord>()
            .WithMany()
            .HasForeignKey(rejected => new { rejected.SimulationId, rejected.Year, rejected.Sequence, rejected.Vintage })
            .OnDelete(DeleteBehavior.Cascade);
    }
}
