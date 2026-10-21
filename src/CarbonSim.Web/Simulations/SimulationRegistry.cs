using System.Runtime.ExceptionServices;
using CarbonSim.Engine.Bots;
using CarbonSim.Engine.Clock;
using CarbonSim.Engine.Domain;
using CarbonSim.Engine.Market;
using CarbonSim.Engine.Market.Exchange;
using CarbonSim.Engine.Market.Otc;
using CarbonSim.Engine.Reporting;
using CarbonSim.Engine.Run;
using CarbonSim.Engine.Scenarios;
using CarbonSim.Engine.Snapshot;
using Microsoft.AspNetCore.SignalR;

namespace CarbonSim.Web.Simulations;

/// <summary>
/// One line in a run's feed. <paramref name="To"/> is the company the line is addressed to, or null
/// for the room: a sealed bid is the owner's business, so a notice naming one is addressed to its
/// company and only that company's screen shows it.
/// </summary>
internal sealed record ChatEntry(int Year, string From, string Text, string? To = null);

/// <summary>One change to the rules of a live run.</summary>
public sealed record RuleChange(
    int Year,
    string By,
    decimal OldFloor,
    decimal NewFloor,
    decimal OldCeiling,
    decimal NewCeiling);

/// <summary>
/// Everything the next broadcast should say, gathered while the run's gate is held. The run module
/// reports what its own state did - notices, fills, books, state and market - and the host adds the
/// one thing the engine does not know about, its messaging switch. Gathering inside the gate is what
/// stops the driver's tick and a player action racing over the same journal cursor or the same notice
/// queue; sending afterwards keeps network waits out of the lock.
/// </summary>
internal sealed record Announcement(RunResult Facts)
{
    /// <summary>The run's messaging switch, when it has moved since it was last announced.</summary>
    internal bool? Messaging { get; set; }
}

/// <summary>One live run and everything the host holds beside the engine to drive it: the clock,
/// the markets, the bots, the outbound event feed and the chat log.</summary>
public sealed class HostedRun
{
    private readonly Dictionary<string, object> _shared = new(StringComparer.Ordinal);

    private bool _announcedMessaging = true;
    private long _revision;

    internal HostedRun(SimulationRun module, string name)
    {
        Module = module;
        Name = name;
    }

    /// <summary>The run module: the engine's own object, and the only thing that plays the run.</summary>
    public SimulationRun Module { get; }

    public Simulation Simulation => Module.Simulation;

    public SimulationClock Clock => Module.Clock;

    public Exchange Exchange => Module.Exchange;

    public OtcMarket Otc => Module.Otc;

    public AuctionSchedule Auctions => Module.Auctions;

    public SimulationBots? Bots
    {
        get => Module.Bots;
        internal set => Module.ReplaceBots(value);
    }

    public string Name { get; }

    /// <summary>
    /// Whether players may post messages in this run. The administrator switches it off when the
    /// room should stop talking; it is host state rather than engine state, so a run reloaded from
    /// the repository starts with messaging on again.
    /// </summary>
    public bool MessagingEnabled { get; internal set; } = true;

    public SemaphoreSlim Gate { get; } = new(1, 1);

    /// <summary>
    /// How many times the run has changed since it was started. A screen reads this to decide
    /// whether the run-wide part of its view it cached a moment ago is still good, so the work
    /// that does not depend on who is asking is done once per change rather than once per player.
    /// </summary>
    internal long Revision => _revision;

    /// <summary>
    /// A run-wide value built once per revision and handed to every player who asks for it. The
    /// value must be immutable, because one instance is shared between players, and it must only
    /// be built while the run's gate is held - every caller of this already is. The cache empties
    /// the moment the run changes, so a screen never reads a figure from an earlier tick.
    /// </summary>
    internal T Shared<T>(string key, Func<T> build)
        where T : class
    {
        if (_shared.TryGetValue(key, out object? cached) && cached is T value)
        {
            return value;
        }

        T built = build();
        _shared[key] = built;
        SharedBuilds++;

        return built;
    }

