using CarbonSim.Engine.Domain;
using CarbonSim.Web.Player;

namespace CarbonSim.Web.Simulations;

/// <summary>
/// The small pieces of a run's state that the player's screens and the console read the same way,
/// built in one place so the two cannot drift apart.
/// </summary>
internal static class RunViews
{
    /// <summary>
    /// The year a run is about. Nothing has begun while a run is pending, so the year it is
    /// waiting to play is year one rather than a year zero no screen has anything to say about.
    /// </summary>
    public static int YearOf(Simulation simulation) => Math.Max(1, simulation.CurrentYear);

    /// <summary>The government's allowance reserve, one row per vintage it was issued for.</summary>
    public static List<HoldingView> Reserve(Simulation simulation) =>
    [
        .. simulation.Allocation.Years.Select(vintage => new HoldingView(
            ProductView.Allowance(vintage),
            simulation.Government.Held(vintage),
            simulation.Government.Issued(vintage))),
    ];
}

