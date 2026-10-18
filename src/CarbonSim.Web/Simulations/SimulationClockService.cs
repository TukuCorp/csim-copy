using CarbonSim.Data.Persistence;
using CarbonSim.Engine.Bots;
using CarbonSim.Engine.Clock;
using CarbonSim.Engine.Domain;
using CarbonSim.Engine.Market;
using CarbonSim.Engine.Market.Exchange;
using CarbonSim.Engine.Run;
using CarbonSim.Engine.Scenarios;
using CarbonSim.Engine.Snapshot;
using CarbonSim.Web.Components.Shared;
using CarbonSim.Web.Resources;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Localization;
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

    /// <summary>
    /// Advances one run once: clock, auction clears, bots, then the run's own events. A failure is
    /// logged once, by the background loop; a caller driving the tick by hand gets the exception.
    /// </summary>
    public Task TickAsync(Guid simulationId, CancellationToken cancellationToken = default) =>
        ApplyAsync(simulationId, run => run.Module.Advance(), cancellationToken: cancellationToken);

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
        await ApplyAsync(simulationId, run => run.Module.Start(), cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Opens the year after the last one and grants its free allocation, mirroring
    /// <see cref="StartAsync"/>. This is an explicit driver call rather than something the tick
    /// does by itself: ending a year and beginning the next are the administrator's controls, and
    /// keeping them here is what the Phase 4 admin flow will call.
    /// </summary>
    public async Task BeginNextYearAsync(Guid simulationId, CancellationToken cancellationToken = default)
    {
        await ApplyAsync(simulationId, run => run.Module.BeginNextYear(), cancellationToken: cancellationToken).ConfigureAwait(false);
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

        // The year close is the one change a run must keep: it is the administrator's act and the
        // whole point of a save, so the primitive writes it once, with the gate still held.
        await runs.ApplyAsync(
            simulationId,
            run => run.Module.EndYear(),
            run => store.SaveAsync(
                SimulationSnapshots.Capture(run.Simulation, run.Clock, run.Exchange, run.Otc, run.Auctions, run.Bots),
                cancellationToken),
            cancellationToken).ConfigureAwait(false);
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
        ApplyAsync(simulationId, run => run.Module.Pause(), cancellationToken: cancellationToken);

    /// <summary>Lets a paused, paused-after-auction or halted run run again.</summary>
    public Task ResumeAsync(Guid simulationId, CancellationToken cancellationToken = default) =>
        ApplyAsync(simulationId, run => run.Module.Resume(), cancellationToken: cancellationToken);

    /// <summary>
    /// Halts trading, warning the players first when a lead is given so they can finish what they
    /// are doing; without one, trading stops at once.
    /// </summary>
    public Task RequestHaltAsync(Guid simulationId, TimeSpan? warnLead = null, CancellationToken cancellationToken = default) =>
        ApplyAsync(simulationId, run => run.Module.RequestHalt(warnLead), cancellationToken: cancellationToken);

    /// <summary>Ends the simulation once its last year has been closed.</summary>
    public Task EndSimulationAsync(Guid simulationId, CancellationToken cancellationToken = default) =>
        ApplyAsync(simulationId, run => run.Module.EndSimulation(), cancellationToken: cancellationToken);

    /// <summary>
    /// The regulator's change of the rules: the collar a tonne may trade between, moved mid-run.
    /// The revision the run carries is what tells every bot the ground has moved, so the market
    /// re-quotes within a tick rather than waiting for the next scheduled pass.
    /// </summary>
    public async Task ChangeRulesAsync(
        Guid simulationId,
        decimal? floor,
        decimal? ceiling,
        string by = "trainer",
        CancellationToken cancellationToken = default)
    {
        using IServiceScope scope = _services.CreateScope();
        IStringLocalizer<SharedStrings> strings = scope.ServiceProvider.GetRequiredService<IStringLocalizer<SharedStrings>>();
        IHubContext<SimulationHub, ISimulationClient> hub =
            scope.ServiceProvider.GetRequiredService<IHubContext<SimulationHub, ISimulationClient>>();

        RuleChange? change = null;
        List<ChatEntry> notices = [];

        await ApplyAsync(
            simulationId,
            run =>
            {
                // The module moves the collar, re-anchors the books and releases the bids the new band
                // cannot hold; the host records who did it and tells the room, because "who" is host
                // knowledge and the run's own facts are not.
                RunResult facts = run.Module.ChangeCollar(floor, ceiling);
                CollarChange collar = facts.Collar
                    ?? throw new InvalidOperationException("The collar change was not reported by the run.");

                change = new RuleChange(
                    collar.Year,
                    by,
                    collar.OldFloor,
                    collar.NewFloor,
                    collar.OldCeiling,
                    collar.NewCeiling);

                run.RuleChanges.Add(change);

                ChatEntry line = new(collar.Year, "Administrator", Line(strings, collar));
                run.Chat.Add(line);
                notices.Add(line);

                foreach (ReleasedBid released in collar.BidsReleased)
                {
                    // Addressed to the company that bid: the price and the volume of a sealed bid are
                    // the owner's business, and the room only hears how many came off.
                    ChatEntry notice = new(
                        collar.Year,
                        "Administrator",
                        Released(strings, collar, released),
                        released.Bid.Unit.Company.Name);

                    run.Chat.Add(notice);
                    notices.Add(notice);
                }

                return facts;
            },
            cancellationToken: cancellationToken).ConfigureAwait(false);

        // Then tell everyone: the parameters push is what an open screen listens to when the rules move
        // rather than the clock, and the feed carries the line and a notice for every bid released.
        if (change is null)
        {
            throw new InvalidOperationException("The collar change was not recorded.");
        }

        string group = SimulationGroups.For(simulationId);

        await hub.Clients.Group(group).ParametersChanged(cancellationToken).ConfigureAwait(false);

        foreach (ChatEntry notice in notices.Where(notice => notice.To is null))
        {
            await hub.Clients.Group(group)
                .MessageReceived(notice.From, notice.Text, notice.Year, cancellationToken)
                .ConfigureAwait(false);
        }
    }

    /// <summary>The trainer's line: what moved, and what it took off the books, in both languages.</summary>
    private static string Line(IStringLocalizer<SharedStrings> strings, CollarChange collar)
    {
        string line = Bilingual.Both(
            strings,
            "RulesChangedLine",
            Display.Price(collar.OldFloor),
            Display.Price(collar.NewFloor),
            Display.Price(collar.OldCeiling),
            Display.Price(collar.NewCeiling));

        if (collar.OrdersReleased > 0)
        {
            line += " " + Bilingual.Both(strings, "RulesReleasedOrders", collar.OrdersReleased);
        }

        if (collar.BidsReleased.Count > 0)
        {
            line += " " + Bilingual.Both(strings, "RulesReleasedBids", collar.BidsReleased.Count);
        }

        return line;
    }

    /// <summary>What the owner of a released bid is told: which bid, and where the collar now stands.</summary>
    private static string Released(IStringLocalizer<SharedStrings> strings, CollarChange collar, ReleasedBid released) =>
        Bilingual.Both(
            strings,
            "BidReleased",
            Display.Tonnes(released.Bid.Volume),
            Display.Price(released.Bid.Price),
            released.Sequence,
            collar.Year,
            Display.Price(collar.NewFloor),
            Display.Price(collar.NewCeiling));

    /// <summary>Imposes a fine on a unit, taking the money from its company at once.</summary>
    public Task IssueFineAsync(
        Guid simulationId,
        int unitId,
        decimal amount,
        string description,
        CancellationToken cancellationToken = default) =>
        ApplyAsync(
            simulationId,
            run =>
            {
                run.Simulation.Compliance.IssueFine(run.Simulation.FindUnit(unitId), amount, description);

                return run.Module.Inspect();
            },
            cancellationToken: cancellationToken);

    /// <summary>Hands a company offsets from the administrator's own book.</summary>
    public Task DisburseOffsetsAsync(
        Guid simulationId,
        int companyId,
        decimal volume,
        CancellationToken cancellationToken = default) =>
        ApplyAsync(
            simulationId,
            run =>
            {
                run.Simulation.Ledger.Grant(run.Simulation.FindCompany(companyId), Product.Offset, volume);

                return run.Module.Inspect();
            },
            cancellationToken: cancellationToken);

    /// <summary>Commits one of a unit's abatement projects for it, in the current year.</summary>
    public Task AddAbatementAsync(
        Guid simulationId,
        int unitId,
        int optionIndex,
        CancellationToken cancellationToken = default) =>
        ApplyAsync(
            simulationId,
            run =>
            {
                Unit unit = run.Simulation.FindUnit(unitId);

                if (optionIndex < 0 || optionIndex >= unit.AbatementOptions.Count)
                {
                    throw new ArgumentOutOfRangeException(
                        nameof(optionIndex), optionIndex, $"That abatement project is not on the menu of '{unit.Name}'.");
                }

                run.Simulation.Abatements.Implement(unit, unit.AbatementOptions[optionIndex], run.Simulation.CurrentYear);

                return run.Module.Inspect();
            },
            cancellationToken: cancellationToken);

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
        ApplyAsync(
            simulationId,
            run =>
            {
                run.Simulation.Allocation.ApplyEmissionShock(run.Simulation.FindUnit(unitId), year, deltaTonnes);

                return run.Module.Inspect();
            },
            cancellationToken: cancellationToken);

    /// <summary>
    /// Turns a unit's AutoTrade switch on or off and refreshes that company's bot, which is what
    /// makes a human unit begin (or stop) trading for itself mid-run.
    /// </summary>
    public Task SetAutoTradeAsync(
        Guid simulationId,
        int unitId,
        bool enabled,
        CancellationToken cancellationToken = default) =>
        ApplyAsync(
            simulationId,
            run =>
            {
                run.Simulation.FindUnit(unitId).AutoTrade = enabled;
                SimulationRegistry.RebuildBots(run);

                return run.Module.Inspect();
            },
            cancellationToken: cancellationToken);

    /// <summary>Switches player messaging on or off for a run and tells its watchers.</summary>
    public Task SetMessagingAsync(Guid simulationId, bool enabled, CancellationToken cancellationToken = default) =>
        ApplyAsync(
            simulationId,
            run =>
            {
                run.MessagingEnabled = enabled;

                return run.Module.Inspect();
            },
            cancellationToken: cancellationToken);

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

        // The saved copy dates from creation or the last year end, so loading it over a live run would
        // throw away everything played since. Loading is for bringing a run back after a restart.
        if (runs.Contains(simulationId))
        {
            throw new InvalidOperationException("This exercise is already running; loading its saved copy would lose the play since it was saved.");
        }

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

    /// <summary>
    /// The driver's handle on the host's one primitive: it opens a scope for the registry - the
    /// registry is a singleton and so is the driver, so neither may hold a scoped service - and hands
    /// the operation straight on. Every control in this file is one call into the run module through
    /// here, which is what keeps the tick, the year close and the administrator's acts one protocol.
    /// </summary>
    private async Task ApplyAsync(
        Guid simulationId,
        Func<HostedRun, RunResult> operation,
        Func<HostedRun, Task>? save = null,
        CancellationToken cancellationToken = default)
    {
        using IServiceScope scope = _services.CreateScope();
        SimulationRegistry runs = scope.ServiceProvider.GetRequiredService<SimulationRegistry>();

        await runs.ApplyAsync(simulationId, operation, save, cancellationToken).ConfigureAwait(false);
    }
}
