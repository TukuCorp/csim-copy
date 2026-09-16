namespace CarbonSim.Engine.Domain;

/// <summary>
/// Lifecycle of one simulation run: it waits to start, runs a virtual year while the clock
/// advances, can be paused by the administrator (mid-year or at an auction close), halts
/// trading with a warning, then ends the year or the game.
/// </summary>
public enum SimulationState
{
    /// <summary>Configured and waiting for the administrator to begin year 1.</summary>
    Pending,

    /// <summary>The clock is advancing and markets are open.</summary>
    Running,

    /// <summary>The administrator paused the clock; markets are frozen.</summary>
    Paused,

    /// <summary>Paused at the end of an auction, before the next section of the year.</summary>
    PausedAfterAuction,

    /// <summary>Trading is halted (regulator intervention) while the year continues.</summary>
    TradingHalted,

    /// <summary>The year closed and results were reconciled; the next year has not started.</summary>
    YearEnded,

    /// <summary>The last year closed; the simulation is over and reports are final.</summary>
    GameEnded,
}
