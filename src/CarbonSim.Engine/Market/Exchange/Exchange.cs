using CarbonSim.Engine.Domain;
using CarbonSim.Engine.Reporting;
using CarbonSim.Engine.Snapshot;

namespace CarbonSim.Engine.Market.Exchange;

/// <summary>
/// The secondary market: one order book per product (each allowance vintage, and offsets).
/// A new book is anchored on the product's last trade on any channel - a vintage the auction has
/// sold opens around its clearing price - and on the auction floor while the product has never
/// traded, so a book starts life with a volatility band around it.
/// </summary>
public sealed class Exchange
{
    private readonly Simulation _simulation;
    private readonly Dictionary<Product, OrderBook> _books = [];
    private readonly OrderIds _ids = new();

    public Exchange(Simulation simulation)
    {
        ArgumentNullException.ThrowIfNull(simulation);

        _simulation = simulation;
    }

    public IReadOnlyCollection<Product> Products => _books.Keys;

    /// <summary>
    /// The last order number this exchange handed out. Restoring it matters even though
    /// cancelled orders are long gone from the books: numbering that started again would reuse
    /// ids, and cancelling by id would then be ambiguous.
    /// </summary>
    internal long LastOrderId => _ids.Last;

    /// <summary>
    /// Rebuilds the books a snapshot was taken with, in the order they are listed. Books that
    /// never traded are rebuilt too: an empty book still carries the price its band is anchored
    /// to, so dropping it would change what the next order may be priced at.
    /// </summary>
    internal void Restore(ExchangeSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        _books.Clear();

        foreach (OrderBookSnapshot bookSnapshot in snapshot.Books)
        {
            OrderBook book = new(
                _simulation,
                bookSnapshot.Product.ToProduct(),
                _simulation.Rules.AuctionFloorPrice,
                _ids);

            book.Restore(bookSnapshot);
            _books[book.Product] = book;
        }

        _ids.Restore(snapshot.LastOrderId);
    }

    /// <summary>The book for a product, created the first time it is needed.</summary>
    public OrderBook Book(Product product)
    {
        if (!_books.TryGetValue(product, out OrderBook? book))
        {
            book = new OrderBook(_simulation, product, OpeningReference(product), _ids);
            _books[product] = book;
        }

        return book;
    }

    /// <summary>
    /// The volatility band a product trades in: around the price its book last traded at, or
    /// around where a new book would open for a product the exchange has not seen yet. The rule
    /// lives here so a screen showing the band and the book refusing a price outside it read the same one.
    /// </summary>
    public (decimal Floor, decimal Ceiling) BandFor(Product product)
    {
        decimal reference = _books.TryGetValue(product, out OrderBook? book)
            ? book.LastTradePrice
            : OpeningReference(product);
        decimal band = _simulation.Rules.VolatilityBand;

        return (reference * (1m - band), reference * (1m + band));
    }

    /// <summary>
    /// Where a new book opens: the product's last trade on any channel (FAQ #16 bands an order on
    /// "the last trade for the particular product", and an auction sale is one), else the floor.
    /// </summary>
    private decimal OpeningReference(Product product)
    {
        MarketTrade? last = _simulation.Journal.Trades.LastOrDefault(trade => trade.Product == product);

        return last?.Price ?? _simulation.Rules.AuctionFloorPrice;
    }

    /// <summary>Places an order on the right book for its product.</summary>
    public Order Place(OrderRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        // The book only takes orders while the year is running. Cancelling stays open in any state,
        // so a player can always take an order back.
        TradingWindow.RequireTrading(_simulation);

        if (!_simulation.Rules.ExchangeAccess)
        {
            throw new TradeRefusedException(TradeRefusal.MarketClosed, "The exchange is not open in this exercise.");
        }

        return Book(request.Product).Submit(request);
    }

    /// <summary>Withdraws an order from whichever book is holding it.</summary>
    public Order Cancel(Company company, long orderId)
    {
        ArgumentNullException.ThrowIfNull(company);

        foreach (OrderBook book in _books.Values)
        {
            if (book.OpenOrders.Any(order => order.Id == orderId))
            {
                return book.Cancel(company, orderId);
            }
        }

        throw new KeyNotFoundException($"This exchange has no open order {orderId}.");
    }

    /// <summary>
    /// Withdraws every order still working on any book, handing back what each had set aside. A
    /// halt does this: nothing may stay escrowed once trading has stopped, or the year end would
    /// reconcile a company on less than it holds.
    /// </summary>
    public IReadOnlyList<Order> CancelAll()
    {
        List<Order> cancelled = [];

        foreach (OrderBook book in _books.Values)
        {
            foreach (Order order in book.OpenOrders)
            {
                cancelled.Add(book.Cancel(order.Company, order.Id));
            }
        }

        return cancelled;
    }

    /// <summary>Money a company could spend, for callers working out what they can afford.</summary>
    public decimal CashOf(Company company) => _simulation.Cash.Available(company) + company.OverdraftLimit;

    /// <summary>Instruments a company could offer, for callers working out what they can sell.</summary>
    public decimal HoldingsOf(Company company, Product product) => _simulation.Ledger.Available(company, product);
}
