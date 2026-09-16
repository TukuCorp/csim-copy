using CarbonSim.Engine.Domain;
using CarbonSim.Engine.Market;

namespace CarbonSim.Engine.Reporting;

/// <summary>Which market a trade happened on.</summary>
public enum TradeChannel
{
    Auction,
    Exchange,
    Otc,
}

/// <summary>One completed trade: who bought, who sold, at what price, for how much.</summary>
public sealed record MarketTrade(
    long Sequence,
    int Year,
    TradeChannel Channel,
    Product Product,
    decimal Price,
    decimal Volume,
    Company Buyer,
    Company? Seller)
{
    /// <summary>Money that changed hands.</summary>
    public decimal Consideration => Price * Volume;

    public override string ToString() =>
        $"Year {Year} {Channel} {Volume} {Product} at {Price} ({Buyer.Name} from {Seller?.Name ?? "the government"})";
}

/// <summary>
/// Every trade the simulation has seen, in the order it happened. The system report and the
/// price averages are built from this rather than from the markets themselves, so a cleared
/// auction keeps showing up after its bids are gone.
/// </summary>
public sealed class MarketJournal
{
    private readonly Simulation _simulation;
    private readonly List<MarketTrade> _trades = [];

    internal MarketJournal(Simulation simulation)
    {
        _simulation = simulation;
    }

    public IReadOnlyList<MarketTrade> Trades => _trades;

    /// <summary>Trades between two years inclusive, optionally one channel and one instrument kind.</summary>
    public IEnumerable<MarketTrade> Between(int fromYear, int toYear, TradeChannel? channel = null, ProductKind? kind = null)
    {
        return _trades
            .Where(trade => trade.Year >= fromYear && trade.Year <= toYear)
            .Where(trade => channel is null || trade.Channel == channel)
            .Where(trade => kind is null || trade.Product.Kind == kind);
    }

    /// <summary>Trades of one year.</summary>
    public IEnumerable<MarketTrade> In(int year, TradeChannel? channel = null, ProductKind? kind = null) =>
        Between(year, year, channel, kind);

    /// <summary>Volume traded between two years.</summary>
    public decimal Volume(int fromYear, int toYear, TradeChannel? channel = null, ProductKind? kind = null) =>
        Between(fromYear, toYear, channel, kind).Sum(trade => trade.Volume);

    /// <summary>Money paid between two years.</summary>
    public decimal Consideration(int fromYear, int toYear, TradeChannel? channel = null, ProductKind? kind = null) =>
        Between(fromYear, toYear, channel, kind).Sum(trade => trade.Consideration);

    /// <summary>Average price between two years, weighted by the volume actually traded.</summary>
    public decimal AveragePrice(int fromYear, int toYear, TradeChannel? channel = null, ProductKind? kind = null)
    {
        decimal volume = Volume(fromYear, toYear, channel, kind);

        return volume <= 0m ? 0m : Consideration(fromYear, toYear, channel, kind) / volume;
    }

    internal void Record(TradeChannel channel, Product product, decimal price, decimal volume, Company buyer, Company? seller)
    {
        _trades.Add(new MarketTrade(_trades.Count + 1, _simulation.CurrentYear, channel, product, price, volume, buyer, seller));
    }
}
