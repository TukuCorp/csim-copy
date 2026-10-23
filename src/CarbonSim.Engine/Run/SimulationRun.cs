using CarbonSim.Engine.Bots;
using CarbonSim.Engine.Clock;
using CarbonSim.Engine.Domain;
using CarbonSim.Engine.Market;
using CarbonSim.Engine.Market.Exchange;
using CarbonSim.Engine.Market.Otc;
using CarbonSim.Engine.Reporting;

namespace CarbonSim.Engine.Run;

/// <summary>
/// One played simulation, and the order of operations that plays it: the tick and the auctions it
/// closes, the bots' turn, the year close, the administrator's change of the collar, and the market
/// actions a player can take. The host drives a run through these verbs and broadcasts what they
/// return; the test loops call the same verbs, so a measured run and a played run are the same run
/// by construction. There are no threads and no locks here - the host's gate is the host's business -
/// and nothing outside this file needs the queues the feed is built from.
/// </summary>
public sealed class SimulationRun
{
    private readonly List<ClockEvent> _pendingNotices = [];
    private readonly Dictionary<ClockEvent, Auction> _clearedSinceAnnounced = [];
    private readonly Dictionary<Product, BookChange> _announcedBooks = [];

    private (string State, int Year)? _announcedState;
    private (string State, int Year, int Auction, bool Open)? _announcedMarket;
    private long _journalSeen;

    private SimulationRun(
        Simulation simulation,
        SimulationClock clock,
        Exchange exchange,
        OtcMarket otc,
        AuctionSchedule auctions,
        SimulationBots? bots)
    {
        Simulation = simulation;
        Clock = clock;
        Exchange = exchange;
        Otc = otc;
        Auctions = auctions;
        Bots = bots;
    }

    public Simulation Simulation { get; }

    public SimulationClock Clock { get; }

    public Exchange Exchange { get; }

    public OtcMarket Otc { get; }

    public AuctionSchedule Auctions { get; }

    /// <summary>The bot fleet, or null for a run with nothing automated.</summary>
    public SimulationBots? Bots { get; private set; }

    /// <summary>
    /// A run ready to play: its own clock over the injected time, its markets, and the auctions the
    /// run's own settings lay out. The fleet is built by the caller, because who is automated is a
    /// decision the host makes (the administrator's AutoTrade switches).
    /// </summary>
    public static SimulationRun Create(Simulation simulation, TimeProvider timeProvider, SimulationBots? bots = null)
    {
        ArgumentNullException.ThrowIfNull(simulation);
        ArgumentNullException.ThrowIfNull(timeProvider);

        return new SimulationRun(
            simulation,
            new SimulationClock(simulation, timeProvider),
            new Exchange(simulation),
            new OtcMarket(simulation),
            AuctionSchedule.Build(simulation),
            bots);
    }

    /// <summary>
    /// Puts a run back on the pieces a snapshot restored rather than fresh ones: a reload must not
    /// rebuild auctions (they would take their lots out of the government's reserve a second time)
    /// and must keep the clock's own position.
    /// </summary>
    public static SimulationRun Restore(
        Simulation simulation,
        SimulationClock clock,
        Exchange exchange,
        OtcMarket otc,
        AuctionSchedule auctions,
        SimulationBots? bots)
    {
        ArgumentNullException.ThrowIfNull(simulation);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(exchange);
        ArgumentNullException.ThrowIfNull(otc);
        ArgumentNullException.ThrowIfNull(auctions);

        return new SimulationRun(simulation, clock, exchange, otc, auctions, bots);
    }

    /// <summary>Swaps the fleet after the AutoTrade switches changed which units are automated.</summary>
    public void ReplaceBots(SimulationBots? bots) => Bots = bots;

    // ---- the year -------------------------------------------------------------------------------

    /// <summary>
    /// Begins year 1 and grants its free allocation. The allocation is granted here because nothing
    /// else in a run hands it out, and a year that has not been granted it is a year nobody can
    /// comply in.
    /// </summary>
    public RunResult Start()
    {
        Clock.Start();
        Simulation.Ledger.GrantFreeAllocation(Simulation.CurrentYear);

        return Collect();
    }

    /// <summary>Opens the year after the last one and grants its free allocation.</summary>
    public RunResult BeginNextYear()
    {
        Clock.BeginNextYear();
        Simulation.Ledger.GrantFreeAllocation(Simulation.CurrentYear);

        return Collect();
    }

