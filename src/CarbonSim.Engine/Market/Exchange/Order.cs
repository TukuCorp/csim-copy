using CarbonSim.Engine.Domain;

namespace CarbonSim.Engine.Market.Exchange;

/// <summary>An order as a player places it, before the book decides what to do with it.</summary>
public sealed record OrderRequest(
    Unit Unit,
    Product Product,
    OrderSide Side,
    OrderKind Kind,
    decimal Volume,
    decimal? Price = null,
    decimal? StopPrice = null,
    FillPolicy FillPolicy = FillPolicy.AllowPartial);

/// <summary>An order the book has accepted, with what has been done with it so far.</summary>
public sealed class Order
{
    internal Order(
        long id,
        Unit unit,
        Product product,
        OrderSide side,
        OrderKind kind,
        FillPolicy fillPolicy,
        decimal volume,
        decimal? price,
        decimal? stopPrice)
    {
        Id = id;
        Unit = unit;
        Product = product;
        Side = side;
        Kind = kind;
        FillPolicy = fillPolicy;
        Volume = volume;
        Price = price;
        StopPrice = stopPrice;
        Status = OrderStatus.Open;
    }

    public long Id { get; }

    public Unit Unit { get; }

    public Company Company => Unit.Company;

    public Product Product { get; }

    public OrderSide Side { get; }

    public OrderKind Kind { get; }

    public FillPolicy FillPolicy { get; }

    /// <summary>The limit price; null for market and stop orders.</summary>
    public decimal? Price { get; }

    /// <summary>The price a stop order waits for; null for other kinds.</summary>
    public decimal? StopPrice { get; }

    public decimal Volume { get; }

    public decimal FilledVolume { get; internal set; }

    public decimal Remaining => Volume - FilledVolume;

    public OrderStatus Status { get; internal set; }

    /// <summary>Cash set aside for this order; the buyer's until the order is done or cancelled.</summary>
    internal decimal EscrowedCash { get; set; }

    internal void RecordFill(decimal volume)
    {
        FilledVolume += volume;
        Status = Remaining <= 0m ? OrderStatus.Filled : OrderStatus.PartiallyFilled;
    }

    public override string ToString() => $"{Side} {Volume} {Product} @ {Price ?? StopPrice} ({Status})";
}

/// <summary>A completed match between a resting order and an incoming one.</summary>
public sealed record Trade(
    long Sequence,
    Product Product,
    long BuyOrderId,
    long SellOrderId,
    decimal Price,
    decimal Volume);