    /// <summary>How many run-wide values this run has had to build; a cache test watches it stall.</summary>
    internal int SharedBuilds { get; private set; }

    /// <summary>
    /// Starts a new revision: the run changed, so every cached run-wide value is stale. Called by the
    /// one primitive every mutation goes through, so no path can leave a screen reading the old state.
    /// </summary>
    internal void Invalidate()
    {
        _revision++;
        _shared.Clear();
    }

    internal List<ChatEntry> Chat { get; } = [];

    /// <summary>
    /// Every change an administrator has made to this run's rules, newest last: what moved, by how
    /// much and who did it, so the trainer can re-announce it and the console can show it.
    /// </summary>
    public List<RuleChange> RuleChanges { get; } = [];

    /// <summary>
    /// The host's side of what the run module just did: the module's own facts, plus the one piece of
    /// state the engine does not hold - the messaging switch. Called with the gate held, so the
    /// announcement describes a state that cannot move under it.
    /// </summary>
    internal Announcement CollectAnnouncement(RunResult facts)
    {
        ArgumentNullException.ThrowIfNull(facts);

        Announcement announcement = new(facts);

        if (_announcedMessaging != MessagingEnabled)
        {
            _announcedMessaging = MessagingEnabled;
            announcement.Messaging = MessagingEnabled;
        }

        return announcement;
    }
}

/// <summary>
/// Where the live runs live. The hub never touches the engine directly: every player action
/// passes through here, which works out which company the caller plays, clears one connection's
/// whole bid-and-trade atom under the run's gate, and broadcasts what changed after releasing it.
/// </summary>
public sealed class SimulationRegistry
{
    private readonly Dictionary<Guid, HostedRun> _runs = [];
    private readonly TimeProvider _time;
    private readonly IHubContext<SimulationHub, ISimulationClient> _hub;
    private readonly AccountCompanyResolver _actors;

    public SimulationRegistry(
        TimeProvider time,
        IHubContext<SimulationHub, ISimulationClient> hub,
        AccountCompanyResolver actors)
    {
        _time = time ?? throw new ArgumentNullException(nameof(time));
        _hub = hub ?? throw new ArgumentNullException(nameof(hub));
        _actors = actors ?? throw new ArgumentNullException(nameof(actors));
    }

    /// <summary>Whether the host holds a run under this identifier.</summary>
    public bool Contains(Guid simulationId)
    {
        lock (_runs)
        {
            return _runs.ContainsKey(simulationId);
        }
    }

    /// <summary>The runs the host is driving, for the background service.</summary>
    public IReadOnlyList<Guid> Ids
    {
        get
        {
            lock (_runs)
            {
                return [.. _runs.Keys];
            }
        }
    }

    /// <summary>The live run under this identifier, for tests driving a run by hand.</summary>
    public HostedRun Run(Guid simulationId)
    {
        lock (_runs)
        {
            return _runs.TryGetValue(simulationId, out HostedRun? run)
                ? run
                : throw new InvalidOperationException($"There is no simulation {simulationId}.");
        }
    }

    /// <summary>Whether the host holds a run under this identifier.</summary>
    private static string Group(Guid simulationId) => SimulationGroups.For(simulationId);

    /// <summary>Starts a run from wherever its simulation is: pending, or mid-run.</summary>
    public Guid Start(Simulation simulation, bool automateHumans = false)
    {
        ArgumentNullException.ThrowIfNull(simulation);

        SimulationBots? bots = BuildBots(simulation, automateHumans);
        HostedRun run = new(SimulationRun.Create(simulation, _time, bots), simulation.Name);

        lock (_runs)
        {
            _runs[simulation.Id] = run;
        }

        return simulation.Id;
    }

    private static SimulationBots? BuildBots(Simulation simulation, bool automateHumans)
    {
        if (automateHumans)
        {
            foreach (Unit unit in simulation.Units)
            {
                unit.AutoTrade = true;
            }
        }

        SimulationBots fleet = SimulationBots.Create(simulation);

        return fleet.Bots.Count == 0 ? null : fleet;
    }

