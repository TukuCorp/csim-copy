using System.Globalization;
using System.Security.Claims;
using CarbonSim.Data.Accounts;
using CarbonSim.Engine.Abatement;
using CarbonSim.Engine.Allocation;
using CarbonSim.Engine.Compliance;
using CarbonSim.Engine.Domain;
using CarbonSim.Engine.Market;
using CarbonSim.Engine.Market.Exchange;
using CarbonSim.Engine.Market.Otc;
using CarbonSim.Engine.Reporting;
using CarbonSim.Engine.Run;
using CarbonSim.Web.Simulations;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;

namespace CarbonSim.Web.Player;

/// <summary>
/// The read model behind the player screens and the front door for their actions. A page asks for
/// one <see cref="PlayerView"/>, which is built inside the run's gate from plain records, so the
/// page never holds a live engine object. Actions go through <see cref="SimulationRegistry"/>, the
/// same owner-checked path the SignalR hub uses.
/// </summary>
public sealed class PlayerSession : IPlayerSession
{
    private const int RecentTradeCount = 10;
    private const int HistoryCount = 25;
    private const int BookLevels = 5;

    private readonly SimulationRegistry _runs;
    private readonly IServiceScopeFactory _scopes;
    private readonly AuthenticationStateProvider _authentication;
    private PlayerAccount? _account;
    private AccountCompanyResolver? _players;

    public PlayerSession(
        SimulationRegistry runs,
        IServiceScopeFactory scopes,
        AuthenticationStateProvider authentication)
    {
        _runs = runs ?? throw new ArgumentNullException(nameof(runs));
        _scopes = scopes ?? throw new ArgumentNullException(nameof(scopes));
        _authentication = authentication ?? throw new ArgumentNullException(nameof(authentication));
    }

    public async Task<IReadOnlyList<RunSummary>> RunsAsync(CancellationToken cancellationToken = default)
    {
        List<RunSummary> summaries = [];

        foreach (Guid id in _runs.Ids)
        {
            if (!_runs.Contains(id))
            {
                continue;
            }

            summaries.Add(await _runs.ReadAsync(id, Summarise).ConfigureAwait(false));
        }

        return summaries;
    }

    public async Task<string?> CurrentActorAsync(CancellationToken cancellationToken = default)
    {
        return (await CurrentAccountAsync().ConfigureAwait(false))?.Id.ToString(CultureInfo.InvariantCulture);
    }

    public async Task<FrontDoorView> FrontDoorAsync(CancellationToken cancellationToken = default)
    {
        PlayerAccount? account = await CurrentAccountAsync().ConfigureAwait(false);

        if (account is null)
        {
            return FrontDoorView.Guest;
        }

        if (account.Role == AccountRole.Administrator)
        {
            // A trainer has no company, so their screens are the console rather than a run's.
            return new FrontDoorView(account.DisplayName, null, IsAdministrator: true, null);
        }

        return new FrontDoorView(
            account.DisplayName,
            account.CompanyName,
            IsAdministrator: false,
            await MyRunAsync(account.CompanyName).ConfigureAwait(false));
    }

    /// <summary>The run the signed-in company plays in, or null when the host holds no such run.</summary>
    private async Task<Guid?> MyRunAsync(string? company)
    {
        if (company is not { Length: > 0 })
        {
            return null;
        }

        foreach (Guid id in _runs.Ids)
        {
            if (_runs.Contains(id) && await _runs.ReadAsync(id, run => PlaysHere(run, company)).ConfigureAwait(false))
            {
                return id;
            }
        }

        return null;
    }

    private static bool PlaysHere(HostedRun run, string company) =>
        run.Simulation.Companies.Any(candidate => string.Equals(candidate.Name, company, StringComparison.Ordinal));

    public async Task<PlayerView?> ForPlayerAsync(Guid simulationId, CancellationToken cancellationToken = default)
    {
        PlayerAccount? account = await CurrentAccountAsync().ConfigureAwait(false);

        if (account?.CompanyName is not { Length: > 0 } companyName || !_runs.Contains(simulationId))
        {
            return null;
        }

        PlayerView? view = await _runs.ReadAsync(simulationId, run => Build(run, account, companyName)).ConfigureAwait(false);

        if (view is null)
        {
            return null;
        }

        // The resolver was taken from the scope the account was read in; it keeps its own cache.
        IReadOnlyDictionary<string, string> players = _players is { } resolver
            ? await resolver.PlayersByCompanyAsync(cancellationToken).ConfigureAwait(false)
            : new Dictionary<string, string>();

        // The run knows a company is played by a human; the account knows who, so the name is added here.
        return view with
        {
            Leaderboard =
            [
                .. view.Leaderboard.Select(row => players.TryGetValue(row.Company, out string? player) ? row with { Player = player } : row),
            ],
        };
    }

