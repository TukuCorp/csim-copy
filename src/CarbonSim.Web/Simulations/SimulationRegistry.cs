using CarbonSim.Engine.Bots;
using CarbonSim.Engine.Clock;
using CarbonSim.Engine.Domain;
using CarbonSim.Engine.Market;
using CarbonSim.Engine.Market.Exchange;
using CarbonSim.Engine.Market.Otc;
using CarbonSim.Engine.Reporting;
using CarbonSim.Engine.Scenarios;
using CarbonSim.Engine.Snapshot;
using Microsoft.AspNetCore.SignalR;

namespace CarbonSim.Web.Simulations;

internal sealed record ChatEntry(int Year, string From, string Text);

/// <summary>A clock notice ready to go out, with the figures the run had when it came due.</summary>
internal sealed record NoticeAnnouncement(
    ClockEventKind Kind,
    int Year,
    int Sequence,
    decimal OfferedVolume = 0m,
    TimeSpan TimeLeft = default,
    Auction? Auction = null);

/// <summary>One order book as it stood when the run's gate was held.</summary>
internal sealed record BookAnnouncement(Product Product, decimal? BestBid, decimal? BestOffer, decimal LastTrade);

/// <summary>
/// Everything the next broadcast should say, gathered while the run's gate is held. Gathering
/// inside the gate is what stops the driver's tick and a player action racing over the same
/// journal cursor or the same notice queue; sending afterwards keeps network waits out of the lock.
/// </summary>
internal sealed class Announcement
{
    internal List<NoticeAnnouncement> Notices { get; } = [];

    internal List<MarketTrade> Fills { get; } = [];

    internal List<BookAnnouncement> Books { get; } = [];

    internal (string State, int Year)? State { get; set; }

    internal (string State, int Year, int Auction, bool Open)? Market { get; set; }

    /// <summary>The run's messaging switch, when it has moved since it was last announced.</summary>
    internal bool? Messaging { get; set; }
}

/// <summary>One live run and everything the host holds beside the engine to drive it: the clock,
/// the markets, the bots, the outbound event feed and the chat log.</summary>
public sealed class HostedRun
{
    private readonly Dictionary<Product, BookAnnouncement> _announcedBooks = [];

    private (string State, int Year)? _announcedState;
    private (string State, int Year, int Auction, bool Open)? _announcedMarket;
    private bool _announcedMessaging = true;

    internal HostedRun(
        Simulation simulation,
        SimulationClock clock,
        Exchange exchange,
        OtcMarket otc,
        AuctionSchedule auctions,
        SimulationBots? bots,
        string name)
    {
        Simulation = simulation;
        Clock = clock;
        Exchange = exchange;
        Otc = otc;
        Auctions = auctions;
        Bots = bots;
        Name = name;
    }

    public Simulation Simulation { get; }

    public SimulationClock Clock { get; }

    public Exchange Exchange { get; }

    public OtcMarket Otc { get; }

    public AuctionSchedule Auctions { get; }

    public SimulationBots? Bots { get; internal set; }

    public string Name { get; }

    /// <summary>
    /// Whether players may post messages in this run. The administrator switches it off when the
    /// room should stop talking; it is host state rather than engine state, so a run reloaded from
    /// the repository starts with messaging on again.
    /// </summary>
    public bool MessagingEnabled { get; internal set; } = true;

    public SemaphoreSlim Gate { get; } = new(1, 1);

    /// <summary>Clock notices not yet broadcast, in event order; cleared auctions ride with their close notice.</summary>
    public List<ClockEvent> PendingNotices { get; } = [];

    /// <summary>Auctions cleared since the last broadcast, keyed by their close notice; drained by the announce step.</summary>
    public Dictionary<ClockEvent, Auction> ClearedSinceAnnounced { get; } = [];

    /// <summary>Journal trades handed to the feed so far; the next collection reports what follows.</summary>
    public long JournalSeen { get; set; }

    internal List<ChatEntry> Chat { get; } = [];

