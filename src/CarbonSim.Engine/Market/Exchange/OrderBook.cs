using CarbonSim.Engine.Domain;
using CarbonSim.Engine.Finance;
using CarbonSim.Engine.Reporting;

namespace CarbonSim.Engine.Market.Exchange;

/// <summary>
/// Hands out order numbers for a whole exchange. Ids have to be unique across products, not
/// only within a book, or cancelling by id would be ambiguous once several books are in use.
/// </summary>
internal sealed class OrderIds
{
    private long _last;

    public long Next() => ++_last;
}

/// <summary>
/// The continuous order book for one product. Orders are served on price and then on arrival,
/// trades happen at the resting order's price, and nothing can trade outside the volatility
/// band around the last trade: an offer that the market has moved away from simply sits there
/// until it is cancelled or the band comes back to it. Selling escrows the allowances, so the
/// same tonnes cannot be promised twice.
/// </summary>
public sealed class OrderBook
{
    private readonly Simulation _simulation;
    private readonly Parameters _parameters;
    private readonly OrderIds _ids;
    private readonly List<Order> _resting = [];
    private readonly List<Trade> _trades = [];

    internal OrderBook(Simulation simulation, Product product, decimal referencePrice, OrderIds ids)
    {
        _simulation = simulation;
        _parameters = simulation.TradingSystems.Single().Parameters;
        _ids = ids;
        Product = product;
        ReferencePrice = referencePrice;
        LastTradePrice = referencePrice;
    }

    public Product Product { get; }

    /// <summary>The price the band was anchored to before anything traded (the auction floor).</summary>
    public decimal ReferencePrice { get; }

    public decimal VolatilityBand => _parameters.VolatilityBand;

    /// <summary>The price of the last trade, or the reference price while nothing has traded.</summary>
    public decimal LastTradePrice { get; private set; }

    public bool HasTraded => _trades.Count > 0;

    public IReadOnlyList<Trade> Trades => _trades;

    public decimal BandFloor => LastTradePrice * (1m - VolatilityBand);

    public decimal BandCeiling => LastTradePrice * (1m + VolatilityBand);

    public decimal? BestBid => _resting.Where(order => order.Side == OrderSide.Buy).Select(order => order.Price).Max();

    public decimal? BestOffer => _resting.Where(order => order.Side == OrderSide.Sell).Select(order => order.Price).Min();

    /// <summary>Resting orders in the order they would be served: buys high to low, then sells low to high.</summary>
    public IReadOnlyList<Order> OpenOrders =>
    [
        .. _resting
            .Where(order => order.Kind != OrderKind.StopLoss)
            .OrderBy(order => order.Side == OrderSide.Buy ? 0 : 1)
            .ThenBy(order => order.Side == OrderSide.Buy ? -order.Price!.Value : order.Price!.Value)
            .ThenBy(order => order.Id),
        .. _resting.Where(order => order.Kind == OrderKind.StopLoss).OrderBy(order => order.Id),
    ];

    /// <summary>Places an order: escrowing what it offers, matching what it can, resting the rest.</summary>
    public Order Submit(OrderRequest request)
    {
        Validate(request);

        Unit unit = request.Unit;
        Order order = new(
            _ids.Next(),
            unit,
            request.Product,
            request.Side,
            request.Kind,
            request.FillPolicy,
            request.Volume,
            request.Price,
            request.StopPrice);

        if (request.Side == OrderSide.Sell)
        {
            _simulation.Ledger.Escrow(unit.Company, request.Product, request.Volume);
        }
        else
        {
            // A buy promises money. A market order can trade anywhere inside the band, so it
            // sets aside the top of the band; whatever is not spent comes back.
            decimal ceiling = request.Price ?? BandCeiling;
            order.EscrowedCash = ceiling * request.Volume;
            _simulation.Cash.Escrow(unit.Company, order.EscrowedCash);
        }

        switch (request.Kind)
        {
            case OrderKind.StopLoss:
                _resting.Add(order);
                break;

            case OrderKind.Market:
                if (request.FillPolicy == FillPolicy.FillOrKill && Fillable(order) < order.Volume)
                {
                    Reject(order);
                    break;
                }

                Match(order, limit: null);
                CancelRemainder(order);

                if (order.Remaining <= 0m)
                {
                    ReleaseUnusedCash(order);
                }

                break;

            default:
                if (request.FillPolicy == FillPolicy.FillOrKill && Fillable(order) < order.Volume)
                {
                    Reject(order);
                    break;
                }

                Match(order, order.Price);

                if (request.FillPolicy == FillPolicy.FillOrKill)
                {
                    CancelRemainder(order);
                }
                else if (order.Remaining > 0m && order.Status != OrderStatus.Cancelled)
                {
                    // It is working on the book, so it keeps its money set aside for the fill.
                    _resting.Add(order);
                }
                else
                {
                    ReleaseUnusedCash(order);
                }

                break;
        }

        // Stops are checked once the order in hand has finished, so a trigger can never take
        // liquidity away from the order being matched.
        CheckStops();

        return order;
    }