    public Task PlaceBidAsync(Guid simulationId, int unitId, int vintage, decimal price, decimal volume, CancellationToken cancellationToken = default) =>
        ActAsync(actor => _runs.BidAtAuctionAsync(simulationId, actor, unitId, vintage, price, volume));

    public async Task<OrderOutcome> PlaceOrderAsync(
        Guid simulationId,
        int unitId,
        string product,
        string side,
        string kind,
        decimal volume,
        decimal? price,
        decimal? stopPrice,
        string fillPolicy,
        CancellationToken cancellationToken = default)
    {
        string actor = await CurrentActorAsync().ConfigureAwait(false)
            ?? throw new InvalidOperationException("Sign in before acting in a simulation.");

        try
        {
            return await _runs.PlaceOrderAsync(simulationId, actor, unitId, product, side, kind, volume, price, stopPrice, fillPolicy).ConfigureAwait(false);
        }
        catch (TradeRefusedException refused)
        {
            // A shut window, named so the screen can say it in the reader's language.
            return OrderOutcome.Refused(OrderRefusal.Of(refused.Refusal, refused.Message));
        }
        catch (ArgumentException exception)
        {
            // An order the book would not take: a price outside the band, a volume of nothing.
            return OrderOutcome.Refused(OrderRefusal.Of(exception.Message));
        }
        catch (InvalidOperationException exception)
        {
            // The escrow the market could not set aside, in the engine's own words.
            return OrderOutcome.Refused(OrderRefusal.Of(exception.Message));
        }
    }

    public Task CancelOrderAsync(Guid simulationId, int unitId, long orderId, CancellationToken cancellationToken = default) =>
        ActAsync(actor => _runs.CancelOrderAsync(simulationId, actor, unitId, orderId));

    public Task SetAutoTradeAsync(Guid simulationId, int unitId, bool enabled, CancellationToken cancellationToken = default) =>
        ActAsync(actor => _runs.SetMyAutoTradeAsync(simulationId, actor, unitId, enabled));

    public Task SetSurrenderAsync(Guid simulationId, decimal allowances, decimal offsets, CancellationToken cancellationToken = default) =>
        ActAsync(actor => _runs.SetMySurrenderAsync(simulationId, actor, allowances, offsets));

    public Task CancelBidAsync(Guid simulationId, int bidId, CancellationToken cancellationToken = default) =>
        ActAsync(actor => _runs.CancelBidAsync(simulationId, actor, bidId));

    public Task ImplementAbatementAsync(Guid simulationId, int unitId, int optionIndex, CancellationToken cancellationToken = default) =>
        ActAsync(actor => _runs.ImplementAbatementAsync(simulationId, actor, unitId, optionIndex));

    public Task SendOtcOfferAsync(Guid simulationId, int sellerUnitId, int buyerUnitId, string product, decimal price, decimal volume, CancellationToken cancellationToken = default) =>
        ActAsync(actor => _runs.SendOtcOfferAsync(simulationId, actor, sellerUnitId, buyerUnitId, product, price, volume));

    public Task CancelOtcOfferAsync(Guid simulationId, long offerId, CancellationToken cancellationToken = default) =>
        ActAsync(actor => _runs.CancelOtcOfferAsync(simulationId, actor, offerId));

    public Task AnswerOtcOfferAsync(Guid simulationId, int buyerUnitId, long offerId, bool accept, CancellationToken cancellationToken = default) =>
        ActAsync(actor => _runs.AnswerOtcOfferAsync(simulationId, actor, buyerUnitId, offerId, accept));

    public Task PostMessageAsync(Guid simulationId, string text, CancellationToken cancellationToken = default) =>
        ActAsync(actor => _runs.PostMessageAsync(simulationId, actor, text));

    private async Task ActAsync(Func<string, Task> action)
    {
        string actor = await CurrentActorAsync().ConfigureAwait(false)
            ?? throw new InvalidOperationException("Sign in before acting in a simulation.");

        await action(actor).ConfigureAwait(false);
    }

    private async Task<PlayerAccount?> CurrentAccountAsync()
    {
        AuthenticationState state = await _authentication.GetAuthenticationStateAsync().ConfigureAwait(false);
        string? identifier = state.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (!int.TryParse(identifier, NumberStyles.Integer, CultureInfo.InvariantCulture, out int accountId))
        {
            return null;
        }

        // The session lives as long as one circuit, and the account it is signed in as does not
        // change under it (the company is claimed once, at registration), so it is read once rather
        // than on every once-a-second refresh.
        if (_account?.Id == accountId)
        {
            return _account;
        }

        using IServiceScope scope = _scopes.CreateScope();
        IAccountStore store = scope.ServiceProvider.GetRequiredService<IAccountStore>();

        _account = await store.FindByIdAsync(accountId).ConfigureAwait(false);
        _players = scope.ServiceProvider.GetRequiredService<AccountCompanyResolver>();

        return _account;
    }

