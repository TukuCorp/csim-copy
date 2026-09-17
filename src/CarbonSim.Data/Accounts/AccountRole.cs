namespace CarbonSim.Data.Accounts;

/// <summary>
/// What an account is allowed to do: play a company, or set up and run the simulation. The
/// administrator role is granted by the host, never claimed by a registering player.
/// </summary>
public enum AccountRole
{
    /// <summary>Plays one human company in the simulation.</summary>
    Player = 0,

    /// <summary>Configures the trading system, drives the clock and administers the run.</summary>
    Administrator = 1,
}
