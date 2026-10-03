using CarbonSim.Data.Persistence;
using CarbonSim.Engine.Bots;
using CarbonSim.Engine.Clock;
using CarbonSim.Engine.Domain;
using CarbonSim.Engine.Market;
using CarbonSim.Engine.Market.Exchange;
using CarbonSim.Engine.Scenarios;
using CarbonSim.Engine.Snapshot;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CarbonSim.Web.Simulations;

/// <summary>
/// Drives every live run the host owns: the clock, auctions, bots and year end, with the results
/// pushed to whoever is watching. The run advances only when the driver ticks it, which is the
/// hosted loop in production and an explicit test call in the suite, and every mutation is
/// collected under the run's gate before it is broadcast.
/// </summary>
public sealed class SimulationClockService : BackgroundService
{
    private readonly IServiceProvider _services;
    private readonly SimulationDriverOptions _options;
    private readonly ILogger<SimulationClockService> _log;

    public SimulationClockService(
        IServiceProvider services,
        SimulationDriverOptions options,
        ILogger<SimulationClockService> log)
    {
        _services = services ?? throw new ArgumentNullException(nameof(services));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _log = log ?? throw new ArgumentNullException(nameof(log));
    }

    /// <summary>Advances one run once: clock, auction clears, bots, then the run's own events.</summary>
    public async Task TickAsync(Guid simulationId, CancellationToken cancellationToken = default)
    {
        using IServiceScope scope = _services.CreateScope();
        SimulationRegistry runs = scope.ServiceProvider.GetRequiredService<SimulationRegistry>();
        HostedRun run = runs.Run(simulationId);
        Announcement announcement;

        await run.Gate.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            TickOnce(run);
            announcement = run.CollectAnnouncement();
        }
        catch (Exception exception)
        {
            _log.LogError(exception, "Simulation {SimulationId} tick failed.", simulationId);
            throw;
        }
        finally
        {
            run.Gate.Release();
        }

        await runs.BroadcastAsync(simulationId, announcement).ConfigureAwait(false);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (_options.PollInterval <= TimeSpan.Zero)
        {
            return;
        }

        using PeriodicTimer timer = new(_options.PollInterval);

        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
            {
                IReadOnlyList<Guid> ids;

                using (IServiceScope scope = _services.CreateScope())
                {
                    ids = scope.ServiceProvider.GetRequiredService<SimulationRegistry>().Ids;
                }

                foreach (Guid id in ids)
                {
                    try
                    {
                        await TickAsync(id, stoppingToken).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                    {
                        throw;
                    }
                    catch (Exception exception)
                    {
                        _log.LogError(exception, "Simulation {SimulationId} background tick failed.", id);
                    }
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // The host is stopping; the loop ends with it.
        }
    }

    /// <summary>
    /// Starts year 1 and grants its free allocation. The allocation is granted once, here,
    /// because nothing else in the run hands it out.
    /// </summary>
    public async Task StartAsync(Guid simulationId, CancellationToken cancellationToken = default)
    {
        using IServiceScope scope = _services.CreateScope();
        SimulationRegistry runs = scope.ServiceProvider.GetRequiredService<SimulationRegistry>();
        HostedRun run = runs.Run(simulationId);
        Announcement announcement;

        await run.Gate.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            run.Clock.Start();
            run.Simulation.Ledger.GrantFreeAllocation(run.Simulation.CurrentYear);
            announcement = run.CollectAnnouncement();
        }
        finally
        {
            run.Gate.Release();
        }

        await runs.BroadcastAsync(simulationId, announcement).ConfigureAwait(false);
    }

    /// <summary>
    /// Opens the year after the last one and grants its free allocation, mirroring
    /// <see cref="StartAsync"/>. This is an explicit driver call rather than something the tick
    /// does by itself: ending a year and beginning the next are the administrator's controls, and
    /// keeping them here is what the Phase 4 admin flow will call.
    /// </summary>
    public async Task BeginNextYearAsync(Guid simulationId, CancellationToken cancellationToken = default)
    {
        using IServiceScope scope = _services.CreateScope();
        SimulationRegistry runs = scope.ServiceProvider.GetRequiredService<SimulationRegistry>();
        HostedRun run = runs.Run(simulationId);
        Announcement announcement;

        await run.Gate.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            run.Clock.BeginNextYear();
            run.Simulation.Ledger.GrantFreeAllocation(run.Simulation.CurrentYear);
            announcement = run.CollectAnnouncement();
        }
        finally
        {
            run.Gate.Release();
        }