    private static RunSummary Summarise(HostedRun run)
    {
        Simulation simulation = run.Simulation;
        Parameters parameters = simulation.Rules;

        return new RunSummary(
            simulation.Id,
            simulation.Name,
            simulation.State.ToString(),
            RunViews.YearOf(simulation),
            parameters.Years,
            simulation.Companies.Count,
            simulation.Companies.Count(company => company.Owner.Kind == PlayerKind.Human),
            simulation.Units.Count,
            parameters.AuctionsPerYear,
            parameters.YearLength,
            parameters.Cap,
            parameters.FreeAllocationShare,
            parameters.OffsetUsageLimit,
            parameters.BankingLimit,
            parameters.AuctionFloorPrice,
            parameters.AuctionCeilingPrice,
            parameters.TradingOpenShareOfYear);
    }

    private static PlayerView? Build(HostedRun run, PlayerAccount account, string companyName)
    {
        Simulation simulation = run.Simulation;
        Company? company = simulation.Companies.FirstOrDefault(candidate => string.Equals(candidate.Name, companyName, StringComparison.Ordinal));

        if (company is null)
        {
            return null;
        }

        Parameters parameters = simulation.Rules;
        int played = simulation.CurrentYear;
        int year = Math.Max(1, played);
        bool started = played >= 1;
        // A year that has been closed has nothing left to count down; the screens show the
        // settlement instead of a countdown to an auction that is not coming.
        bool yearOver = simulation.State is SimulationState.YearEnded or SimulationState.GameEnded;
        Standing standing = new(simulation);
        List<ProductView> tradable =
        [
            .. Enumerable.Range(1, parameters.Years).Select(ProductView.Allowance),
            ProductView.Offset,
        ];

        return new PlayerView(
            simulation.Id,
            simulation.Name,
            simulation.Seed,
            simulation.State.ToString(),
            year,
            parameters.Years,
            run.Clock.CurrentAuction,
            parameters.AuctionsPerYear,
            run.Clock.IsAuctionOpen,
            run.Clock.State == SimulationState.Running,
            yearOver ? TimeSpan.Zero : run.Clock.TimeLeftInYear,
            yearOver ? TimeSpan.Zero : run.Clock.TimeToAuctionOpen,
            yearOver ? TimeSpan.Zero : run.Clock.TimeToAuctionClose,
            account.DisplayName,
            company.Name,
            company.Sector.Name,
            company.Owner.Name,
            company.Owner.Kind == PlayerKind.Ai,
            BuildFinance(simulation, company, year, started),
            BuildCompliance(simulation, company, year, started),
            BuildPosition(simulation, standing, company, year, started),
            BuildUnits(simulation, standing, company, year, started),
            BuildAbatements(simulation, company, year),
            BuildAuctions(simulation, run, company),
            started ? BuildAuction(run.Auctions.ForSection(year, run.Clock.CurrentAuction), company) : null,
            started ? AuctionableForYear(run, year) : 0m,
            BuildBooks(simulation, run, company, tradable),
            tradable,
            BuildAuctionPrices(run),
            BuildCounterparties(run, company),
            BuildOffersToAnswer(run, company),
            BuildMyOffers(run, company),
            BuildHoldings(simulation, company, tradable),
            BuildMyTrades(run, company),
            parameters.LeaderboardVisible ? BuildLeaderboard(run, company) : [],
            BuildSystemInfo(run, parameters, year, started),
            // The room reads the room's lines; a line addressed to one company is that company's, which
            // is what keeps a sealed bid out of everybody else's feed.
            [.. run.Chat
                .Where(entry => entry.To is null || string.Equals(entry.To, company.Name, StringComparison.Ordinal))
                .TakeLast(50)
                .Select(entry => new ChatLine(entry.Year, entry.From, entry.Text))],
            run.MessagingEnabled)
        {
            TimeToHalt = run.Clock.TimeToHalt,
            CompanyReports = started ? [.. Enumerable.Range(1, year).Select(each => simulation.Finance.YearReport(company, each))] : [],
            LeaderboardYears = parameters.LeaderboardVisible && started
                ? [.. Enumerable.Range(1, year).Select(each => new LeaderboardYearView(each, BuildLeaderboardYear(run, company, each)))]
                : [],
            Surrender = started && simulation.Compliance.For(company, year) is null
                ? BuildSurrender(simulation, standing, company, year)
                : null,
        };
    }

