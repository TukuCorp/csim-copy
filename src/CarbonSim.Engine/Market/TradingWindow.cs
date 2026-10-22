using CarbonSim.Engine.Clock;
using CarbonSim.Engine.Domain;

namespace CarbonSim.Engine.Market;

/// <summary>Why a market action was refused: the window it needs is shut.</summary>
public enum TradeRefusal
{
    /// <summary>The year is not running: pending, paused, halted, ended or over.</summary>
    YearNotRunning,

    /// <summary>The current section's auction window is closed.</summary>
    AuctionClosed,

    /// <summary>The administrator has not opened this market to players in this exercise.</summary>
    MarketClosed,
}

/// <summary>
/// One home for "may this be done now": a year trades only while it is running, and an auction takes
/// bids only while its section's window is open. The markets and the host read the rule from here
/// rather than each keeping its own copy, so a hub client or a stale page cannot act out of window.
/// Cancelling an order and turning an offer down are not gated: they take a player out of the market
/// rather than into it.
/// </summary>
public static class TradingWindow
{
    /// <summary>Refuses unless the year is running.</summary>
    public static void RequireTrading(Simulation simulation)
    {
        ArgumentNullException.ThrowIfNull(simulation);

        if (simulation.State != SimulationState.Running)
        {
            throw new TradeRefusedException(
                TradeRefusal.YearNotRunning,
                $"Trading is closed: year {simulation.CurrentYear} is {simulation.State}.");
        }
    }

    /// <summary>Refuses unless the year is running and the current section's auction window is open.</summary>
    public static void RequireAuctionOpen(SimulationClock clock)
    {
        ArgumentNullException.ThrowIfNull(clock);

        if (clock.State != SimulationState.Running)
        {
            throw new TradeRefusedException(
                TradeRefusal.YearNotRunning,
                $"The auction is not taking bids: year {clock.CurrentYear} is {clock.State}.");
        }

        if (!clock.IsAuctionOpen)
        {
            throw new TradeRefusedException(
                TradeRefusal.AuctionClosed,
                $"The auction is closed: section {clock.CurrentAuction} of year {clock.CurrentYear} is not in its window.");
        }
    }
}

/// <summary>
/// A market action refused because its window is shut. It is a type rather than only a message so a
/// screen can write the refusal in the reader's own language: the engine has no resources of its own.
/// </summary>
public sealed class TradeRefusedException : InvalidOperationException
{
    public TradeRefusedException(TradeRefusal refusal, string message)
        : base(message)
    {
        Refusal = refusal;
    }

    /// <summary>Which window was shut, for a caller that wants to say so itself.</summary>
    public TradeRefusal Refusal { get; }
}
