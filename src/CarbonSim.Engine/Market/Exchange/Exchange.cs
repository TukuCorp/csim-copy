using CarbonSim.Engine.Domain;

namespace CarbonSim.Engine.Market.Exchange;

/// <summary>
/// The secondary market: one order book per product (each allowance vintage, and offsets).
/// The first trade of a book is anchored on the auction price floor, so a book starts life
/// with a volatility band around it.
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

    /// <summary>The book for a product, created the first time it is needed.</summary>
    public OrderBook Book(Product product)
    {
        if (!_books.TryGetValue(product, out OrderBook? book))
        {
            book = new OrderBook(_simulation, product, _simulation.TradingSystems.Single().Parameters.AuctionFloorPrice, _ids);
            _books[product] = book;
        }

        return book;
    }

    /// <summary>Places an order on the right book for its product.</summary>
    public Order Place(OrderRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

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

    /// <summary>Money a company could spend, for callers working out what they can afford.</summary>
    public decimal CashOf(Company company) => _simulation.Cash.Available(company) + company.OverdraftLimit;

    /// <summary>Instruments a company could offer, for callers working out what they can sell.</summary>
    public decimal HoldingsOf(Company company, Product product) => _simulation.Ledger.Available(company, product);
}
