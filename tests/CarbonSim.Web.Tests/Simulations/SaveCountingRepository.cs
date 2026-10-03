using CarbonSim.Data.Persistence;
using CarbonSim.Engine.Snapshot;

namespace CarbonSim.Web.Tests.Simulations;

/// <summary>How many times a run has been saved, shared across the scopes a host creates.</summary>
public sealed class SaveCounter
{
    private int _count;

    public int Count => Volatile.Read(ref _count);

    internal void Increment() => Interlocked.Increment(ref _count);
}

/// <summary>
/// Wraps the real repository so a test can see how often a run is saved. The counter is a
/// singleton because each tick runs in a service scope of its own, while the repository stays
/// scoped with the database context it writes through.
/// </summary>
internal sealed class CountingSimulationRepository(EfSimulationRepository inner, SaveCounter counter) : ISimulationRepository
{
    public async Task SaveAsync(SimulationSnapshot snapshot, CancellationToken cancellationToken = default)
    {
        await inner.SaveAsync(snapshot, cancellationToken).ConfigureAwait(false);
        counter.Increment();
    }

    public Task<SimulationSnapshot?> LoadAsync(Guid simulationId, CancellationToken cancellationToken = default) =>
        inner.LoadAsync(simulationId, cancellationToken);

    public Task<IReadOnlyList<SimulationSummary>> ListAsync(CancellationToken cancellationToken = default) =>
        inner.ListAsync(cancellationToken);

    public Task<bool> DeleteAsync(Guid simulationId, CancellationToken cancellationToken = default) =>
        inner.DeleteAsync(simulationId, cancellationToken);
}
