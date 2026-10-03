using Bunit;
using CarbonSim.Web.Admin;
using CarbonSim.Web.Player;
using Microsoft.Extensions.DependencyInjection;

namespace CarbonSim.Web.Tests.Components;

/// <summary>A console session a screen test drives: one prepared snapshot, and every control recorded.</summary>
public sealed class StubAdminSession : IAdminSession
{
    public AdminSnapshot Snapshot { get; set; } = AdminSnapshot.Denied("Stub");

    public ScenarioDraft? Draft { get; set; }

    public bool Administrator { get; set; } = true;

    public List<string> Actions { get; } = [];

    public Task<bool> IsAdministratorAsync(CancellationToken cancellationToken = default) => Task.FromResult(Administrator);

    public Task<AdminSnapshot> SnapshotAsync(Guid? runId, CancellationToken cancellationToken = default) => Task.FromResult(Snapshot);

    public Task<ScenarioDraft?> SetupDraftAsync(CancellationToken cancellationToken = default) => Task.FromResult(Draft);

    public Task<Guid> CreateRunAsync(ScenarioDraft draft, CancellationToken cancellationToken = default)
    {
        Actions.Add("create");
        return Task.FromResult(Guid.Empty);
    }

    public Task LoadRunAsync(Guid simulationId, CancellationToken cancellationToken = default)
    {
        Actions.Add($"load:{simulationId}");
        return Task.CompletedTask;
    }

    public Task DeleteRunAsync(Guid simulationId, CancellationToken cancellationToken = default)
    {
        Actions.Add($"delete:{simulationId}");
        return Task.CompletedTask;
    }

    public Task BeginYearAsync(Guid simulationId, CancellationToken cancellationToken = default)
    {
        Actions.Add("begin");
        return Task.CompletedTask;
    }

    public Task PauseAsync(Guid simulationId, CancellationToken cancellationToken = default)
    {
        Actions.Add("pause");
        return Task.CompletedTask;
    }

    public Task ResumeAsync(Guid simulationId, CancellationToken cancellationToken = default)
    {
        Actions.Add("resume");
        return Task.CompletedTask;
    }

    public Task HaltAsync(Guid simulationId, TimeSpan? warnLead, CancellationToken cancellationToken = default)
    {
        Actions.Add($"halt:{warnLead?.TotalSeconds}");
        return Task.CompletedTask;
    }

    public Task EndYearAsync(Guid simulationId, CancellationToken cancellationToken = default)
    {
        Actions.Add("end-year");
        return Task.CompletedTask;
    }

    public Task EndSimulationAsync(Guid simulationId, CancellationToken cancellationToken = default)
    {
        Actions.Add("end-simulation");
        return Task.CompletedTask;
    }

    public Task SetMessagingAsync(Guid simulationId, bool enabled, CancellationToken cancellationToken = default)
    {
        Actions.Add($"messaging:{enabled}");
        return Task.CompletedTask;
    }

    public Task IssueFineAsync(Guid simulationId, int unitId, decimal amount, string description, CancellationToken cancellationToken = default)
    {
        Actions.Add($"fine:{unitId}:{amount}:{description}");
        return Task.CompletedTask;
    }

    public Task DisburseOffsetsAsync(Guid simulationId, int companyId, decimal volume, CancellationToken cancellationToken = default)
    {
        Actions.Add($"offsets:{companyId}:{volume}");
        return Task.CompletedTask;
    }

    public Task AddAbatementAsync(Guid simulationId, int unitId, int optionIndex, CancellationToken cancellationToken = default)
    {
        Actions.Add($"abatement:{unitId}:{optionIndex}");
        return Task.CompletedTask;
    }

    public Task ApplyShockAsync(Guid simulationId, int unitId, int year, decimal deltaTonnes, CancellationToken cancellationToken = default)
    {
        Actions.Add($"shock:{unitId}:{year}:{deltaTonnes}");
        return Task.CompletedTask;
    }

    public Task SetAutoTradeAsync(Guid simulationId, int unitId, bool enabled, CancellationToken cancellationToken = default)
    {
        Actions.Add($"autotrade:{unitId}:{enabled}");
        return Task.CompletedTask;
    }

    public Task SetRegistrationAsync(bool open, string pin, CancellationToken cancellationToken = default)
    {
        Actions.Add($"registration:{open}:{pin}");
        return Task.CompletedTask;
    }

    public Task<string?> CsvAsync(Guid simulationId, string report, CancellationToken cancellationToken = default) =>
        Task.FromResult<string?>("metric,value\r\n");
}