    /// <summary>
    /// Drains what the feed owes its watchers and moves the journal cursor on. Called with the
    /// gate held: the one cursor, advanced once per fill, is the single source of TradeExecuted
    /// for exchange fills, so the driver's tick and a player action can neither both report a fill
    /// nor skip one. It describes only exchanges, because the auction and OTC paths report their
    /// own trades from what they already know.
    /// </summary>
    internal Announcement CollectAnnouncement()
    {
        Announcement announcement = new();

        foreach (ClockEvent notice in PendingNotices)
        {
            switch (notice.Kind)
            {
                case ClockEventKind.AuctionOpened:
                    announcement.Notices.Add(new NoticeAnnouncement(
                        notice.Kind,
                        notice.Year,
                        notice.Auction,
                        OfferedVolume: Auctions.ForSection(notice.Year, notice.Auction).OfferedVolume));
                    break;

                case ClockEventKind.AuctionEndingSoon:
                    announcement.Notices.Add(new NoticeAnnouncement(
                        notice.Kind, notice.Year, notice.Auction, TimeLeft: Clock.TimeToAuctionClose));
                    break;

                case ClockEventKind.AuctionClosed when ClearedSinceAnnounced.TryGetValue(notice, out Auction? cleared):
                    announcement.Notices.Add(new NoticeAnnouncement(
                        notice.Kind, notice.Year, notice.Auction, Auction: cleared));
                    break;
            }
        }

        PendingNotices.Clear();
        ClearedSinceAnnounced.Clear();

        IReadOnlyList<MarketTrade> trades = Simulation.Journal.Trades;

        for (int index = (int)JournalSeen; index < trades.Count; index++)
        {
            if (trades[index].Channel == TradeChannel.Exchange)
            {
                announcement.Fills.Add(trades[index]);
            }
        }

        JournalSeen = trades.Count;

        foreach (Product product in Exchange.Products)
        {
            if (ChangedBook(product) is { } changed)
            {
                announcement.Books.Add(changed);
            }
        }

        (string State, int Year) state = (Simulation.State.ToString(), Simulation.CurrentYear);

        if (_announcedState != state)
        {
            _announcedState = state;
            announcement.State = state;
        }

        (string State, int Year, int Auction, bool Open) market =
            (Simulation.State.ToString(), Simulation.CurrentYear, Clock.CurrentAuction, Clock.IsAuctionOpen);

        if (_announcedMarket != market)
        {
            _announcedMarket = market;
            announcement.Market = market;
        }

        if (_announcedMessaging != MessagingEnabled)
        {
            _announcedMessaging = MessagingEnabled;
            announcement.Messaging = MessagingEnabled;
        }

        return announcement;
    }

