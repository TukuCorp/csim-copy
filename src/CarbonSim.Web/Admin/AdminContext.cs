namespace CarbonSim.Web.Admin;

/// <summary>
/// Reads the run a console page belongs to out of its address, <c>/admin/run/{id}</c>. The layout
/// needs the same answer the page's route parameter carries, and a layout cannot take a route
/// parameter, so the address is the one place both agree on.
/// </summary>
public static class AdminRoute
{
    public const string Prefix = "admin";

    public static Guid? SimulationId(string? uri)
    {
        if (string.IsNullOrWhiteSpace(uri) || !Uri.TryCreate(uri, UriKind.RelativeOrAbsolute, out Uri? parsed))
        {
            return null;
        }

        string path = parsed.IsAbsoluteUri ? parsed.AbsolutePath : uri;
        string[] parts = path.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);

        return parts.Length >= 3
            && string.Equals(parts[0], Prefix, StringComparison.OrdinalIgnoreCase)
            && string.Equals(parts[1], "run", StringComparison.OrdinalIgnoreCase)
            && Guid.TryParse(parts[2], out Guid simulationId)
                ? simulationId
                : null;
    }

    public static string Run(Guid simulationId) => $"/{Prefix}/run/{simulationId:D}";

    public static string Reports(Guid simulationId) => $"/{Prefix}/run/{simulationId:D}/reports";

    public static string Surrender(Guid simulationId) => $"/{Prefix}/run/{simulationId:D}/surrender";
}

/// <summary>How often an open console screen re-reads the runs; null in tests, so nothing polls.</summary>
public sealed record AdminContextOptions
{
    public TimeSpan? RefreshInterval { get; init; }
}

/// <summary>
/// The one place a console page reads and acts through. The shell loads a snapshot once per
/// refresh and hands it down as a cascading value; a page's button calls <see cref="ActAsync"/>,
/// which goes through <see cref="IAdminSession" /> to the driver and then reloads the snapshot, so
/// the page updates from the same path the next reader would have used.
/// </summary>
public sealed class AdminContext
{
    private readonly IAdminSession _session;

    public AdminContext(IAdminSession session)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
    }

    public AdminSnapshot Snapshot { get; private set; } = AdminSnapshot.Denied(null);

    public Guid? RunId { get; private set; }

    public event Action? Changed;

    /// <summary>Points the context at a run's pages, if it is not already there.</summary>
    public async Task AttachAsync(Guid? runId)
    {
        if (RunId == runId && Snapshot.IsAdministrator && Snapshot.RunId == runId)
        {
            return;
        }

        RunId = runId;
        await LoadAsync(runId);
    }

    /// <summary>Re-reads the console picture.</summary>
    public Task RefreshAsync() => LoadAsync(RunId);

    /// <summary>Points at another run, for the runs list's Open control.</summary>
    public Task SelectAsync(Guid? runId)
    {
        RunId = runId;

        return LoadAsync(runId);
    }

    /// <summary>Runs a control and reloads, reporting a refusal instead of throwing at the UI.</summary>
    public async Task ActAsync(Func<IAdminSession, Task> action, string? status = null)
    {
        string? error = null;

        try
        {
            await action(_session).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            error = exception.Message;
        }

        AdminSnapshot loaded = await _session.SnapshotAsync(RunId).ConfigureAwait(false);
        Snapshot = loaded with { Error = error, Status = error is null ? status : null };
        Changed?.Invoke();
    }

    private async Task LoadAsync(Guid? runId)
    {
        Snapshot = await _session.SnapshotAsync(runId).ConfigureAwait(false);
        Changed?.Invoke();
    }
}
