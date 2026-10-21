using CarbonSim.Engine.Bots;
using CarbonSim.Engine.Clock;
using CarbonSim.Engine.Domain;
using CarbonSim.Engine.Market;
using CarbonSim.Engine.Market.Exchange;
using CarbonSim.Engine.Market.Otc;
using CarbonSim.Engine.Reporting;

namespace CarbonSim.Engine.Run;

/// <summary>
/// One thing the feed owes its watchers: an auction that opened, is closing soon, or closed. A
/// closed auction carries the auction itself, because the result it produced (the uniform price,
/// the volume sold, who was served) is the news.
/// </summary>
public sealed record RunNotice(
    ClockEventKind Kind,
    int Year,
    int Sequence,
    decimal OfferedVolume = 0m,
    TimeSpan TimeLeft = default,
    Auction? Cleared = null);

/// <summary>One order book as it stands now, so the feed can say what moved since it last spoke.</summary>
public sealed record BookChange(Product Product, decimal? BestBid, decimal? BestOffer, decimal LastTrade);

/// <summary>What an administrator's change of the collar did, for whoever has to announce it.</summary>
public sealed record CollarChange(
    int Year,
    decimal OldFloor,
    decimal NewFloor,
    decimal OldCeiling,
    decimal NewCeiling,
    IReadOnlyList<ReleasedBid> BidsReleased);

/// <summary>A bid a collar change took off the book, with the auction it was resting in.</summary>
public sealed record ReleasedBid(int Year, int Sequence, AuctionBid Bid);

/// <summary>
/// What one operation on a run did, and what the feed owes after it. A verb returns this instead of
/// leaving its news behind in queues the host has to remember to drain, so the host broadcasts from
/// a value: what the bots did, whether the collar moved, which auctions opened, closed or cleared,
/// which trades filled, how the books and the run's own state stand - each present only when it
/// moved.
/// </summary>
public sealed record RunResult
{
    /// <summary>What the bots did on this operation; empty unless it was an advance.</summary>
    public IReadOnlyList<BotAction> BotActions { get; init; } = [];

    /// <summary>The administrator's change of the collar, when this operation was one.</summary>
    public CollarChange? Collar { get; init; }

    /// <summary>The exchange order this operation placed, cancelled or answered, when it touched one.</summary>
    public Order? Order { get; init; }

    /// <summary>The OTC offer this operation sent or answered, when it touched one.</summary>
    public OtcOffer? Offer { get; init; }

    /// <summary>Auctions that opened, are closing soon, or closed, in the order they came due.</summary>
    public IReadOnlyList<RunNotice> Notices { get; init; } = [];

    /// <summary>Exchange fills since the last operation reported them.</summary>
    public IReadOnlyList<MarketTrade> Fills { get; init; } = [];

    /// <summary>Books whose top has moved since the last operation reported them.</summary>
    public IReadOnlyList<BookChange> Books { get; init; } = [];

    /// <summary>The run's state and year, when they have moved.</summary>
    public (string State, int Year)? State { get; init; }

    /// <summary>The market's state, year, section and window, when they have moved.</summary>
    public (string State, int Year, int Auction, bool Open)? Market { get; init; }
}