    /// <summary>The book summary if it has moved since it was last announced, otherwise null.</summary>
    private BookAnnouncement? ChangedBook(Product product)
    {
        OrderBook book = Exchange.Book(product);
        BookAnnouncement current = new(product, book.BestBid, book.BestOffer, book.LastTradePrice);

        if (_announcedBooks.TryGetValue(product, out BookAnnouncement? announced) && announced == current)
        {
            return null;
        }

        _announcedBooks[product] = current;

        return current;
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

        SimulationClock clock = new(simulation, _time);
        Exchange exchange = new(simulation);
        OtcMarket otc = new(simulation);
        AuctionSchedule auctions = AuctionSchedule.Build(simulation);
        SimulationBots? bots = BuildBots(simulation, automateHumans);
        HostedRun run = new(simulation, clock, exchange, otc, auctions, bots, simulation.Name);

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
    /// Replaces a run's bot fleet after an AutoTrade switch has changed which units are
    /// automated. The fleet is rebuilt from scratch, so a bot's own progress is dropped and the
    /// trigger times are drawn again; an administrator flipping the switch accepts that.
    /// </summary>
    internal static void RebuildBots(HostedRun run)
    {
        SimulationBots fleet = SimulationBots.Create(run.Simulation);

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
            restored.Simulation,
            restored.Clock,
            restored.Exchange,
            restored.Otc,
            restored.Auctions,
            restored.Bots,
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

        await MutateAndAnnounceAsync(simulationId, run =>
        {
            Unit unit = run.Simulation.FindUnit(unitId);
            RequireOwner(company, unit);

            Auction auction = run.Auctions.ForSection(run.Simulation.CurrentYear, run.Clock.CurrentAuction);
            auction.PlaceBid(unit, vintage, price, volume);
        }).ConfigureAwait(false);
    }

    /// <summary>Places an exchange order and reports the new top of book.</summary>
    public async Task<long> PlaceOrderAsync(
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

        Order placed = await MutateAndAnnounceAsync(simulationId, run =>
        {
            Unit unit = run.Simulation.FindUnit(unitId);
            RequireOwner(company, unit);

            return run.Exchange.Place(new OrderRequest(
                unit,
                ParseProduct(product),
                ParseSide(side),
                ParseKind(kind),
                volume,
                price,
                stopPrice,
                ParseFill(fillPolicy)));
        }).ConfigureAwait(false);

        return placed.Id;
    }

    /// <summary>Cancels an open order and reports the new top of book.</summary>
    public async Task CancelOrderAsync(Guid simulationId, string actor, int unitId, long orderId)
    {
        string company = await ResolveCompanyAsync(actor).ConfigureAwait(false);

        await MutateAndAnnounceAsync(simulationId, run =>
        {
            Unit unit = run.Simulation.FindUnit(unitId);
            RequireOwner(company, unit);

            run.Exchange.Cancel(unit.Company, orderId);
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

        OtcOffer offer = await MutateAndAnnounceAsync(simulationId, run =>
        {
            Unit seller = run.Simulation.FindUnit(sellerUnitId);
            Unit buyer = run.Simulation.FindUnit(buyerUnitId);
            RequireOwner(company, seller);

            return run.Otc.Send(seller, buyer, ParseProduct(product), price, volume);
        }).ConfigureAwait(false);

        await _hub.Clients.Group(Group(simulationId)).OtcOfferReceived(
            offer.Id, offer.Seller.Name, offer.Buyer.Name, Describe(offer.Product), offer.Price, offer.Volume).ConfigureAwait(false);

        return offer.Id;
    }

    /// <summary>Answers an offer and tells its watchers how it was resolved.</summary>
    public async Task AnswerOtcOfferAsync(Guid simulationId, string actor, int buyerUnitId, long offerId, bool accept)
    {
        string company = await ResolveCompanyAsync(actor).ConfigureAwait(false);

        OtcOffer offer = await MutateAndAnnounceAsync(simulationId, run =>
        {
            Unit buyer = run.Simulation.FindUnit(buyerUnitId);
            RequireOwner(company, buyer);

            return accept ? run.Otc.Accept(buyer, offerId) : run.Otc.Reject(buyer, offerId);
        }).ConfigureAwait(false);

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
        HostedRun run = Run(simulationId);
        ChatEntry entry;

        await run.Gate.WaitAsync().ConfigureAwait(false);

        try
        {
            if (!run.MessagingEnabled)
            {
                throw new InvalidOperationException("The administrator has switched messaging off for this run.");
            }

            entry = new ChatEntry(run.Simulation.CurrentYear, company, text.Trim());
            run.Chat.Add(entry);
        }
        finally
        {
            run.Gate.Release();
        }

        await _hub.Clients.Group(Group(simulationId)).MessageReceived(entry.From, entry.Text, entry.Year).ConfigureAwait(false);
    }

    /// <summary>Sends everything one collection gathered, in the order a watcher should see it.</summary>
    internal async Task BroadcastAsync(Guid simulationId, Announcement announcement)
    {
        string group = Group(simulationId);

        if (announcement.State is { } state)
        {
            await _hub.Clients.Group(group).SimulationStateChanged(state.State, state.Year).ConfigureAwait(false);
        }

        foreach (NoticeAnnouncement notice in announcement.Notices)
        {
            switch (notice.Kind)
            {
                case ClockEventKind.AuctionOpened:
                    await _hub.Clients.Group(group).AuctionOpened(notice.Year, notice.Sequence, notice.OfferedVolume).ConfigureAwait(false);
                    break;

                case ClockEventKind.AuctionEndingSoon:
                    await _hub.Clients.Group(group).AuctionClosing(notice.Year, notice.Sequence, notice.TimeLeft).ConfigureAwait(false);
                    break;

                case ClockEventKind.AuctionClosed when notice.Auction is { } auction:
                    await AnnounceClearedAsync(group, auction).ConfigureAwait(false);
                    break;
            }
        }

        foreach (MarketTrade fill in announcement.Fills)
        {
            await _hub.Clients.Group(group).TradeExecuted(
                fill.Channel.ToString(),
                Describe(fill.Product),
                fill.Price,
                fill.Volume,
                fill.Buyer.Name,
                fill.Seller?.Name).ConfigureAwait(false);
        }

        foreach (BookAnnouncement book in announcement.Books)
        {
            await _hub.Clients.Group(group).OrderBookChanged(
                Describe(book.Product), book.BestBid, book.BestOffer, book.LastTrade).ConfigureAwait(false);
        }

        if (announcement.Market is { } market)
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

    internal async Task<T> MutateAsync<T>(Guid simulationId, Func<HostedRun, T> mutate)
    {
        HostedRun run = Run(simulationId);

        await run.Gate.WaitAsync().ConfigureAwait(false);

        try
        {
            return mutate(run);
        }
        finally
        {
            run.Gate.Release();
        }
    }

    internal async Task MutateAsync(Guid simulationId, Action<HostedRun> mutate)
    {
        HostedRun run = Run(simulationId);

        await run.Gate.WaitAsync().ConfigureAwait(false);

        try
        {
            mutate(run);
        }
        finally
        {
            run.Gate.Release();
        }
    }

    /// <summary>
    /// Changes one run and gathers what changed while the gate is still held, then broadcasts
    /// after letting go: a player action can neither read the journal while the driver is writing
    /// it nor hold the lock across a network wait.
    /// </summary>
    internal async Task MutateAndAnnounceAsync(Guid simulationId, Action<HostedRun> mutate)
    {
        Announcement announcement = await MutateAsync(simulationId, run =>
        {
            mutate(run);

            return run.CollectAnnouncement();
        }).ConfigureAwait(false);

        await BroadcastAsync(simulationId, announcement).ConfigureAwait(false);
    }

    internal async Task<T> MutateAndAnnounceAsync<T>(Guid simulationId, Func<HostedRun, T> mutate)
    {
        (T Result, Announcement Announcement) outcome = await MutateAsync(simulationId, run =>
        {
            T result = mutate(run);

            return (Result: result, Announcement: run.CollectAnnouncement());
        }).ConfigureAwait(false);

        await BroadcastAsync(simulationId, outcome.Announcement).ConfigureAwait(false);

        return outcome.Result;
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

        await MutateAndAnnounceAsync(simulationId, run =>
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