    /// <summary>Pauses the clock where it stands; the year does not move on.</summary>
    public RunResult Pause()
    {
        Clock.Pause();

        return Collect();
    }

    /// <summary>Lets a paused, paused-after-auction or halted run run again.</summary>
    public RunResult Resume()
    {
        Clock.Resume();

        return Collect();
    }

    /// <summary>Halts trading, warning the players first when a lead is given.</summary>
    public RunResult RequestHalt(TimeSpan? warnLead = null)
    {
        CancelOnHalt(Clock.RequestHalt(warnLead));

        return Collect();
    }

    /// <summary>Ends the simulation once its last year has been closed.</summary>
    public RunResult EndSimulation()
    {
        Clock.EndSimulation();

        return Collect();
    }

    /// <summary>
    /// Advances the run once: the clock moves, every auction it closed is cleared, and then the bots
    /// take their turn on the market as it now stands. The order matters and is why this is one
    /// operation rather than three: a bot must quote against a book whose closed auction has already
    /// cleared, and a loop that cleared after the bots would trade a different run.
    /// </summary>
    public RunResult Advance()
    {
        List<BotAction> actions = [];

        if (Clock.State == SimulationState.Running)
        {
            IReadOnlyList<ClockEvent> events = Clock.Advance();

            foreach (ClockEvent @event in events)
            {
                if (@event.Kind == ClockEventKind.AuctionClosed)
                {
                    Auction auction = Auctions.ForSection(@event.Year, @event.Auction);
                    auction.Clear();
                    _clearedSinceAnnounced[@event] = auction;
                }

                if (@event.Kind is ClockEventKind.AuctionOpened or ClockEventKind.AuctionEndingSoon or ClockEventKind.AuctionClosed)
                {
                    _pendingNotices.Add(@event);
                }
            }

            CancelOnHalt(events);
            actions.AddRange(Bots?.Act(Simulation, Clock, new BotMarkets(Exchange, Otc, Auctions)) ?? []);
        }

        return Collect(actions);
    }

    /// <summary>
    /// Closes the year: the clock ends it, the auction window that is still open is cleared first so
    /// the bids the room already committed are honoured rather than silently thrown away, any auction
    /// the year never reached is cleared empty, and then compliance is reconciled and the books are
    /// closed. The clock has to have halted trading
    /// already, which is the administrator's step and not this one's.
    /// </summary>
    public RunResult EndYear()
    {
        Clock.EndYear();

        Auction open = Auctions.ForSection(Simulation.CurrentYear, Clock.CurrentAuction);

        if (!open.IsCleared && open.Bids.Count > 0)
        {
            open.Clear();

            ClockEvent closed = new(
                ClockEventKind.AuctionClosed,
                Simulation.CurrentYear,
                Clock.CurrentAuction,
                Clock.ElapsedInYear);

            _clearedSinceAnnounced[closed] = open;
            _pendingNotices.Add(closed);
        }

        // A year halted early never reached its later windows. Their lots were drawn from the
        // government when the schedule was built, so clearing them (unbid) puts the volume back in
        // the reserve rather than leaving it in auctions that will never run.
        foreach (Auction unrun in Auctions.Auctions.Where(auction => auction.Year == Simulation.CurrentYear && !auction.IsCleared))
        {
            unrun.Clear();
        }

        Simulation.Compliance.Reconcile(Simulation.CurrentYear);
        Simulation.Finance.CloseYear(Simulation.CurrentYear);

        return Collect();
    }

    /// <summary>
    /// The administrator's change of the collar. The collar bounds the auctions only, so the exchange
    /// keeps trading in its own band around its own last trade; bids resting in an auction that the
    /// new collar cannot hold are released. The revision the run carries is what tells the bots the
    /// ground has moved, so the market re-quotes within a tick rather than at the next scheduled pass.
    /// </summary>
    public RunResult ChangeCollar(decimal? floor, decimal? ceiling)
    {
        Parameters rules = Simulation.Rules;
        decimal oldFloor = rules.AuctionFloorPrice;
        decimal oldCeiling = rules.AuctionCeilingPrice;
        decimal newFloor = floor ?? oldFloor;
        decimal newCeiling = ceiling ?? oldCeiling;

        if (newFloor <= 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(floor), newFloor, "An auction floor has to be greater than zero.");
        }

