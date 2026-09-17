using CarbonSim.Engine.Domain;
using CarbonSim.Engine.Snapshot;

namespace CarbonSim.Data.Persistence;

/// <summary>A saved run in a list: enough to show a trainer what is on the shelf.</summary>
public sealed record SimulationSummary(Guid Id, string Name, ulong Seed, SimulationState State, int CurrentYear);

/// <summary>
/// Saving and loading whole runs. A run is saved as one unit: the graph is written as it stands,
/// so a caller never has to say which part of the state changed.
/// </summary>
public interface ISimulationRepository
{
    /// <summary>Writes the run, replacing any earlier save of the same simulation.</summary>
    Task SaveAsync(SimulationSnapshot snapshot, CancellationToken cancellationToken = default);

    /// <summary>Reads a run back, or null when nothing has been saved under that identifier.</summary>
    Task<SimulationSnapshot?> LoadAsync(Guid simulationId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SimulationSummary>> ListAsync(CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(Guid simulationId, CancellationToken cancellationToken = default);
}