    private static SurrenderView BuildSurrender(Simulation simulation, Standing standing, Company company, int year)
    {
        SurrenderPlan plan = simulation.Compliance.Plan(company, year);

        return new SurrenderView(
            plan.Manual,
            plan.HasInstruction,
            plan.Obligation,
            plan.AllowancesAvailable,
            plan.OffsetsAvailable,
            standing.OffsetLimit(company, year),
            plan.Allowances,
            plan.Offsets,
            plan.ResultingPosition,
            plan.Shortfall,
            plan.EstimatedPenalty,
            simulation.Rules.UnlimitedBanking);
    }

    private static FinancialView BuildFinance(Simulation simulation, Company company, int year, bool started)
    {
        decimal nop = started ? simulation.Finance.NormalOperatingProfit(company, year) : 0m;
        decimal abatement = started ? simulation.Finance.AbatementNetRevenue(company, year) : 0m;
        decimal netRevenue = started ? simulation.Finance.NetRevenue(company, year) : 0m;

        return new FinancialView(
            company.Capital,
            simulation.Cash.Available(company),
            simulation.Cash.Escrowed(company),
            company.OverdraftLimit,
            simulation.Finance.AvailableOverdraft(company),
            nop,
            abatement,
            nop + abatement,
            netRevenue,
            started ? simulation.Finance.Interest(company, year) : 0m,
            nop + abatement + netRevenue);
    }

    private static ComplianceView BuildCompliance(Simulation simulation, Company company, int year, bool started)
    {
        CompanyScore? thisYear = started ? Scoring.ForYear(simulation, company, year) : null;
        CompanyScore overall = Scoring.Overall(simulation, company);
        CompanyCompliance? result = started
            ? simulation.Compliance.Results.FirstOrDefault(candidate => ReferenceEquals(candidate.Company, company) && candidate.Year == year)
            : null;

        return new ComplianceView(
            thisYear?.CostOfCompliance ?? 0m,
            overall.CostOfCompliance,
            thisYear?.MarginalCostOfCompliance ?? 0m,
            overall.MarginalCostOfCompliance,
            thisYear?.Emissions ?? 0m,
            result?.Obligation ?? 0m,
            result?.Covered ?? 0m,
            result?.AllowancesSurrendered ?? 0m,
            result?.Banked ?? 0m,
            result?.Forfeited ?? 0m,
            result?.Shortfall ?? 0m,
            result is not null);
    }

    private static PositionView BuildPosition(
        Simulation simulation,
        Standing standing,
        Company company,
        int year,
        bool started)
    {
        decimal free = started ? simulation.Allocation.FreeAllocationFor(company, year) : 0m;
        decimal emissions = started ? standing.Emissions(company, year) : 0m;
        decimal reduction = started ? simulation.Abatements.ReductionsFor(company, year) : 0m;
        decimal held = started ? standing.Held(company, year) : 0m;
        CompanyCompliance? settled = started ? simulation.Compliance.For(company, year) : null;

        // Once the year has been reconciled there is no position left to be in: the obligation was
        // met or was not, and what the company holds is what it carried forward. Showing a fresh
        // forecast gap against the instruments that survived the settlement read as a new
        // shortfall of everything it had just surrendered.
        if (settled is not null)
        {
            return new PositionView(
                free,
                held,
                settled.Obligation,
                reduction,
                settled.Shortfall,
                Settled: true);
        }

        // The shortfall is the one module's answer, never negative: held above emissions is a long
        // position, and a light one is what the year still owes. A screen that subtracted them the
        // other way up would report a long company as though it had a negative obligation.
        return new PositionView(
            free,
            held,
            emissions,
            reduction,
            standing.Shortfall(company, year),
            Settled: false);
    }

    private static List<UnitView> BuildUnits(
        Simulation simulation,
        Standing standing,
        Company company,
        int year,
        bool started)
    {
        List<UnitView> units = [];

        foreach (Unit unit in company.Units)
        {
            List<AbatementOpportunityView> opportunities = [];

            for (int index = 0; index < unit.AbatementOptions.Count; index++)
            {
                AbatementOption option = unit.AbatementOptions[index];
                AbatementEconomics horizon = simulation.Abatements.OverSimulation(option, year);

                opportunities.Add(new AbatementOpportunityView(
                    unit.Id,
                    unit.Name,
                    index,
                    option.Code,
                    option.Name,
                    option.UpfrontCost,
                    option.AnnualReduction,
                    option.ImplementationYears,
                    option.LifetimeYears,
                    option.AnnualNetRevenue,
                    option.ForecastRoi ?? 0m,
                    option.NetCostPerTonne,
                    simulation.Abatements.HasImplemented(unit, option))
                {
                    SimulationTonnes = horizon.Tonnes,
                    SimulationCostPerTonne = horizon.NetCostPerTonne,
                    SimulationRoi = horizon.ForecastRoi,
                });
            }

            units.Add(new UnitView(
                unit.Id,
                unit.Name,
                unit.Sector.Name,
                unit.BaselineEmissions,
                started ? simulation.Allocation.FreeAllocationFor(unit, year) : 0m,
                started ? standing.Emissions(unit, year) : unit.BaselineEmissions,
                started ? simulation.Abatements.ReductionsFor(unit, year) : 0m,
                unit.NormalOperatingProfit,
                started && simulation.Abatements.IsShutDown(unit, year),
                unit.AutoTrade,
                simulation.Abatements.For(unit).Count,
                opportunities));
        }

        return units;
    }

