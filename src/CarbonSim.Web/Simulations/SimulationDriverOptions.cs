namespace CarbonSim.Web.Simulations;

/// <summary>How often the hosted driver polls the run it owns when nobody ticks it by hand.</summary>
public sealed class SimulationDriverOptions
{
    /// <summary>Zero disables the shared loop: the run only moves when a test ticks it.</summary>
    public TimeSpan PollInterval { get; set; } = TimeSpan.FromSeconds(1);
}