/// <summary>
/// The base for a console screen test: a stub session holding one prepared snapshot, and a real
/// context pointed at the run, so a page's control has somewhere to go.
/// </summary>
public abstract class AdminScreenTestContext : TestContext
{
    protected AdminScreenTestContext()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddLocalization();
        Services.AddSingleton<IAdminSession>(Session);
        Services.AddSingleton(new AdminContextOptions { RefreshInterval = null });
        Services.AddSingleton(provider =>
        {
            AdminContext context = new(provider.GetRequiredService<IAdminSession>());
            context.AttachAsync(SimulationId).GetAwaiter().GetResult();

            return context;
        });
    }

    protected StubAdminSession Session { get; } = new();

    protected Guid SimulationId { get; } = Guid.Parse("5c1d2e3f-4a5b-4c6d-8e7f-9a0b1c2d3e4f");

    protected AdminSnapshot Snapshot
    {
        get => Session.Snapshot;
        set => Session.Snapshot = value;
    }
}

/// <summary>A prepared console snapshot with one of everything, so a screen renders real content.</summary>
internal static class AdminViewFixture
{
    public static AdminSnapshot Sample(Guid simulationId, string state = "Running", string detailState = "Running")
    {
        AdminRunDetail detail = Detail(simulationId, detailState);

        return new AdminSnapshot(
            true,
            "Tung",
            null,
            null,
            [new AdminRunSummary(simulationId, "Vietnam pilot", detailState, 1, 3, 2, 2, 2, true)],
            [new AdminSavedRun(simulationId, "Vietnam pilot", detailState, 1, true)],
            simulationId,
            detail,
            new AdminRegistrationView(true, "pin-123"));
    }

    public static AdminRunDetail Detail(Guid simulationId, string state) => new(
        simulationId,
        "Vietnam pilot",
        state,
        1,
        3,
        2,
        4,
        false,
        state == "Running",
        true,
        TimeSpan.FromMinutes(11),
        TimeSpan.FromMinutes(2),
        TimeSpan.FromMinutes(3),
        Parameters(),
        [new AdminSectorView("Power", 1m, 2, 900_000m)],
        Companies(),
        Units(),
        [new AdminLeaderView(1, "Delta Power", "Delta Unit 1", "Delta Power", 2.5m, 81_000m, false)],
        [new AdminPriceView(1, 250_000m, 110m, 1_000m, 90m, 105m, 100m, 120m, 4)],
        Surrender(),
        Totals(1_000_000m, 900_000m, 100_000m),
        Totals(1_000_000m, 900_000m, 100_000m),
        500_000m,
        [new HoldingView(ProductView.Allowance(1), 100_000m, 100_000m)],
        state == "YearEnded");

    private static AdminParametersView Parameters() => new(
        1_000_000m,
        0.03m,
        0.9m,
        3,
        0.1m,
        1m,
        300m,
        1m,
        100m,
        300m,
        4,
        TimeSpan.FromMinutes(20),
        TimeSpan.FromMinutes(3),
        0.6m,
        0.1m,
        0.07m,
        [new AdminGrowthView("Power", 0.02m, 0.06m)]);

    private static List<AdminCompanyView> Companies() =>
    [
        new AdminCompanyView(1, "Delta Power", "Power", "Delta Power", false, 1_000_000m, 100_000m, 100_000m, 90_000m, 10_000m, 250_000m, 2.5m, 81_000m, true, 0m),
        new AdminCompanyView(2, "Red River Power", "Power", "Red River Power", false, 1_000_000m, 100_000m, 100_000m, 90_000m, 10_000m, 400_000m, 4.0m, -1_000m, false, 0m),
    ];

    private static List<AdminUnitView> Units() =>
    [
        new AdminUnitView(1, "Delta Unit 1", "Delta Power", "Power", 100_000m, 90_000m, 100_000m, 5_000m, false, false, 1, Opportunities()),
        new AdminUnitView(2, "Red River Unit 1", "Red River Power", "Power", 100_000m, 90_000m, 100_000m, 0m, false, false, 0, []),
    ];

    private static List<AdminAbatementOptionView> Opportunities() =>
    [
        new AdminAbatementOptionView(0, "P1", "Boiler efficiency", 5_000m, 500_000m, 1, 10, -2m, false),
        new AdminAbatementOptionView(1, "P2", "Waste heat recovery", 4_000m, 800_000m, 1, 10, 200m, false),
    ];

    private static List<AdminSurrenderView> Surrender() =>
    [
        new AdminSurrenderView(1, "Delta Power", "Delta Unit 1", 100_000m, 100_000m, 100_000m, 90_000m, 10_000m, 0m, 0m, 0m, 0m, true),
        new AdminSurrenderView(2, "Red River Power", "Red River Unit 1", 100_000m, 100_000m, 90_000m, 90_000m, 0m, 0m, 0m, 10_000m, 3_000_000m, false),
    ];

    private static SystemTotalsView Totals(decimal cap, decimal free, decimal auctionable) => new(
        100_000m,
        90_000m,
        10_000m,
        0m,
        0m,
        30_000m,
        30_000m,
        1_000m,
        1_000m,
        5_000m,
        550_000m,
        110m,
        87m,
        2,
        200_000m,
        200_000m,
        0,
        0m,
        0m,
        cap,
        free,
        auctionable);
}
