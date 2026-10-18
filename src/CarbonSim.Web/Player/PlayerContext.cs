using CarbonSim.Engine.Market;
using CarbonSim.Web.Components.Shared;
using CarbonSim.Web.Resources;
using Microsoft.Extensions.Localization;

namespace CarbonSim.Web.Player;

/// <summary>What the shell and the page inside it both render from: the latest snapshot and any refusal.</summary>
public sealed record PlayerScreenSnapshot(Guid? SimulationId, PlayerView? View, string? Error, string? Status);

/// <summary>
/// The one place a run page reads and acts through. The shell loads a snapshot once per refresh and
/// hands it down as a cascading value; a page's button calls <see cref="ActAsync"/>, which goes
/// through <see cref="IPlayerSession" /> to the registry and then reloads the snapshot, so the page
/// updates from the same path the live feed would have used.
/// </summary>
public sealed class PlayerContext
{
    private readonly IPlayerSession _session;
    private readonly IStringLocalizer<SharedStrings> _strings;

    public PlayerContext(IPlayerSession session, IStringLocalizer<SharedStrings> strings)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
        _strings = strings ?? throw new ArgumentNullException(nameof(strings));
    }

    public PlayerScreenSnapshot Snapshot { get; private set; } = new(null, null, null, null);

    public event Action? Changed;

    /// <summary>Points the context at a run's screens, if it is not already there.</summary>
    public async Task AttachAsync(Guid? simulationId)
    {
        if (Snapshot.SimulationId == simulationId && Snapshot.View is not null)
        {
            return;
        }

        if (simulationId is not { } id)
        {
            Update(new PlayerScreenSnapshot(null, null, null, null));
            return;
        }

        await LoadAsync(id);
    }

    /// <summary>
    /// Re-reads the run for whoever is signed in. What the last control reported stays until the
    /// player dismisses it: the shell refreshes every second, faster than anyone reads a refusal.
    /// </summary>
    public async Task RefreshAsync()
    {
        if (Snapshot.SimulationId is { } simulationId)
        {
            PlayerView? view = await _session.ForPlayerAsync(simulationId).ConfigureAwait(false);
            Update(Snapshot with { View = view });
        }
    }

    /// <summary>Runs a player action and reloads, reporting a refusal instead of throwing at the UI.</summary>
    public Task ActAsync(Func<IPlayerSession, Task> action, string? status = null) =>
        ActAsync<object?>(async session =>
        {
            await action(session).ConfigureAwait(false);
            return null;
        }, status);

    /// <summary>
    /// The same, for an action that reports what it did: the outcome comes back to the caller when
    /// nothing was refused, and null when a refusal was written to the snapshot instead.
    /// </summary>
    public async Task<T?> ActAsync<T>(Func<IPlayerSession, Task<T>> action, string? status = null)
    {
        if (Snapshot.SimulationId is not { } simulationId)
        {
            return default;
        }

        string? error = null;
        T? result = default;

        try
        {
            result = await action(_session).ConfigureAwait(false);
        }
        catch (TradeRefusedException refused)
        {
            // The engine refuses in its own words; the screen says it in the reader's language.
            error = Display.Refusal(refused.Refusal, _strings);
        }
        catch (Exception exception)
        {
            error = exception.Message;
        }

        PlayerView? view = await _session.ForPlayerAsync(simulationId).ConfigureAwait(false);
        Update(new PlayerScreenSnapshot(simulationId, view, error, error is null ? status : null));

        return error is null ? result : default;
    }

    /// <summary>Dismisses whatever the last action reported.</summary>
    public void ClearNotice()
    {
        Update(Snapshot with { Error = null, Status = null });
    }

    private async Task LoadAsync(Guid simulationId)
    {
        PlayerView? view = await _session.ForPlayerAsync(simulationId).ConfigureAwait(false);
        Update(new PlayerScreenSnapshot(simulationId, view, null, null));
    }

    private void Update(PlayerScreenSnapshot snapshot)
    {
        Snapshot = snapshot;
        Changed?.Invoke();
    }
}