    /// <summary>
    /// Brings a run's bot fleet in line after an AutoTrade switch has changed which units are
    /// automated. Only the switched company's bot changes; every other bot keeps its progress.
    /// </summary>
    internal static void RebuildBots(HostedRun run)
    {
        SimulationBots fleet = SimulationBots.Refresh(run.Simulation, run.Bots);

        run.Bots = fleet.Bots.Count == 0 ? null : fleet;
    }

    /// <summary>
    /// Puts a run that came back from the repository under the host's wing, with the clock,
    /// markets and bots the snapshot carried rather than fresh ones. Loading a run is the one
    /// way a run can be on the shelf again after the process that started it has gone.
    /// </summary>
    public Guid Restore(RestoredSimulation restored)
    {
        ArgumentNullException.ThrowIfNull(restored);

        HostedRun run = new(
            SimulationRun.Restore(
                restored.Simulation,
                restored.Clock,
                restored.Exchange,
                restored.Otc,
                restored.Auctions,
                restored.Bots),
            restored.Simulation.Name);

        lock (_runs)
        {
            _runs[restored.Simulation.Id] = run;
        }

        return restored.Simulation.Id;
    }

    /// <summary>Takes a run off the shelf; the saved copy is the caller's to delete.</summary>
    public bool Remove(Guid simulationId)
    {
        lock (_runs)
        {
            return _runs.Remove(simulationId);
        }
    }

    /// <summary>Loads a scenario straight into a run, the way the demo host does.</summary>
    public Guid StartScenario(string json, string sourceName = "scenario", bool automateHumans = false)
    {
        return Start(ScenarioLoader.LoadJson(json, sourceName), automateHumans);
    }

    /// <summary>Bids into the open auction of the run's current section.</summary>
    public async Task BidAtAuctionAsync(Guid simulationId, string actor, int unitId, int vintage, decimal price, decimal volume)
    {
        string company = await ResolveCompanyAsync(actor).ConfigureAwait(false);

        await ApplyAsync(simulationId, run =>
        {
            Unit unit = run.Simulation.FindUnit(unitId);
            RequireOwner(company, unit);

            return run.Module.Bid(unit, vintage, price, volume);
        }).ConfigureAwait(false);
    }

    /// <summary>Withdraws one of the caller's company's bids from the open auction.</summary>
    public async Task CancelBidAsync(Guid simulationId, string actor, int bidId)
    {
        string company = await ResolveCompanyAsync(actor).ConfigureAwait(false);

        await ApplyAsync(simulationId, run =>
            run.Module.CancelBid(run.Simulation.Companies.Single(candidate => candidate.Name == company), bidId)).ConfigureAwait(false);
    }

    /// <summary>Places an exchange order and reports what became of it.</summary>
    public async Task<OrderOutcome> PlaceOrderAsync(
        Guid simulationId,
        string actor,
        int unitId,
        string product,
        string side,
        string kind,
        decimal volume,
        decimal? price = null,
        decimal? stopPrice = null,
        string fillPolicy = "AllowPartial")
    {
        string company = await ResolveCompanyAsync(actor).ConfigureAwait(false);

        RunResult facts = await ApplyAsync(simulationId, run =>
        {
            Unit unit = run.Simulation.FindUnit(unitId);
            RequireOwner(company, unit);

            return run.Module.PlaceOrder(new OrderRequest(
                unit,
                ParseProduct(product),
                ParseSide(side),
                ParseKind(kind),
                volume,
                price,
                stopPrice,
                ParseFill(fillPolicy)));
        }).ConfigureAwait(false);

        return OrderOutcome.From(facts);
    }

    /// <summary>Cancels an open order and reports the new top of book.</summary>
    public async Task CancelOrderAsync(Guid simulationId, string actor, int unitId, long orderId)
    {
        string company = await ResolveCompanyAsync(actor).ConfigureAwait(false);

        await ApplyAsync(simulationId, run =>
        {
            Unit unit = run.Simulation.FindUnit(unitId);
            RequireOwner(company, unit);

            return run.Module.CancelOrder(unit.Company, orderId);
        }).ConfigureAwait(false);
    }