        if (newCeiling <= newFloor)
        {
            throw new ArgumentOutOfRangeException(
                nameof(ceiling),
                newCeiling,
                $"The ceiling ({newCeiling}) has to be above the floor ({newFloor}).");
        }

        rules.AuctionFloorPrice = newFloor;
        rules.AuctionCeilingPrice = newCeiling;
        Simulation.RulesRevision++;

        // The auction's own book follows the collar too: a bid the new band cannot hold comes off it,
        // with the money back to the company, so nothing is left resting that could not be cleared.
        List<ReleasedBid> releasedBids = [];

        foreach (Auction auction in Auctions.Auctions.Where(auction => !auction.IsCleared))
        {
            releasedBids.AddRange(auction.ReleaseOutsideTheCollar().Select(bid => new ReleasedBid(auction.Year, auction.Sequence, bid)));
        }

        return Collect() with
        {
            Collar = new CollarChange(
                Simulation.CurrentYear,
                oldFloor,
                newFloor,
                oldCeiling,
                newCeiling,
                releasedBids),
        };
    }

    /// <summary>
    /// The trainer opening or closing a market, or the leaderboard, while the run is under way; a null
    /// leaves that switch as it is. A market that closes keeps nothing resting: its orders or offers
    /// are cancelled with what they had set aside handed back, as a halt does, because nobody could
    /// reach them to cancel them until it reopens.
    /// </summary>
    public RunResult SetMarketAccess(bool? exchange, bool? otc, bool? leaderboard)
    {
        Parameters rules = Simulation.Rules;

        if (exchange is { } exchangeOpen)
        {
            if (rules.ExchangeAccess && !exchangeOpen)
            {
                Exchange.CancelAll();
            }

            rules.ExchangeAccess = exchangeOpen;
        }

        if (otc is { } otcOpen)
        {
            if (rules.OtcAccess && !otcOpen)
            {
                Otc.CancelPending();
            }

            rules.OtcAccess = otcOpen;
        }

        rules.LeaderboardVisible = leaderboard ?? rules.LeaderboardVisible;
        Simulation.RulesRevision++;

        return Collect();
    }

    /// <summary>
    /// A halt cancels every resting exchange order and every OTC offer still waiting for an answer,
    /// as the original does ("Cancelled - Trading halted"). Whatever they had set aside goes back to
    /// its owner, so the year end surrenders and banks on everything a company holds.
    /// </summary>
    private void CancelOnHalt(IReadOnlyList<ClockEvent> events)
    {
        if (events.Any(@event => @event.Kind == ClockEventKind.TradingHalted))
        {
            Exchange.CancelAll();
            Otc.CancelPending();
        }
    }

    // ---- the market seam ------------------------------------------------------------------------

    /// <summary>Places a sealed bid in the section whose window is open.</summary>
    public RunResult Bid(Unit unit, int vintage, decimal price, decimal volume)
    {
        ArgumentNullException.ThrowIfNull(unit);

        TradingWindow.RequireAuctionOpen(Clock);
        Auctions.ForSection(Simulation.CurrentYear, Clock.CurrentAuction).PlaceBid(unit, vintage, price, volume);

        return Collect();
    }

    /// <summary>Withdraws one of the company's bids from the auction whose window is open.</summary>
    public RunResult CancelBid(Company company, int bidId)
    {
        ArgumentNullException.ThrowIfNull(company);

        TradingWindow.RequireAuctionOpen(Clock);
        Auctions.ForSection(Simulation.CurrentYear, Clock.CurrentAuction).CancelBid(company, bidId);

        return Collect();
    }

    /// <summary>Places an exchange order; the book refuses one outside a running year.</summary>
    public RunResult PlaceOrder(OrderRequest request)
    {
        Order placed = Exchange.Place(request);

        return Collect() with { Order = placed };
    }

    /// <summary>Withdraws an order, which is allowed in any state.</summary>
    public RunResult CancelOrder(Company company, long orderId)
    {
        Order cancelled = Exchange.Cancel(company, orderId);

        return Collect() with { Order = cancelled };
    }

    /// <summary>Sends an OTC offer to a named unit; refused outside a running year.</summary>
    public RunResult SendOffer(Unit seller, Unit buyer, Product product, decimal price, decimal volume)
    {
        OtcOffer offer = Otc.Send(seller, buyer, product, price, volume);

        return Collect() with { Offer = offer };
    }

    /// <summary>Answers an OTC offer: accepting needs a running year, turning it down does not.</summary>
    public RunResult AnswerOffer(Unit buyer, long offerId, bool accept)
    {
        OtcOffer offer = accept ? Otc.Accept(buyer, offerId) : Otc.Reject(buyer, offerId);

        return Collect() with { Offer = offer };
    }

    /// <summary>The seller withdraws an offer that has not been answered; allowed in any state.</summary>
    public RunResult WithdrawOffer(Unit seller, long offerId)
    {
        OtcOffer offer = Otc.Cancel(seller, offerId);

        return Collect() with { Offer = offer };
    }

    /// <summary>
    /// What the feed owes, without changing the run. A host-only change - a chat message, a save -
    /// still owes the watchers whatever the last operation left unsaid, and this is how it asks.
    /// </summary>
    public RunResult Inspect() => Collect();

    // ---- what the feed owes ---------------------------------------------------------------------

    /// <summary>
    /// Everything that has become true since the last operation reported it: the notices the clock
    /// raised (with the figures the run had when they came due), the exchange fills that followed the
    /// journal cursor, the books whose top has moved, and the run's own state and market when they
    /// have changed. Called at the end of every verb, so no caller has to remember to drain anything.
    /// </summary>
    private RunResult Collect(IReadOnlyList<BotAction>? botActions = null)
    {
        List<RunNotice> notices = [];

        foreach (ClockEvent notice in _pendingNotices)
        {
            switch (notice.Kind)
            {
                case ClockEventKind.AuctionOpened:
                    notices.Add(new RunNotice(
                        notice.Kind,
                        notice.Year,
                        notice.Auction,
                        OfferedVolume: Auctions.ForSection(notice.Year, notice.Auction).OfferedVolume));
                    break;

                case ClockEventKind.AuctionEndingSoon:
                    notices.Add(new RunNotice(notice.Kind, notice.Year, notice.Auction, TimeLeft: Clock.TimeToAuctionClose));
                    break;

                case ClockEventKind.AuctionClosed when _clearedSinceAnnounced.TryGetValue(notice, out Auction? cleared):
                    notices.Add(new RunNotice(notice.Kind, notice.Year, notice.Auction, Cleared: cleared));
                    break;
            }
        }

        _pendingNotices.Clear();
        _clearedSinceAnnounced.Clear();

        List<MarketTrade> fills = [];
        IReadOnlyList<MarketTrade> trades = Simulation.Journal.Trades;

        for (int index = (int)_journalSeen; index < trades.Count; index++)
        {
            if (trades[index].Channel == TradeChannel.Exchange)
            {
                fills.Add(trades[index]);
            }
        }

        _journalSeen = trades.Count;

        List<BookChange> books = [];

        foreach (Product product in Exchange.Products)
        {
            if (ChangedBook(product) is { } changed)
            {
                books.Add(changed);
            }
        }

        (string State, int Year) state = (Simulation.State.ToString(), Simulation.CurrentYear);
        (string State, int Year)? stateChange = null;

        if (_announcedState != state)
        {
            _announcedState = state;
            stateChange = state;
        }

        (string State, int Year, int Auction, bool Open) market =
            (Simulation.State.ToString(), Simulation.CurrentYear, Clock.CurrentAuction, Clock.IsAuctionOpen);
        (string State, int Year, int Auction, bool Open)? marketChange = null;

        if (_announcedMarket != market)
        {
            _announcedMarket = market;
            marketChange = market;
        }

        return new RunResult
        {
            BotActions = botActions ?? [],
            Notices = notices,
            Fills = fills,
            Books = books,
            State = stateChange,
            Market = marketChange,
        };
    }

    /// <summary>The book's summary if it has moved since it was last reported, otherwise null.</summary>
    private BookChange? ChangedBook(Product product)
    {
        OrderBook book = Exchange.Book(product);
        BookChange current = new(product, book.BestBid, book.BestOffer, book.LastTradePrice);

        if (_announcedBooks.TryGetValue(product, out BookChange? announced) && announced == current)
        {
            return null;
        }

        _announcedBooks[product] = current;

        return current;
    }
}