        await runs.BroadcastAsync(simulationId, announcement).ConfigureAwait(false);
    }

    /// <summary>
    /// Closes the year: reconciles compliance, closes the books and saves the run once. The clock
    /// has to have halted trading first, and the whole thing runs once per year end, so a run that
    /// sits in <see cref="SimulationState.YearEnded"/> is not re-saved on every later tick.
    /// </summary>
    public async Task EndYearAsync(Guid simulationId, CancellationToken cancellationToken = default)
    {
        using IServiceScope scope = _services.CreateScope();
        SimulationRegistry runs = scope.ServiceProvider.GetRequiredService<SimulationRegistry>();
        ISimulationRepository store = scope.ServiceProvider.GetRequiredService<ISimulationRepository>();
        HostedRun run = runs.Run(simulationId);
        Announcement announcement;

        await run.Gate.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            run.Clock.EndYear();
            run.Simulation.Compliance.Reconcile(run.Simulation.CurrentYear);
            run.Simulation.Finance.CloseYear(run.Simulation.CurrentYear);

            await store.SaveAsync(
                SimulationSnapshots.Capture(run.Simulation, run.Clock, run.Exchange, run.Otc, run.Auctions, run.Bots),
                cancellationToken).ConfigureAwait(false);

            announcement = run.CollectAnnouncement();
        }
        finally
        {
            run.Gate.Release();
        }

        await runs.BroadcastAsync(simulationId, announcement).ConfigureAwait(false);
    }

    // ---- The administrator's run-time controls -------------------------------------------------
    //
    // Everything below drives one run the same way the tick does: through the run's gate, with
    // what changed gathered while the gate is held and broadcast after it is let go. A pending
    // run can be configured and started, a running one paused, halted or ended, and the
    // administrator's own instruments - fines, offsets, abatement, end-of-year shocks - reach the
    // engine here rather than from a component.

    /// <summary>Pauses the clock where it stands; the year does not move on.</summary>
    public Task PauseAsync(Guid simulationId, CancellationToken cancellationToken = default) =>
        MutateAsync(simulationId, (_, run) => run.Clock.Pause(), cancellationToken);

    /// <summary>Lets a paused, paused-after-auction or halted run run again.</summary>
    public Task ResumeAsync(Guid simulationId, CancellationToken cancellationToken = default) =>
        MutateAsync(simulationId, (_, run) => run.Clock.Resume(), cancellationToken);

    /// <summary>
    /// Halts trading, warning the players first when a lead is given so they can finish what they
    /// are doing; without one, trading stops at once.
    /// </summary>
    public Task RequestHaltAsync(Guid simulationId, TimeSpan? warnLead = null, CancellationToken cancellationToken = default) =>
        MutateAsync(simulationId, (_, run) => run.Clock.RequestHalt(warnLead), cancellationToken);

    /// <summary>Ends the simulation once its last year has been closed.</summary>
    public Task EndSimulationAsync(Guid simulationId, CancellationToken cancellationToken = default) =>
        MutateAsync(simulationId, (_, run) => run.Clock.EndSimulation(), cancellationToken);

    /// <summary>Imposes a fine on a unit, taking the money from its company at once.</summary>
    public Task IssueFineAsync(
        Guid simulationId,
        int unitId,
        decimal amount,
        string description,
        CancellationToken cancellationToken = default) =>
        MutateAsync(
            simulationId,
            (_, run) => run.Simulation.Compliance.IssueFine(run.Simulation.FindUnit(unitId), amount, description),
            cancellationToken);

    /// <summary>Hands a company offsets from the administrator's own book.</summary>
    public Task DisburseOffsetsAsync(
        Guid simulationId,
        int companyId,
        decimal volume,
        CancellationToken cancellationToken = default) =>
        MutateAsync(
            simulationId,
            (_, run) => run.Simulation.Ledger.Grant(run.Simulation.FindCompany(companyId), Product.Offset, volume),
            cancellationToken);

    /// <summary>Commits one of a unit's abatement projects for it, in the current year.</summary>
    public Task AddAbatementAsync(
        Guid simulationId,
        int unitId,
        int optionIndex,
        CancellationToken cancellationToken = default) =>
        MutateAsync(
            simulationId,
            (_, run) =>
            {
                Unit unit = run.Simulation.FindUnit(unitId);

                if (optionIndex < 0 || optionIndex >= unit.AbatementOptions.Count)
                {
                    throw new ArgumentOutOfRangeException(
                        nameof(optionIndex), optionIndex, $"That abatement project is not on the menu of '{unit.Name}'.");
                }

                run.Simulation.Abatements.Implement(unit, unit.AbatementOptions[optionIndex], run.Simulation.CurrentYear);
            },
            cancellationToken);

    /// <summary>
    /// An end-of-year modification: a shock to one unit's emissions in one year, applied to its
    /// business-as-usual path so every later reading of that unit sees the modified figure.
    /// </summary>
    public Task ApplyShockAsync(
        Guid simulationId,
        int unitId,
        int year,
        decimal deltaTonnes,
        CancellationToken cancellationToken = default) =>
        MutateAsync(
            simulationId,
            (_, run) => run.Simulation.Allocation.ApplyEmissionShock(run.Simulation.FindUnit(unitId), year, deltaTonnes),
            cancellationToken);

    /// <summary>
    /// Turns a unit's AutoTrade switch on or off and rebuilds the run's bot fleet, which is what
    /// makes a human unit begin (or stop) trading for itself mid-run.
    /// </summary>
    public Task SetAutoTradeAsync(
        Guid simulationId,
        int unitId,
        bool enabled,
        CancellationToken cancellationToken = default) =>
        MutateAsync(
            simulationId,
            (_, run) =>
            {
                run.Simulation.FindUnit(unitId).AutoTrade = enabled;
                SimulationRegistry.RebuildBots(run);
            },
            cancellationToken);

    /// <summary>Switches player messaging on or off for a run and tells its watchers.</summary>
    public Task SetMessagingAsync(Guid simulationId, bool enabled, CancellationToken cancellationToken = default) =>
        MutateAsync(simulationId, (_, run) => run.MessagingEnabled = enabled, cancellationToken);

    /// <summary>Creates a pending run from a scenario document and writes it to the repository.</summary>
    public async Task<Guid> CreateRunAsync(
        string scenarioJson,
        string sourceName = "admin-setup",
        CancellationToken cancellationToken = default)
    {
        Simulation simulation = ScenarioLoader.LoadJson(scenarioJson, sourceName);

        using IServiceScope scope = _services.CreateScope();
        SimulationRegistry runs = scope.ServiceProvider.GetRequiredService<SimulationRegistry>();
        ISimulationRepository store = scope.ServiceProvider.GetRequiredService<ISimulationRepository>();

        if (runs.Contains(simulation.Id))
        {
            throw new InvalidOperationException(
                $"A run built from seed {simulation.Seed} is already under way; use another seed for a second exercise.");
        }

        Guid id = runs.Start(simulation);

        await SaveRunAsync(id, cancellationToken).ConfigureAwait(false);

        return id;
    }

    /// <summary>Loads a saved run and puts it back under the host's control, markets and all.</summary>
    public async Task<Guid> LoadRunAsync(Guid simulationId, CancellationToken cancellationToken = default)
    {
        using IServiceScope scope = _services.CreateScope();
        SimulationRegistry runs = scope.ServiceProvider.GetRequiredService<SimulationRegistry>();
        ISimulationRepository store = scope.ServiceProvider.GetRequiredService<ISimulationRepository>();
        TimeProvider time = scope.ServiceProvider.GetRequiredService<TimeProvider>();

        SimulationSnapshot snapshot = await store.LoadAsync(simulationId, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"There is no saved simulation {simulationId}.");

        return runs.Restore(SimulationSnapshots.Restore(snapshot, time));
    }

    /// <summary>Takes a run off the shelf and deletes its saved copy.</summary>
    public async Task DeleteRunAsync(Guid simulationId, CancellationToken cancellationToken = default)
    {
        using IServiceScope scope = _services.CreateScope();
        SimulationRegistry runs = scope.ServiceProvider.GetRequiredService<SimulationRegistry>();
        ISimulationRepository store = scope.ServiceProvider.GetRequiredService<ISimulationRepository>();

        runs.Remove(simulationId);

        await store.DeleteAsync(simulationId, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Writes a run to the repository as it stands.</summary>
    public async Task SaveRunAsync(Guid simulationId, CancellationToken cancellationToken = default)
    {
        using IServiceScope scope = _services.CreateScope();
        SimulationRegistry runs = scope.ServiceProvider.GetRequiredService<SimulationRegistry>();
        ISimulationRepository store = scope.ServiceProvider.GetRequiredService<ISimulationRepository>();
        HostedRun run = runs.Run(simulationId);

        await run.Gate.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            await store.SaveAsync(
                SimulationSnapshots.Capture(run.Simulation, run.Clock, run.Exchange, run.Otc, run.Auctions, run.Bots),
                cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            run.Gate.Release();
        }
    }

    /// <summary>Changes one run under its gate and broadcasts what the change made visible.</summary>
    private async Task MutateAsync(
        Guid simulationId,
        Action<SimulationRegistry, HostedRun> mutate,
        CancellationToken cancellationToken)
    {
        using IServiceScope scope = _services.CreateScope();
        SimulationRegistry runs = scope.ServiceProvider.GetRequiredService<SimulationRegistry>();
        HostedRun run = runs.Run(simulationId);
        Announcement announcement;

        await run.Gate.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            mutate(runs, run);
            announcement = run.CollectAnnouncement();
        }
        finally
        {
            run.Gate.Release();
        }

        await runs.BroadcastAsync(simulationId, announcement).ConfigureAwait(false);
    }

    private static void TickOnce(HostedRun run)
    {
        if (run.Clock.State != SimulationState.Running)
        {
            return;
        }

        foreach (ClockEvent @event in run.Clock.Advance())
        {
            if (@event.Kind == ClockEventKind.AuctionClosed)
            {
                Auction auction = run.Auctions.ForSection(@event.Year, @event.Auction);
                auction.Clear();
                run.ClearedSinceAnnounced[@event] = auction;
            }

            if (@event.Kind is ClockEventKind.AuctionOpened or ClockEventKind.AuctionEndingSoon or ClockEventKind.AuctionClosed)
            {
                run.PendingNotices.Add(@event);
            }
        }

        run.Bots?.Act(run.Simulation, run.Clock, new BotMarkets(run.Exchange, run.Otc, run.Auctions));
    }
}
