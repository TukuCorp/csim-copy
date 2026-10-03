namespace CarbonSim.Web.Admin;

/// <summary>
/// What the administrator console asks of the host: the picture of the runs, and the controls
/// that drive one. Every control goes through the server-side driver, which takes the run's gate
/// and broadcasts what changed, so no screen ever mutates the engine itself. Every method is
/// refused unless the signed-in account holds the administrator role.
/// </summary>
public interface IAdminSession
{
    /// <summary>Whether the signed-in account may use the console at all.</summary>
    Task<bool> IsAdministratorAsync(CancellationToken cancellationToken = default);

    /// <summary>The runs on offer, the saved ones on the shelf, and one run in detail if named.</summary>
    Task<AdminSnapshot> SnapshotAsync(Guid? runId, CancellationToken cancellationToken = default);

    /// <summary>The configured scenario as an editable draft, or null for a caller who is not an administrator.</summary>
    Task<ScenarioDraft?> SetupDraftAsync(CancellationToken cancellationToken = default);

    /// <summary>Builds a pending run from an edited draft and saves it; throws with every problem at once.</summary>
    Task<Guid> CreateRunAsync(ScenarioDraft draft, CancellationToken cancellationToken = default);

    /// <summary>Loads a saved run back onto the shelf.</summary>
    Task LoadRunAsync(Guid simulationId, CancellationToken cancellationToken = default);

    /// <summary>Takes a run off the shelf and deletes its saved copy.</summary>
    Task DeleteRunAsync(Guid simulationId, CancellationToken cancellationToken = default);

    /// <summary>Starts a pending run's first year, or opens the next year of a closed one.</summary>
    Task BeginYearAsync(Guid simulationId, CancellationToken cancellationToken = default);

    Task PauseAsync(Guid simulationId, CancellationToken cancellationToken = default);

    Task ResumeAsync(Guid simulationId, CancellationToken cancellationToken = default);

    Task HaltAsync(Guid simulationId, TimeSpan? warnLead, CancellationToken cancellationToken = default);

    Task EndYearAsync(Guid simulationId, CancellationToken cancellationToken = default);

    Task EndSimulationAsync(Guid simulationId, CancellationToken cancellationToken = default);

    Task SetMessagingAsync(Guid simulationId, bool enabled, CancellationToken cancellationToken = default);

    Task IssueFineAsync(Guid simulationId, int unitId, decimal amount, string description, CancellationToken cancellationToken = default);

    Task DisburseOffsetsAsync(Guid simulationId, int companyId, decimal volume, CancellationToken cancellationToken = default);

    Task AddAbatementAsync(Guid simulationId, int unitId, int optionIndex, CancellationToken cancellationToken = default);

    Task ApplyShockAsync(Guid simulationId, int unitId, int year, decimal deltaTonnes, CancellationToken cancellationToken = default);

    Task SetAutoTradeAsync(Guid simulationId, int unitId, bool enabled, CancellationToken cancellationToken = default);

    /// <summary>Opens or closes registration and sets the access PIN visitors must quote.</summary>
    Task SetRegistrationAsync(bool open, string pin, CancellationToken cancellationToken = default);

    /// <summary>One report as CSV, or null for a caller who may not read it or a report that does not exist.</summary>
    Task<string?> CsvAsync(Guid simulationId, string report, CancellationToken cancellationToken = default);
}