    /// <summary>Withdraws a resting order and hands back anything it had escrowed.</summary>
    public Order Cancel(Company company, long orderId)
    {
        ArgumentNullException.ThrowIfNull(company);

        Order order = _resting.FirstOrDefault(candidate => candidate.Id == orderId)
            ?? throw new KeyNotFoundException($"This book has no open order {orderId}.");

        if (!ReferenceEquals(order.Company, company))
        {
            throw new InvalidOperationException($"Order {orderId} belongs to '{order.Company.Name}', not to '{company.Name}'.");
        }

        _resting.Remove(order);
        Reject(order);

        return order;
    }

    private void Validate(OrderRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Unit);

        if (!ReferenceEquals(request.Unit.Company.Units.FirstOrDefault(unit => ReferenceEquals(unit, request.Unit)), request.Unit))
        {
            throw new ArgumentException($"Unit '{request.Unit.Name}' is not part of this simulation.", nameof(request));
        }

        if (request.Volume <= 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(request), request.Volume, "An order volume must be greater than zero.");
        }

        if (request.Kind == OrderKind.Limit && request.Price is null)
        {
            throw new ArgumentException("A limit order needs a price.", nameof(request));
        }

        if (request.Kind != OrderKind.Limit && request.Price is not null)
        {
            throw new ArgumentException($"A {request.Kind} order cannot carry a price.", nameof(request));
        }

        if (request.Kind == OrderKind.StopLoss && request.StopPrice is null)
        {
            throw new ArgumentException("A stop order needs a stop price.", nameof(request));
        }

        if (request.Kind != OrderKind.StopLoss && request.StopPrice is not null)
        {
            throw new ArgumentException($"A {request.Kind} order cannot carry a stop price.", nameof(request));
        }

        if (request.Price is { } price && (price < BandFloor || price > BandCeiling))
        {
            throw new ArgumentOutOfRangeException(
                nameof(request),
                price,
                $"A price of {price} is outside the volatility band {BandFloor} to {BandCeiling} around the last trade of {LastTradePrice}.");
        }
    }

    /// <summary>How much of an order the book could serve right now, without touching anything.</summary>
    private decimal Fillable(Order order) => Plan(order, order.Kind == OrderKind.Limit ? order.Price : null)
        .Sum(leg => leg.Volume);

    /// <summary>
    /// Works out what an order would take from the book before anything is settled, walking the
    /// opposite side on price and then arrival and following the volatility band as each leg
    /// moves the last price. Nothing can change between planning and executing, so an all-or-
    /// nothing order can be killed on the strength of this plan.
    /// </summary>
    private List<(Order Resting, decimal Volume)> Plan(Order incoming, decimal? limit)
    {
        List<(Order Resting, decimal Volume)> legs = [];
        Order[] candidates = [.. OppositeSide(incoming)];
        decimal projectedLast = LastTradePrice;
        decimal remaining = incoming.Remaining;

        foreach (Order resting in candidates)
        {
            if (remaining <= 0m)
            {
                break;
            }

            if (resting.Remaining <= 0m)
            {
                continue;
            }

            decimal price = resting.Price!.Value;

            if (limit is { } ceiling && (incoming.Side == OrderSide.Buy ? price > ceiling : price < ceiling))
            {
                break;
            }

            if (!IsWithinBand(projectedLast, price))
            {
                break;
            }

            decimal volume = Math.Min(remaining, resting.Remaining);
            legs.Add((resting, volume));
            remaining -= volume;
            projectedLast = price;
        }

        return legs;
    }

    private void Match(Order incoming, decimal? limit)
    {
        foreach ((Order resting, decimal volume) in Plan(incoming, limit))
        {
            Execute(incoming, resting, resting.Price!.Value, volume);
        }
    }

    private IEnumerable<Order> OppositeSide(Order incoming) => incoming.Side == OrderSide.Buy
        ? _resting.Where(order => order.Side == OrderSide.Sell && order.Kind != OrderKind.StopLoss)
            .OrderBy(order => order.Price).ThenBy(order => order.Id)
        : _resting.Where(order => order.Side == OrderSide.Buy && order.Kind != OrderKind.StopLoss)
            .OrderByDescending(order => order.Price).ThenBy(order => order.Id);

    private void Execute(Order incoming, Order resting, decimal price, decimal volume)
    {
        Order buy = incoming.Side == OrderSide.Buy ? incoming : resting;
        Order sell = incoming.Side == OrderSide.Buy ? resting : incoming;
        decimal consideration = volume * price;

        _simulation.Ledger.SettleEscrow(sell.Company, buy.Company, Product, volume);
        _simulation.Cash.SettleEscrow(
            buy.Company,
            sell.Company,
            consideration,
            CashCategory.SecondaryPurchase,
            $"{Product} trade at {price}");

        buy.EscrowedCash -= consideration;
        buy.RecordFill(volume);
        sell.RecordFill(volume);

        _trades.Add(new Trade(_trades.Count + 1, Product, buy.Id, sell.Id, price, volume));
        LastTradePrice = price;
        _simulation.Journal.Record(TradeChannel.Exchange, Product, price, volume, buy.Company, sell.Company);

        if (resting.Remaining <= 0m)
        {
            _resting.Remove(resting);
            ReleaseUnusedCash(resting);
        }
    }

    /// <summary>Hands back whatever a finished or cancelled order set aside and did not spend.</summary>
    private void ReleaseUnusedCash(Order order)
    {
        if (order.EscrowedCash <= 0m)
        {
            return;
        }

        _simulation.Cash.ReleaseEscrow(order.Company, order.EscrowedCash);
        order.EscrowedCash = 0m;
    }

    private void CheckStops()
    {
        while (true)
        {
            Order? triggered = _resting.FirstOrDefault(order => order.Kind == OrderKind.StopLoss && IsTriggered(order));

            if (triggered is null)
            {
                return;
            }

            _resting.Remove(triggered);

            if (triggered.FillPolicy == FillPolicy.FillOrKill && Fillable(triggered) < triggered.Remaining)
            {
                Reject(triggered);
                continue;
            }

            Match(triggered, limit: null);
            CancelRemainder(triggered);
        }
    }

    private bool IsTriggered(Order stop)
    {
        return stop.Side == OrderSide.Sell
            ? LastTradePrice <= stop.StopPrice!.Value
            : LastTradePrice >= stop.StopPrice!.Value;
    }

    private void CancelRemainder(Order order)
    {
        if (order.Remaining > 0m)
        {
            Reject(order);
        }
    }

    /// <summary>Takes the rest of an order off the market and hands its escrow back.</summary>
    private void Reject(Order order)
    {
        if (order.Remaining > 0m && order.Side == OrderSide.Sell)
        {
            _simulation.Ledger.ReleaseEscrow(order.Company, Product, order.Remaining);
        }

        ReleaseUnusedCash(order);
        order.Status = OrderStatus.Cancelled;
    }

    private bool IsWithinBand(decimal price) => IsWithinBand(LastTradePrice, price);

    private bool IsWithinBand(decimal referencePrice, decimal price) =>
        price >= referencePrice * (1m - VolatilityBand) && price <= referencePrice * (1m + VolatilityBand);

    public override string ToString() => $"{Product} book";
}
