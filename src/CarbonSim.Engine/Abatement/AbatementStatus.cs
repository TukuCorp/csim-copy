namespace CarbonSim.Engine.Abatement;

/// <summary>Where an implemented project stands in a given year.</summary>
public enum AbatementStatus
{
    /// <summary>Being built; no reductions yet, but the capital is spent.</summary>
    Building,

    /// <summary>Running and reducing emissions.</summary>
    Operating,

    /// <summary>Running and has earned back more than it cost.</summary>
    InProfit,

    /// <summary>Past the end of its lifetime; it no longer reduces anything.</summary>
    Expired,
}