    private static List<AbatementView> BuildAbatements(Simulation simulation, Company company, int year)
    {
        List<AbatementView> projects = [];

        foreach (Unit unit in company.Units)
        {
            foreach (ImplementedAbatement project in simulation.Abatements.For(unit))
            {
                projects.Add(new AbatementView(
                    unit.Id,
                    unit.Name,
                    project.Option.Code,
                    project.Option.Name,
                    project.ImplementedIn,
                    project.OperatingFromYear,
                    project.ExpiresAfterYear,
                    project.StatusIn(year).ToString(),
                    project.Option.AnnualReduction,
                    project.Option.UpfrontCost));
            }
        }

        return projects;
    }

    private static List<AuctionView> BuildAuctions(Simulation simulation, HostedRun run, Company company)
    {
        List<AuctionView> auctions = [];

        foreach (Auction auction in run.Auctions.Auctions.Where(auction => HasStarted(run, auction)))
        {
            auctions.Add(BuildAuction(auction, company));
        }

        return auctions;
    }

    /// <summary>An auction belongs on the history screen once its section of the year is under way.</summary>
    private static bool HasStarted(HostedRun run, Auction auction)
    {
        int year = run.Simulation.CurrentYear;

        return auction.Year < year || (auction.Year == year && auction.Sequence <= run.Clock.CurrentAuction);
    }

    private static AuctionView BuildAuction(Auction auction, Company company)
    {
        IEnumerable<AuctionAward> awards = auction.Results.SelectMany(result => result.Awards);
        List<MyBidView> myBids = [];

        foreach (AuctionBid bid in auction.Bids.Where(bid => ReferenceEquals(bid.Unit.Company, company)))
        {
            AuctionAward? award = awards.FirstOrDefault(candidate => ReferenceEquals(candidate.Bid, bid));

            myBids.Add(new MyBidView(bid.Vintage, bid.Price, bid.Volume, award?.Volume ?? 0m, award?.Cost ?? 0m, bid.Id));
        }

        List<AuctionAwardView> myAwards = [];

        foreach (AuctionResult result in auction.Results)
        {
            foreach (AuctionAward award in result.Awards.Where(award => ReferenceEquals(award.Bid.Unit.Company, company)))
            {
                myAwards.Add(new AuctionAwardView(result.Vintage, award.Volume, result.ClearingPrice ?? 0m, award.Cost));
            }
        }

        decimal? clearingPrice = auction.Results.FirstOrDefault(result => result.VolumeSold > 0m)?.ClearingPrice;

        return new AuctionView(
            auction.Year,
            auction.Sequence,
            auction.IsCleared,
            auction.OfferedVolume,
            auction.UnsoldVolume,
            clearingPrice,
            auction.Results.Sum(result => result.VolumeSold),
            [.. auction.Lots.Select(lot => new AuctionLotView(lot.Vintage, lot.Volume, lot.IsForward))],
            myBids,
            myAwards)
        {
            BidCount = auction.IsCleared ? auction.Bids.Count : null,
        };
    }

    private static decimal AuctionableForYear(HostedRun run, int year)
    {
        return run.Auctions.Auctions.Where(auction => auction.Year == year).Sum(auction => auction.OfferedVolume);
    }

