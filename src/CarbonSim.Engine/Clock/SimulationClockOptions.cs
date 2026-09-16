namespace CarbonSim.Engine.Clock;

/// <summary>
/// How the trainer runs the clock, as opposed to <see cref="Domain.Parameters"/>, which
/// describes the trading system itself.
/// </summary>
public sealed class SimulationClockOptions
{
    /// <summary>"Pause after each auction": stop the clock at every auction close until resumed.</summary>
    public bool PauseAfterAuction { get; init; }

    /// <summary>How long before an auction opens (and before it closes) players are told.</summary>
    public TimeSpan AuctionNotice { get; init; } = TimeSpan.FromSeconds(30);
}
