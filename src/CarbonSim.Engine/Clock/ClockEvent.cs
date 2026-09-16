namespace CarbonSim.Engine.Clock;

/// <summary>Everything the clock announces, so hosts can turn it into messages or UI state.</summary>
public enum ClockEventKind
{
    YearStarted,
    AuctionOpeningSoon,
    AuctionOpened,
    AuctionEndingSoon,
    AuctionClosed,
    YearEnding,
    YearEnded,
    TradingHaltWarned,
    TradingHalted,
    SimulationEnded,
}

/// <summary>
/// Something the clock did, addressed to a year, a section of that year, and the virtual
/// offset inside the year at which it happened.
/// </summary>
public sealed record ClockEvent(ClockEventKind Kind, int Year, int Auction, TimeSpan AtYearOffset)
{
    public override string ToString() => $"{Kind} year {Year} auction {Auction} at {AtYearOffset}";
}