    /// <summary>
    /// One entry per tradable product, whether or not it has traded yet. A product with no book is
    /// shown at the auction floor, which is the price every book's volatility band is anchored to,
    /// so the exchange screen lists its vintages and offsets from the moment the year opens.
    /// </summary>
    private static List<ProductBookView> BuildBooks(
        Simulation simulation,
        HostedRun run,
        Company company,
        IReadOnlyList<ProductView> tradable)
    {
        List<ProductBookView> books = [];
        decimal floor = simulation.Rules.AuctionFloorPrice;

        foreach (ProductView view in tradable)
        {
            Product product = ToProduct(view);
            OrderBook? book = run.Exchange.Products.Contains(product) ? run.Exchange.Book(product) : null;
            (decimal bandFloor, decimal bandCeiling) = run.Exchange.BandFor(product);

            // The top of the book, the tape and the candles are the same for every player, so each
            // is built once per change; only a player's own resting orders are cut per player. The
            // levels add every order at a price together: a book is read by price, not by order, and
            // the earlier take of the first twelve orders hid the selling side of a busy book
            // entirely.
            BookLevelsView levels = run.Shared(
                $"book-levels:{view.Key}",
                () => new BookLevelsView(
                    Bids: (IReadOnlyList<BookLevelView>)
                    [
                        .. (book?.OpenOrders ?? [])
                            .Where(order => order.Side == OrderSide.Buy && order.Price is not null)
                            .GroupBy(order => order.Price!.Value)
                            .OrderByDescending(level => level.Key)
                            .Take(BookLevels)
                            .Select(level => new BookLevelView(level.Key, level.Sum(order => order.Volume - order.FilledVolume))),
                    ],
                    Asks: (IReadOnlyList<BookLevelView>)
                    [
                        .. (book?.OpenOrders ?? [])
                            .Where(order => order.Side == OrderSide.Sell && order.Price is not null)
                            .GroupBy(order => order.Price!.Value)
                            .OrderBy(level => level.Key)
                            .Take(BookLevels)
                            .Select(level => new BookLevelView(level.Key, level.Sum(order => order.Volume - order.FilledVolume))),
                    ]));

            List<OrderView> myOrders =
            [
                .. (book?.OpenOrders ?? []).Where(order => ReferenceEquals(order.Company, company)).Select(ToView),
            ];

            IReadOnlyList<MarketTrade> recentTrades = run.Shared(
                $"book-trades:{view.Key}",
                () => (IReadOnlyList<MarketTrade>)
                [
                    .. simulation.Journal.Trades
                        .Where(trade => trade.Channel == TradeChannel.Exchange && trade.Product == product)
                        .TakeLast(RecentTradeCount),
                ]);
            IReadOnlyList<TradeRow> recent =
                [.. recentTrades.Reverse().Select(trade => ToRow(trade, company))];
            LastTrades last = run.Shared($"last-trades:{view.Key}", () => LastTradesOf(simulation, product));

            books.Add(new ProductBookView(
                view,
                view.Key,
                book?.BestBid,
                book?.BestOffer,
                book?.LastTradePrice ?? floor,
                bandFloor,
                bandCeiling,
                levels.Bids,
                levels.Asks,
                myOrders,
                recent,
                run.Shared($"book-prices:{view.Key}", () => MonthPrices(simulation, product)))
            {
                OtcLastTrade = last.Otc,
                MarketLastTrade = last.Market,
            });
        }

        return books;
    }

    /// <summary>A product's latest OTC deal and its latest deal in either secondary market.</summary>
    private sealed record LastTrades(decimal? Otc, decimal? Market);

    private static LastTrades LastTradesOf(Simulation simulation, Product product)
    {
        MarketTrade? otc = simulation.Journal.Trades.LastOrDefault(trade => trade.Channel == TradeChannel.Otc && trade.Product == product);
        MarketTrade? market = simulation.Journal.Trades.LastOrDefault(trade =>
            trade.Channel is TradeChannel.Exchange or TradeChannel.Otc && trade.Product == product);

        return new LastTrades(otc?.Price, market?.Price);
    }

    private static List<PeriodPriceView> MonthPrices(Simulation simulation, Product product) =>
    [
        .. simulation.Journal.Trades
            .Where(trade => trade.Channel == TradeChannel.Exchange && trade.Product == product)
            .GroupBy(trade => (trade.Year, Month: CarbonSim.Web.Components.Shared.Display.MonthOf(trade.AtYearOffset, simulation.Rules.YearLength)))
            .OrderBy(group => group.Key.Year)
            .ThenBy(group => group.Key.Month)
            .Select(group => new PeriodPriceView(
                group.Key.Year,
                group.Key.Month,
                group.First().Price,
                group.Max(trade => trade.Price),
                group.Min(trade => trade.Price),
                group.Last().Price,
                group.Sum(trade => trade.Volume))),
    ];

    private static AuctionPriceSummaryView BuildAuctionPrices(HostedRun run)
    {
        return run.Shared("auction-prices", () => BuildAuctionPricesCore(run));
    }