    /// <summary>Sends an offer to a named unit and tells its watchers.</summary>
    public async Task<long> SendOtcOfferAsync(
        Guid simulationId,
        string actor,
        int sellerUnitId,
        int buyerUnitId,
        string product,
        decimal price,
        decimal volume)
    {
        string company = await ResolveCompanyAsync(actor).ConfigureAwait(false);

        RunResult facts = await ApplyAsync(simulationId, run =>
        {
            Unit seller = run.Simulation.FindUnit(sellerUnitId);
            Unit buyer = run.Simulation.FindUnit(buyerUnitId);
            RequireOwner(company, seller);

            return run.Module.SendOffer(seller, buyer, ParseProduct(product), price, volume);
        }).ConfigureAwait(false);

        OtcOffer offer = facts.Offer
            ?? throw new InvalidOperationException("The OTC market did not report the offer it took.");

        await _hub.Clients.Group(Group(simulationId)).OtcOfferReceived(
            offer.Id, offer.Seller.Name, offer.Buyer.Name, Describe(offer.Product), offer.Price, offer.Volume).ConfigureAwait(false);

        return offer.Id;
    }

    /// <summary>Answers an offer and tells its watchers how it was resolved.</summary>
    public async Task AnswerOtcOfferAsync(Guid simulationId, string actor, int buyerUnitId, long offerId, bool accept)
    {
        string company = await ResolveCompanyAsync(actor).ConfigureAwait(false);

        RunResult facts = await ApplyAsync(simulationId, run =>
        {
            Unit buyer = run.Simulation.FindUnit(buyerUnitId);
            RequireOwner(company, buyer);

            return run.Module.AnswerOffer(buyer, offerId, accept);
        }).ConfigureAwait(false);

        OtcOffer offer = facts.Offer
            ?? throw new InvalidOperationException("The OTC market did not report the offer it answered.");

        await _hub.Clients.Group(Group(simulationId)).OtcOfferResolved(offer.Id, offer.State.ToString()).ConfigureAwait(false);

        if (offer.State == OtcOfferState.Accepted)
        {
            await AnnounceTradeAsync(
                simulationId, "Otc", offer.Product, offer.Price, offer.Volume, offer.Buyer.Company, offer.Seller.Company).ConfigureAwait(false);
        }
    }

