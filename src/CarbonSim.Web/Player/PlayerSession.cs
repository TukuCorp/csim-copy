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

    private readonly SimulationRegistry _runs;
    private readonly IServiceScopeFactory _scopes;
    private readonly AuthenticationStateProvider _authentication;

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

    public async Task<PlayerView?> ForPlayerAsync(Guid simulationId, CancellationToken cancellationToken = default)
    {
        PlayerAccount? account = await CurrentAccountAsync().ConfigureAwait(false);

        if (account?.CompanyName is not { Length: > 0 } companyName || !_runs.Contains(simulationId))
        {
            return null;
        }

        return await _runs.ReadAsync(simulationId, run => Build(run, account, companyName)).ConfigureAwait(false);
    }

    public Task PlaceBidAsync(Guid simulationId, int unitId, int vintage, decimal price, decimal volume, CancellationToken cancellationToken = default) =>
        ActAsync(actor => _runs.BidAtAuctionAsync(simulationId, actor, unitId, vintage, price, volume));

    public Task PlaceOrderAsync(
        Guid simulationId,
        int unitId,
        string product,
        string side,
        string kind,
        decimal volume,
        decimal? price,
        decimal? stopPrice,
        string fillPolicy,
        CancellationToken cancellationToken = default) =>
        ActAsync(actor => _runs.PlaceOrderAsync(simulationId, actor, unitId, product, side, kind, volume, price, stopPrice, fillPolicy));

    public Task CancelOrderAsync(Guid simulationId, int unitId, long orderId, CancellationToken cancellationToken = default) =>
        ActAsync(actor => _runs.CancelOrderAsync(simulationId, actor, unitId, orderId));

    public Task ImplementAbatementAsync(Guid simulationId, int unitId, int optionIndex, CancellationToken cancellationToken = default) =>
        ActAsync(actor => _runs.ImplementAbatementAsync(simulationId, actor, unitId, optionIndex));

    public Task SendOtcOfferAsync(Guid simulationId, int sellerUnitId, int buyerUnitId, string product, decimal price, decimal volume, CancellationToken cancellationToken = default) =>
        ActAsync(actor => _runs.SendOtcOfferAsync(simulationId, actor, sellerUnitId, buyerUnitId, product, price, volume));

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

        using IServiceScope scope = _scopes.CreateScope();
        IAccountStore store = scope.ServiceProvider.GetRequiredService<IAccountStore>();

        return await store.FindByIdAsync(accountId).ConfigureAwait(false);
    }

    private static RunSummary Summarise(HostedRun run)
    {
        Simulation simulation = run.Simulation;
        Parameters parameters = simulation.TradingSystems.Single().Parameters;

        return new RunSummary(
            simulation.Id,
            simulation.Name,
            simulation.State.ToString(),
            simulation.CurrentYear,
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

        Parameters parameters = simulation.TradingSystems.Single().Parameters;
        int year = simulation.CurrentYear;
        bool started = year >= 1;
        CompliancePosition position = new(simulation);
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
            run.Clock.TimeLeftInYear,
            run.Clock.TimeToAuctionOpen,
            run.Clock.TimeToAuctionClose,
            account.DisplayName,
            company.Name,
            company.Sector.Name,
            company.Owner.Name,
            company.Owner.Kind == PlayerKind.Ai,
            BuildFinance(simulation, company, year, started),
            BuildCompliance(simulation, company, year, started),
            BuildPosition(simulation, position, company, year, started),
            BuildUnits(simulation, position, company, year, started),
            BuildAbatements(simulation, company, year),
            BuildAuctions(simulation, run, company),
            started ? BuildAuction(run.Auctions.ForSection(year, run.Clock.CurrentAuction), company) : null,
            started ? AuctionableForYear(run, year) : 0m,
            BuildBooks(simulation, run, company, tradable),
            tradable,
            BuildAuctionPrices(run),
            BuildCounterparties(simulation, company),
            BuildOffersToAnswer(run, company),
            BuildMyOffers(run, company),
            BuildHoldings(simulation, company, tradable),
            BuildMyTrades(simulation, company),
            BuildLeaderboard(simulation, company),
            BuildSystemInfo(simulation, parameters, year, started),
            [.. run.Chat.TakeLast(50).Select(entry => new ChatLine(entry.Year, entry.From, entry.Text))],
            run.MessagingEnabled);
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
        CompliancePosition position,
        Company company,
        int year,
        bool started)
    {
        decimal free = started ? simulation.Allocation.FreeAllocationFor(company, year) : 0m;
        decimal emissions = started ? position.EmissionsFor(company, year) : 0m;
        decimal reduction = started ? simulation.Abatements.ReductionsFor(company, year) : 0m;

        return new PositionView(free, emissions, reduction, emissions - free, position.OverallShortfallFor(company));
    }

    private static List<UnitView> BuildUnits(
        Simulation simulation,
        CompliancePosition position,
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
                    simulation.Abatements.HasImplemented(unit, option)));
            }

            units.Add(new UnitView(
                unit.Id,
                unit.Name,
                unit.Sector.Name,
                unit.BaselineEmissions,
                started ? simulation.Allocation.FreeAllocationFor(unit, year) : 0m,
                started ? position.EmissionsFor(unit, year) : unit.BaselineEmissions,
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

            myBids.Add(new MyBidView(bid.Vintage, bid.Price, bid.Volume, award?.Volume ?? 0m, award?.Cost ?? 0m));
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
            myAwards);
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
        decimal floor = simulation.TradingSystems.Single().Parameters.AuctionFloorPrice;

        foreach (ProductView view in tradable)
        {
            Product product = ToProduct(view);
            OrderBook? book = run.Exchange.Products.Contains(product) ? run.Exchange.Book(product) : null;

            List<OrderView> openOrders =
            [
                .. (book?.OpenOrders ?? []).Take(12).Select(ToView),
            ];

            List<OrderView> myOrders =
            [
                .. (book?.OpenOrders ?? []).Where(order => ReferenceEquals(order.Company, company)).Select(ToView),
            ];

            List<TradeRow> recent =
            [
                .. simulation.Journal.Trades
                    .Where(trade => trade.Channel == TradeChannel.Exchange && trade.Product == product)
                    .TakeLast(RecentTradeCount)
                    .Reverse()
                    .Select(trade => ToRow(trade, company)),
            ];

            books.Add(new ProductBookView(
                view,
                view.Key,
                book?.BestBid,
                book?.BestOffer,
                book?.LastTradePrice ?? floor,
                book?.BandFloor ?? floor * (1m - simulation.TradingSystems.Single().Parameters.VolatilityBand),
                book?.BandCeiling ?? floor * (1m + simulation.TradingSystems.Single().Parameters.VolatilityBand),
                openOrders,
                myOrders,
                recent,
                YearPrices(simulation, product)));
        }

        return books;
    }

    private static List<YearPriceView> YearPrices(Simulation simulation, Product product) =>
    [
        .. simulation.Journal.Trades
            .Where(trade => trade.Channel == TradeChannel.Exchange && trade.Product == product)
            .GroupBy(trade => trade.Year)
            .OrderBy(group => group.Key)
            .Select(group => new YearPriceView(
                group.Key,
                group.First().Price,
                group.Max(trade => trade.Price),
                group.Min(trade => trade.Price),
                group.Last().Price,
                group.Sum(trade => trade.Volume))),
    ];

    private static AuctionPriceSummaryView BuildAuctionPrices(HostedRun run)
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

    private static List<CounterpartyView> BuildCounterparties(Simulation simulation, Company company) =>
    [
        .. simulation.Units
            .Where(unit => !ReferenceEquals(unit.Company, company))
            .OrderBy(unit => unit.Company.Name, StringComparer.Ordinal)
            .ThenBy(unit => unit.Id)
            .Select(unit => new CounterpartyView(unit.Id, unit.Name, unit.Company.Name, unit.Sector.Name)),
    ];

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

    private static List<TradeRow> BuildMyTrades(Simulation simulation, Company company) =>
    [
        .. simulation.Journal.Trades
            .Where(trade => ReferenceEquals(trade.Buyer, company) || ReferenceEquals(trade.Seller, company))
            .TakeLast(HistoryCount)
            .Reverse()
            .Select(trade => ToRow(trade, company)),
    ];

    private static List<LeaderboardRow> BuildLeaderboard(Simulation simulation, Company company) =>
    [
        .. Leaderboard.Rank(simulation).Select(entry => new LeaderboardRow(
            entry.Rank,
            entry.Company.Name,
            string.Join(", ", entry.Company.Units.Select(unit => unit.Name)),
            entry.Company.Owner.Name,
            entry.OverallCostOfCompliance,
            entry.OverallMarginalCostOfCompliance,
            entry.FinalPosition,
            entry.IsAutomated,
            ReferenceEquals(entry.Company, company))),
    ];

    private static SystemInfoView BuildSystemInfo(
        Simulation simulation,
        Parameters parameters,
        int year,
        bool started)
    {
        SystemTotalsView thisYear = started
            ? ToTotals(simulation, SystemReport.For(simulation, year).ThisYear, year)
            : EmptyTotals();
        SystemTotalsView toDate = started
            ? ToTotals(simulation, SystemReport.For(simulation, year).ToDate, 0, toDate: true)
            : EmptyTotals();

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

        List<HoldingView> reserve =
        [
            .. Enumerable.Range(1, parameters.Years).Select(vintage => new HoldingView(
                ProductView.Allowance(vintage),
                simulation.Government.Held(vintage),
                simulation.Government.Issued(vintage))),
        ];

        return new SystemInfoView(
            simulation.TradingSystems.Single().Name,
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
            sectors,
            thisYear,
            toDate,
            simulation.Government.Revenue,
            reserve);
    }

    private static SystemTotalsView ToTotals(Simulation simulation, SystemTotals totals, int year, bool toDate = false)
    {
        int through = toDate ? simulation.Allocation.Years.Max() : year;
        decimal cap = through >= 1 ? simulation.Allocation.Years.Where(candidate => candidate <= through).Sum(simulation.Allocation.CapForYear) : 0m;
        decimal free = through >= 1 ? simulation.Allocation.Years.Where(candidate => candidate <= through).Sum(simulation.Allocation.FreeAllocationForYear) : 0m;

        return new SystemTotalsView(
            totals.ForecastEmissions,
            totals.AllowancesSurrendered,
            totals.OffsetsSurrendered,
            totals.Banked,
            totals.Forfeited,
            totals.AllowancesSold,
            totals.AllowancesBought,
            totals.OffsetsSold,
            totals.OffsetsBought,
            totals.AuctionVolume,
            totals.AuctionRevenue,
            totals.AverageAllowancePrice,
            totals.AverageOffsetPrice,
            totals.AbatementsImplemented,
            totals.AbatementTonnes,
            totals.EmissionsReduced,
            totals.PenaltyCount,
            totals.PenaltyValue,
            totals.FineValue,
            cap,
            free,
            cap - free);
    }

    private static SystemTotalsView EmptyTotals()
    {
        return new SystemTotalsView(
            0m, 0m, 0m, 0m, 0m, 0m, 0m, 0m, 0m, 0m, 0m, 0m, 0m, 0, 0m, 0m, 0, 0m, 0m, 0m, 0m, 0m);
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
