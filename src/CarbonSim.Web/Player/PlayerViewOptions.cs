namespace CarbonSim.Web.Player;

/// <summary>How the player screens keep themselves current.</summary>
public sealed class PlayerViewOptions
{
    /// <summary>
    /// How often a run page re-reads its view while the simulation is running, so countdowns and
    /// other players' trades appear without a reload. Null turns the loop off, which is what the
    /// component tests use.
    /// </summary>
    public TimeSpan? RefreshInterval { get; set; } = TimeSpan.FromSeconds(1);
}