    /// <summary>Posts a message to everyone watching the run.</summary>
    public async Task PostMessageAsync(Guid simulationId, string actor, string text)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);

        string company = await ResolveCompanyAsync(actor).ConfigureAwait(false);
        ChatEntry? recorded = null;

        await ApplyAsync(simulationId, run =>
        {
            if (!run.MessagingEnabled)
            {
                throw new InvalidOperationException("The administrator has switched messaging off for this run.");
            }

            recorded = new ChatEntry(run.Simulation.CurrentYear, company, text.Trim());
            run.Chat.Add(recorded);

            return run.Module.Inspect();
        }).ConfigureAwait(false);

        ChatEntry entry = recorded ?? throw new InvalidOperationException("The chat message was not recorded.");

        await _hub.Clients.Group(Group(simulationId)).MessageReceived(entry.From, entry.Text, entry.Year).ConfigureAwait(false);
    }

    /// <summary>Sends everything one collection gathered, in the order a watcher should see it.</summary>
    internal async Task BroadcastAsync(Guid simulationId, Announcement announcement)
    {
        string group = Group(simulationId);

        if (announcement.Facts.State is { } state)
        {
            await _hub.Clients.Group(group).SimulationStateChanged(state.State, state.Year).ConfigureAwait(false);
        }

        foreach (RunNotice notice in announcement.Facts.Notices)
        {
            switch (notice.Kind)
            {
                case ClockEventKind.AuctionOpened:
                    await _hub.Clients.Group(group).AuctionOpened(notice.Year, notice.Sequence, notice.OfferedVolume).ConfigureAwait(false);
                    break;

                case ClockEventKind.AuctionEndingSoon:
                    await _hub.Clients.Group(group).AuctionClosing(notice.Year, notice.Sequence, notice.TimeLeft).ConfigureAwait(false);
                    break;

                case ClockEventKind.AuctionClosed when notice.Cleared is { } auction:
                    await AnnounceClearedAsync(group, auction).ConfigureAwait(false);
                    break;
            }
        }

        foreach (MarketTrade fill in announcement.Facts.Fills)
        {
            await _hub.Clients.Group(group).TradeExecuted(
                fill.Channel.ToString(),
                Describe(fill.Product),
                fill.Price,
                fill.Volume,
                fill.Buyer.Name,
                fill.Seller?.Name).ConfigureAwait(false);
        }

        foreach (BookChange book in announcement.Facts.Books)
        {
            await _hub.Clients.Group(group).OrderBookChanged(
                Describe(book.Product), book.BestBid, book.BestOffer, book.LastTrade).ConfigureAwait(false);
        }

        if (announcement.Facts.Market is { } market)
        {
            await _hub.Clients.Group(group).MarketStateChanged(
                market.State, market.Year, market.Auction, market.Open).ConfigureAwait(false);
        }

        if (announcement.Messaging is { } messaging)
        {
            await _hub.Clients.Group(group).MessagingStateChanged(messaging).ConfigureAwait(false);
        }
    }

    private async Task AnnounceClearedAsync(string group, Auction auction)
    {
        foreach (AuctionResult result in auction.Results)
        {
            await _hub.Clients.Group(group).AuctionCleared(
                auction.Year, auction.Sequence, result.ClearingPrice, result.VolumeSold, result.OfferedVolume).ConfigureAwait(false);

            // Every served bidder pays the one uniform clearing price, so that is what the feed
            // reports; award.Cost / award.Volume would only restate it with rounding applied.
            if (result.ClearingPrice is not { } clearingPrice)
            {
                continue;
            }

            foreach (AuctionAward award in result.Awards)
            {
                await _hub.Clients.Group(group).TradeExecuted(
                    nameof(TradeChannel.Auction),
                    Describe(Product.Allowance(result.Vintage)),
                    clearingPrice,
                    award.Volume,
                    award.Bid.Unit.Company.Name,
                    null).ConfigureAwait(false);
            }
        }
    }

    /// <summary>
    /// The one way a run changes. It takes the run's gate, runs the operation through the run module,
    /// collects what the run owes its watchers, invalidates the screens' run-wide cache, saves when the
    /// caller says this change has to be kept, then lets the gate go and broadcasts what it gathered.
    /// Every mutation in the host goes through here - the driver's tick, a player's order, an
    /// administrator's control - so no path can broadcast half a change, leave a cached view stale, or
    /// skip the save. A read is <see cref="ReadAsync"/>, which never moves the revision.
    /// </summary>
    internal async Task<RunResult> ApplyAsync(
        Guid simulationId,
        Func<HostedRun, RunResult> operation,
        Func<HostedRun, Task>? save = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operation);

        HostedRun run = Run(simulationId);
        RunResult facts;
        Announcement announcement;
        ExceptionDispatchInfo? saveFailed = null;

        await run.Gate.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            facts = operation(run);
            announcement = run.CollectAnnouncement(facts);

            // The run has changed, so every run-wide value the screens cached belongs to the old state.
            run.Invalidate();

            if (save is not null)
            {
                try
                {
                    await save(run).ConfigureAwait(false);
                }
                catch (Exception exception)
                {
                    // The change has happened whether or not it was saved, so the room still hears
                    // it; the caller is told the save failed once the broadcast is out.
                    saveFailed = ExceptionDispatchInfo.Capture(exception);
                }
            }
        }
        finally
        {
            run.Gate.Release();
        }

        await BroadcastAsync(simulationId, announcement).ConfigureAwait(false);
        saveFailed?.Throw();

        return facts;
    }

    internal Task AnnounceTradeAsync(
        Guid simulationId,
        string channel,
        Product product,
        decimal price,
        decimal volume,
        Company buyer,
        Company? seller)
    {
        return _hub.Clients.Group(Group(simulationId)).TradeExecuted(
            channel, Describe(product), price, volume, buyer.Name, seller?.Name);
    }

    /// <summary>
    /// Reads a run while its gate is held. A screen builds its whole view through this, so the
    /// driver cannot move the clock or clear an auction halfway through the read and leave the
    /// page showing a state that never existed.
    /// </summary>
    internal async Task<T> ReadAsync<T>(Guid simulationId, Func<HostedRun, T> read)
    {
        HostedRun run = Run(simulationId);

        await run.Gate.WaitAsync().ConfigureAwait(false);

        try
        {
            return read(run);
        }
        finally
        {
            run.Gate.Release();
        }
    }

    /// <summary>Commits a unit to one of the abatement projects on its own menu.</summary>
    public async Task ImplementAbatementAsync(Guid simulationId, string actor, int unitId, int optionIndex)
    {
        string company = await ResolveCompanyAsync(actor).ConfigureAwait(false);

        await ApplyAsync(simulationId, run =>
        {
            Unit unit = run.Simulation.FindUnit(unitId);
            RequireOwner(company, unit);

            if (optionIndex < 0 || optionIndex >= unit.AbatementOptions.Count)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(optionIndex),
                    optionIndex,
                    $"That abatement project is not on the menu of '{unit.Name}'.");
            }

            run.Simulation.Abatements.Implement(unit, unit.AbatementOptions[optionIndex], run.Simulation.CurrentYear);

            return run.Module.Inspect();
        }).ConfigureAwait(false);
    }

    /// <summary>
    /// Turns the identity a caller reached the hub with into the company it plays, through the
    /// account row it claimed at registration. An unknown, anonymous or company-less identity is
    /// refused here, so no caller without a company can reach any of the actions below.
    /// </summary>
    private async Task<string> ResolveCompanyAsync(string actor)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(actor);

        string? company = await _actors.CompanyForAsync(actor).ConfigureAwait(false);

        return string.IsNullOrWhiteSpace(company)
            ? throw new UnauthorizedAccessException("Sign in as a player who has claimed a company before acting.")
            : company;
    }

    private static void RequireOwner(string company, Unit unit)
    {
        if (!string.Equals(company, unit.Company.Name, StringComparison.Ordinal))
        {
            throw new UnauthorizedAccessException($"'{company}' does not play '{unit.Company.Name}'.");
        }
    }

    private static Product ParseProduct(string product)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(product);

        string text = product.Trim();

        if (string.Equals(text, "Offset", StringComparison.OrdinalIgnoreCase))
        {
            return Product.Offset;
        }

        if (text.StartsWith("Vintage ", StringComparison.OrdinalIgnoreCase) &&
            int.TryParse(text["Vintage ".Length..], out int vintage))
        {
            return Product.Allowance(vintage);
        }

        if (int.TryParse(text, out int bare))
        {
            return Product.Allowance(bare);
        }

        throw new ArgumentException($"Unknown product '{product}'. Name an allowance vintage or 'Offset'.", nameof(product));
    }

    internal static string Describe(Product product) =>
        product.IsOffset ? "Offset" : $"Vintage {product.Vintage}";

    private static OrderSide ParseSide(string side) =>
        side.Trim().ToLowerInvariant() switch
        {
            "buy" => OrderSide.Buy,
            "sell" => OrderSide.Sell,
            _ => throw new ArgumentException($"Unknown order side '{side}'. Use 'Buy' or 'Sell'.", nameof(side)),
        };

    private static OrderKind ParseKind(string kind) =>
        kind.Trim().ToLowerInvariant() switch
        {
            "limit" => OrderKind.Limit,
            "market" => OrderKind.Market,
            "stop" or "stoploss" or "stop-loss" => OrderKind.StopLoss,
            _ => throw new ArgumentException($"Unknown order kind '{kind}'. Use 'Limit', 'Market' or 'Stop'.", nameof(kind)),
        };

    private static FillPolicy ParseFill(string fill) =>
        fill.Trim().ToLowerInvariant() switch
        {
            "allowpartial" or "partial" => FillPolicy.AllowPartial,
            "fillorkill" or "fok" => FillPolicy.FillOrKill,
            _ => throw new ArgumentException($"Unknown fill policy '{fill}'.", nameof(fill)),
        };
}