    private static AuctionPriceSummaryView BuildAuctionPricesCore(HostedRun run)
    {
        List<decimal> all =
        [
            .. run.Auctions.Auctions
                .SelectMany(auction => auction.Results)
                .Where(result => result.ClearingPrice is not null && result.VolumeSold > 0m)
                .Select(result => result.ClearingPrice!.Value),
        ];

        List<decimal> thisYear =
        [
            .. run.Auctions.Auctions
                .Where(auction => auction.Year == run.Simulation.CurrentYear)
                .SelectMany(auction => auction.Results)
                .Where(result => result.ClearingPrice is not null && result.VolumeSold > 0m)
                .Select(result => result.ClearingPrice!.Value),
        ];

        return new AuctionPriceSummaryView(
            all.Count == 0 ? null : all.Min(),
            all.Count == 0 ? null : all.Max(),
            all.Count == 0 ? null : all.Average(),
            all.Count == 0 ? null : all[^1],
            thisYear.Count == 0 ? null : thisYear.Average());
    }

    private static IReadOnlyList<CounterpartyView> BuildCounterparties(HostedRun run, Company company)
    {
        // The roster of other units is the same for everyone, so it is sorted once per change and
        // each player only filters out their own company from it. The companies a person plays
        // come first and the bots after them by sector, so a trainer looking for a counterparty
        // finds the other humans before the machines.
        IReadOnlyList<CounterpartyView> all = run.Shared(
            "counterparties",
            () => (IReadOnlyList<CounterpartyView>)
            [
                .. run.Simulation.Units
                    .OrderByDescending(unit => unit.Company.Owner.Kind == PlayerKind.Human)
                    .ThenBy(unit => unit.Company.Sector.Name, StringComparer.CurrentCulture)
                    .ThenBy(unit => unit.Company.Name, StringComparer.CurrentCulture)
                    .ThenBy(unit => unit.Id)
                    .Select(unit => new CounterpartyView(
                        unit.Id,
                        unit.Name,
                        unit.Company.Name,
                        unit.Sector.Name,
                        unit.Company.Owner.Kind == PlayerKind.Ai)),
            ]);

        return [.. all.Where(candidate => !string.Equals(candidate.CompanyName, company.Name, StringComparison.Ordinal))];
    }

    private static List<OtcOfferView> BuildOffersToAnswer(HostedRun run, Company company) =>
    [
        .. run.Otc.Pending
            .Where(offer => ReferenceEquals(offer.Buyer.Company, company))
            .Select(offer => ToView(offer, company)),
    ];

    private static List<OtcOfferView> BuildMyOffers(HostedRun run, Company company) =>
    [
        .. run.Otc.Offers
            .Where(offer => ReferenceEquals(offer.Seller.Company, company))
            .TakeLast(HistoryCount)
            .Reverse()
            .Select(offer => ToView(offer, company)),
    ];

    private static List<HoldingView> BuildHoldings(Simulation simulation, Company company, IReadOnlyList<ProductView> tradable)
    {
        List<HoldingView> holdings = [];

        foreach (ProductView view in tradable)
        {
            Product product = ToProduct(view);
            decimal available = simulation.Ledger.Available(company, product);
            decimal escrowed = simulation.Ledger.Escrowed(company, product);

            if (available != 0m || escrowed != 0m)
            {
                holdings.Add(new HoldingView(view, available, escrowed));
            }
        }

        return holdings;
    }

    private static IReadOnlyList<TradeRow> BuildMyTrades(HostedRun run, Company company) =>
        run.Shared(
            $"my-trades:{company.Name}",
            () => (IReadOnlyList<TradeRow>)
            [
                .. run.Simulation.Journal.Trades
                    .Where(trade => ReferenceEquals(trade.Buyer, company) || ReferenceEquals(trade.Seller, company))
                    .TakeLast(HistoryCount)
                    .Reverse()
                    .Select(trade => ToRow(trade, company)),
            ]);

    private static IReadOnlyList<LeaderboardRow> BuildLeaderboard(HostedRun run, Company company)
    {
        // The ranking touches every company in the run, so it is built once per change and shared;
        // only the "is this mine" flag is per player, and that is set as the rows are handed out.
        IReadOnlyList<LeaderboardEntry> ranked = run.Shared(
            "leaderboard",
            () => (IReadOnlyList<LeaderboardEntry>)Leaderboard.Rank(run.Simulation));

        return Rows(ranked, company);
    }

    private static IReadOnlyList<LeaderboardRow> BuildLeaderboardYear(HostedRun run, Company company, int year)
    {
        IReadOnlyList<LeaderboardEntry> ranked = run.Shared(
            $"leaderboard:{year}",
            () => (IReadOnlyList<LeaderboardEntry>)Leaderboard.RankYear(run.Simulation, year));

        return Rows(ranked, company);
    }

    private static List<LeaderboardRow> Rows(IReadOnlyList<LeaderboardEntry> ranked, Company company)
    {
        return
        [
            .. ranked.Select(entry => new LeaderboardRow(
                entry.Rank,
                entry.Company.Name,
                string.Join(", ", entry.Company.Units.Select(unit => unit.Name)),
                entry.Company.Owner.Name,
                entry.OverallCostOfCompliance,
                entry.OverallMarginalCostOfCompliance,
                entry.FinalPosition,
                entry.IsAutomated,
                ReferenceEquals(entry.Company, company))
            {
                Sector = entry.Company.Sector.Name,
                Penalties = entry.Penalties,
            }),
        ];
    }

    private static SystemInfoView BuildSystemInfo(
        HostedRun run,
        Parameters parameters,
        int year,
        bool started)
    {
        // The system report walks every company and unit, so it is built once per change and the
        // same answer is handed to every player; before the first year starts nothing is reported.
        return run.Shared("system-info", () => BuildSystemInfoCore(run.Simulation, parameters, year, started));
    }

    private static SystemInfoView BuildSystemInfoCore(
        Simulation simulation,
        Parameters parameters,
        int year,
        bool started)
    {
        SystemReport? report = started ? SystemReport.For(simulation, year) : null;
        SystemTotals thisYear = report?.ThisYear ?? SystemTotals.Empty;
        SystemTotals toDate = report?.ToDate ?? SystemTotals.Empty;

        List<SectorView> sectors =
        [
            .. simulation.Sectors.Select(sector => new SectorView(
                sector.Name,
                sector.EmissionShare,
                simulation.Units.Count(unit => string.Equals(unit.Sector.Name, sector.Name, StringComparison.Ordinal)),
                started
                    ? simulation.Units
                        .Where(unit => string.Equals(unit.Sector.Name, sector.Name, StringComparison.Ordinal))
                        .Sum(unit => simulation.Allocation.FreeAllocationFor(unit, year))
                    : 0m)),
        ];

        List<HoldingView> reserve = RunViews.Reserve(simulation);

        return new SystemInfoView(
            simulation.TradingSystem.Name,
            parameters.Cap,
            parameters.AnnualCapReductionRate,
            parameters.FreeAllocationShare,
            parameters.OffsetUsageLimit,
            parameters.BankingLimit,
            parameters.PenaltyPerTonne,
            parameters.PenaltyAllowanceDebit,
            parameters.AuctionFloorPrice,
            parameters.AuctionCeilingPrice,
            parameters.AuctionsPerYear,
            parameters.YearLength,
            parameters.AuctionDuration,
            parameters.TradingOpenShareOfYear,
            parameters.VolatilityBand,
            parameters.OverdraftInterestRate,
            parameters.GovernmentReserveToAuctionPercent,
            parameters.ForwardVintageShare,
            sectors,
            thisYear,
            toDate,
            simulation.Government.Revenue,
            reserve)
        {
            ExchangeAccess = parameters.ExchangeAccess,
            OtcAccess = parameters.OtcAccess,
            LeaderboardVisible = parameters.LeaderboardVisible,
            AiTradeAssistance = parameters.AiTradeAssistance,
        };
    }

    private static OrderView ToView(Order order) => new(
        order.Id,
        order.Unit.Id,
        order.Product.IsOffset ? "Offset" : $"Vintage {order.Product.Vintage}",
        order.Side.ToString(),
        order.Kind.ToString(),
        order.Volume,
        order.FilledVolume,
        order.Price,
        order.StopPrice,
        order.Status.ToString());

    private static TradeRow ToRow(MarketTrade trade, Company company) => new(
        trade.Year,
        trade.Channel.ToString(),
        trade.Product.IsOffset ? "Offset" : $"Vintage {trade.Product.Vintage}",
        trade.Price,
        trade.Volume,
        trade.Buyer.Name,
        trade.Seller?.Name ?? string.Empty,
        ReferenceEquals(trade.Buyer, company) || ReferenceEquals(trade.Seller, company));

    private static OtcOfferView ToView(OtcOffer offer, Company company) => new(
        offer.Id,
        offer.Seller.Name,
        offer.Buyer.Name,
        offer.Seller.Company.Name,
        offer.Buyer.Company.Name,
        ToView(offer.Product),
        offer.Price,
        offer.Volume,
        offer.Consideration,
        offer.State.ToString(),
        ReferenceEquals(offer.Buyer.Company, company),
        ReferenceEquals(offer.Seller.Company, company));

    private static ProductView ToView(Product product) =>
        product.IsOffset ? ProductView.Offset : ProductView.Allowance(product.Vintage);

    private static Product ToProduct(ProductView view) =>
        view.IsOffset ? Product.Offset : Product.Allowance(view.Vintage);
}
