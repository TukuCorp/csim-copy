using CarbonSim.Engine.Abatement;
using CarbonSim.Engine.Allocation;
using CarbonSim.Engine.Bots;
using CarbonSim.Engine.Clock;
using CarbonSim.Engine.Domain;
using CarbonSim.Engine.Market;
using CarbonSim.Engine.Market.Exchange;
using CarbonSim.Engine.Market.Otc;

namespace CarbonSim.Engine.Snapshot;

/// <summary>
/// A snapshot put back into a live object graph: the simulation and the things a host holds
/// beside it. The pieces are returned together because they only mean anything together — a
/// clock restored against a different simulation, or an exchange holding orders for units that
/// are not in the graph, would each be a different run.
/// </summary>
public sealed record RestoredSimulation(
    Simulation Simulation,
    SimulationClock Clock,
    Exchange Exchange,
    OtcMarket Otc,
    AuctionSchedule Auctions,
    SimulationBots? Bots);

/// <summary>
/// The snapshot boundary. <see cref="Capture"/> takes a run as it stands and
/// <see cref="Restore"/> builds a fresh graph from it, so a host can save a training exercise
/// and pick it up later without the engine ever knowing where it was written.
/// </summary>
/// <remarks>
/// Restoring does not replay the run. Balances, sequences and results are put back as they
/// were, and everything the run ever drew from the simulation's random stream is carried in the
/// snapshot rather than drawn again, including the stream's position — so the first draw after
/// a restore is the draw the original would have made. Restoring into a running simulation is
/// therefore the same run, not a run that happens to have reached the same numbers.
/// </remarks>
public static class SimulationSnapshots
{
    /// <summary>
    /// Takes everything a run needs to be rebuilt, exactly as it stands. The markets and the
    /// clock are captured alongside the simulation because a host holds them together: an
    /// order book with escrowed cash is state, not a view.
    /// </summary>
    public static SimulationSnapshot Capture(
        Simulation simulation,
        SimulationClock clock,
        Exchange exchange,
        OtcMarket otc,
        AuctionSchedule auctions,
        SimulationBots? bots = null)
    {
        ArgumentNullException.ThrowIfNull(simulation);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(exchange);
        ArgumentNullException.ThrowIfNull(otc);
        ArgumentNullException.ThrowIfNull(auctions);

        TradingSystem system = simulation.TradingSystems.Single();

        return new SimulationSnapshot(
            simulation.Id,
            simulation.Name,
            simulation.Seed,
            simulation.Random.State,
            simulation.State,
            simulation.CurrentYear,
            new TradingSystemSnapshot(system.Id, system.Name, Snapshot(system.Parameters)),
            [.. simulation.Sectors.Select(sector => new SectorSnapshot(
                sector.Name,
                sector.EmissionShare,
                [.. sector.AbatementMenu.Select(menu => new AbatementMenuSnapshot(
                    menu.Code,
                    menu.Name,
                    menu.AnnualReductionShare,
                    menu.UpfrontCostPerTonne,
                    menu.AnnualNetRevenuePerTonne,
                    menu.ImplementationYears,
                    menu.LifetimeYears))]))],
            [.. simulation.Players.Select(player => new PlayerSnapshot(player.Id, player.Name, player.Kind))],
            [.. simulation.Companies.Select(company => new CompanySnapshot(
                company.Id,
                company.Name,
                company.Sector.Name,
                company.Owner.Id,
                company.Capital,
                company.EscrowedCash,
                company.OverdraftLimit))],
            [.. simulation.Units.Select(unit => new UnitSnapshot(
                unit.Id,
                unit.Name,
                unit.Company.Id,
                unit.BaselineEmissions,
                unit.NormalOperatingProfit,
                unit.AutoTrade,
                [.. unit.AbatementOptions.Select(option => new AbatementOptionSnapshot(
                    option.Code,
                    option.Name,
                    option.UpfrontCost,
                    option.AnnualReduction,
                    option.ImplementationYears,
                    option.LifetimeYears,
                    option.AnnualNetRevenue))]))],
            CaptureAllocation(simulation.Allocation, simulation.Units),
            CaptureAbatements(simulation.Abatements, simulation.Units, simulation.Allocation.Years),
            simulation.Ledger.ToSnapshot(),
            simulation.Government.ToSnapshot(),
            new CashLedgerSnapshot(
                [.. simulation.Cash.Movements.Select(movement => new CashMovementSnapshot(
                    movement.Sequence,
                    movement.Year,
                    movement.Company.Id,
                    movement.Category,
                    movement.Amount,
                    movement.Description))]),
            simulation.Compliance.ToSnapshot(),
            new MarketJournalSnapshot(
                [.. simulation.Journal.Trades.Select(trade => new MarketTradeSnapshot(
                    trade.Sequence,
                    trade.Year,
                    trade.Channel,
                    Project(trade.Product),
                    trade.Price,
                    trade.Volume,
                    trade.Buyer.Id,
                    trade.Seller?.Id))]),
            CaptureExchange(exchange),
            CaptureOtc(otc),
            new AuctionScheduleSnapshot([.. auctions.Auctions.Select(CaptureAuction)]),
            clock.ToSnapshot(),
            bots is null ? null : new BotsSnapshot([.. bots.Bots.Select(bot => bot.ToSnapshot())]));
    }

    /// <summary>
    /// Builds a fresh object graph from a snapshot and hands back the simulation with the
    /// markets and clock that belong to it. <paramref name="timeProvider"/> is the host's clock
    /// for wall time; a run restored while its clock was running keeps measuring from the
    /// instant it was started, so the caller supplies a provider that agrees with that instant.
    /// </summary>
    public static RestoredSimulation Restore(SimulationSnapshot snapshot, TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(timeProvider);

        List<Sector> sectorList = [];
        Dictionary<string, Sector> sectors = [];

        foreach (SectorSnapshot sector in snapshot.Sectors)
        {
            Sector rebuilt = new(
                sector.Name,
                sector.EmissionShare,
                [.. sector.AbatementMenu.Select(menu => new AbatementMenu(
                    menu.Code,
                    menu.Name,
                    menu.AnnualReductionShare,
                    menu.UpfrontCostPerTonne,
                    menu.AnnualNetRevenuePerTonne,
                    menu.ImplementationYears,
                    menu.LifetimeYears))]);

            sectorList.Add(rebuilt);
            sectors[sector.Name] = rebuilt;
        }

        List<Player> playerList = [];
        Dictionary<int, Player> players = [];

        foreach (PlayerSnapshot player in snapshot.Players)
        {
            Player rebuilt = new(player.Id, player.Name, player.Kind);
            playerList.Add(rebuilt);
            players[player.Id] = rebuilt;
        }

        List<Company> companyList = [];
        Dictionary<int, Company> companies = [];

        foreach (CompanySnapshot company in snapshot.Companies)
        {
            Company rebuilt = Company.Restore(company, sectors[company.SectorName], players[company.OwnerPlayerId]);
            companyList.Add(rebuilt);
            companies[company.Id] = rebuilt;
        }

        Dictionary<int, Unit> units = [];

        foreach (UnitSnapshot unit in snapshot.Units)
        {
            Company company = companies[unit.CompanyId];
            List<AbatementOption> options =
            [
                .. unit.AbatementOptions.Select(option => new AbatementOption(
                    option.Code,
                    option.Name,
                    option.UpfrontCost,
                    option.AnnualReduction,
                    option.ImplementationYears,
                    option.LifetimeYears,
                    option.AnnualNetRevenue)),
            ];

            Unit rebuilt = new(unit.Id, unit.Name, company, unit.BaselineEmissions, options, unit.NormalOperatingProfit)
            {
                AutoTrade = unit.AutoTrade,
            };

            company.AddUnit(rebuilt);
            units[unit.Id] = rebuilt;
        }

        // Lists, not the lookup dictionaries, feed the graph: the order the companies, units and
        // players come back in decides the allocation split, the reconciliation order and the
        // leaderboard, so it is the order the snapshot recorded and not whatever order a
        // dictionary happens to enumerate in.
        TradingSystem system = new(
            snapshot.TradingSystem.Id,
            snapshot.TradingSystem.Name,
            Rebuild(snapshot.TradingSystem.Parameters),
            sectorList,
            companyList);

        Simulation simulation = Simulation.Create(snapshot.Name, snapshot.Seed, [system], playerList);

        // The graph exists, so the components can be put back onto it. The stream is set last of
        // the simulation's own state: building the graph draws the business-as-usual growth, and
        // that draw must not survive the restore.
        simulation.Restore(snapshot);
        simulation.Allocation.Restore(snapshot.Allocation);
        simulation.Abatements.Restore(snapshot.Abatements);
        simulation.Ledger.Restore(snapshot.Ledger);
        simulation.Government.Restore(snapshot.Government);
        simulation.Compliance.Restore(snapshot.Compliance);
        simulation.Journal.Restore(snapshot.Journal);
        simulation.Cash.Restore(snapshot.Cash);
        simulation.Random.RestoreState(snapshot.RandomState);

        Exchange exchange = new(simulation);
        exchange.Restore(snapshot.Exchange);

        OtcMarket otc = new(simulation);
        otc.Restore(snapshot.Otc);

        AuctionSchedule auctions = AuctionSchedule.Restore(
            [.. snapshot.Auctions.Auctions.Select(auction => Auction.Restore(simulation, auction))]);

        SimulationBots? bots = snapshot.Bots is { } fleet
            ? SimulationBots.Restore(
                [.. fleet.Bots.Select(bot => ComplianceBot.Restore(
                    companies[bot.CompanyId],
                    [.. bot.UnitIds.Select(id => units[id])],
                    simulation.Random,
                    bot))])
            : null;

        SimulationClock clock = new(
            simulation,
            timeProvider,
            new SimulationClockOptions
            {
                PauseAfterAuction = snapshot.Clock.PauseAfterAuction,
                AuctionNotice = snapshot.Clock.AuctionNotice,
            });

        clock.Restore(snapshot.Clock);

        return new RestoredSimulation(simulation, clock, exchange, otc, auctions, bots);
    }

    private static ParametersSnapshot Snapshot(Parameters parameters)
    {
        return new ParametersSnapshot(
            parameters.Cap,
            parameters.AnnualCapReductionRate,
            parameters.FreeAllocationShare,
            parameters.Years,
            [.. parameters.BausGrowthBySector.Select(growth => new SectorBausGrowthSnapshot(
                growth.Sector,
                growth.MinAnnualRate,
                growth.MaxAnnualRate))],
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
            parameters.OverdraftInterestRate);
    }

    private static Parameters Rebuild(ParametersSnapshot snapshot)
    {
        return new Parameters
        {
            Cap = snapshot.Cap,
            AnnualCapReductionRate = snapshot.AnnualCapReductionRate,
            FreeAllocationShare = snapshot.FreeAllocationShare,
            Years = snapshot.Years,
            BausGrowthBySector =
            [
                .. snapshot.BausGrowthBySector.Select(growth => new SectorBausGrowth(
                    growth.Sector,
                    growth.MinAnnualRate,
                    growth.MaxAnnualRate)),
            ],
            OffsetUsageLimit = snapshot.OffsetUsageLimit,
            BankingLimit = snapshot.BankingLimit,
            PenaltyPerTonne = snapshot.PenaltyPerTonne,
            PenaltyAllowanceDebit = snapshot.PenaltyAllowanceDebit,
            AuctionFloorPrice = snapshot.AuctionFloorPrice,
            AuctionCeilingPrice = snapshot.AuctionCeilingPrice,
            AuctionsPerYear = snapshot.AuctionsPerYear,
            YearLength = snapshot.YearLength,
            AuctionDuration = snapshot.AuctionDuration,
            TradingOpenShareOfYear = snapshot.TradingOpenShareOfYear,
            VolatilityBand = snapshot.VolatilityBand,
            OverdraftInterestRate = snapshot.OverdraftInterestRate,
        };
    }

    private static AllocationPlanSnapshot CaptureAllocation(AllocationPlan plan, IReadOnlyList<Unit> units)
    {
        return new AllocationPlanSnapshot(
            [.. plan.Years.Select(plan.CapForYear)],
            [.. plan.Years.Select(plan.FreeAllocationForYear)],
            [.. units.Select(unit => new UnitAllocationSnapshot(
                unit.Id,
                plan.BausGrowthFor(unit),
                [.. plan.Years.Select(year => plan.FreeAllocationFor(unit, year))],
                [.. plan.Years.Select(year => plan.BausEmissionsFor(unit, year))]))]);
    }

    private static AbatementPortfolioSnapshot CaptureAbatements(
        AbatementPortfolio portfolio,
        IReadOnlyList<Unit> units,
        IReadOnlyList<int> years)
    {
        List<UnitYearSnapshot> shutdowns = [];

        foreach (Unit unit in units)
        {
            foreach (int year in years)
            {
                if (portfolio.IsShutDown(unit, year))
                {
                    shutdowns.Add(new UnitYearSnapshot(unit.Id, year));
                }
            }
        }

        return new AbatementPortfolioSnapshot(
            [.. portfolio.All.Select(project => new ImplementedAbatementSnapshot(
                project.Unit.Id,
                OptionIndex(project.Unit, project.Option),
                project.Option.Code,
                project.ImplementedIn,
                project.OperatingFromYear,
                project.ExpiresAfterYear))],
            shutdowns);
    }

    private static int OptionIndex(Unit unit, AbatementOption option)
    {
        for (int index = 0; index < unit.AbatementOptions.Count; index++)
        {
            if (ReferenceEquals(unit.AbatementOptions[index], option))
            {
                return index;
            }
        }

        throw new InvalidOperationException(
            $"Abatement '{option.Code}' is implemented on unit '{unit.Name}' but is not on its menu.");
    }

    private static ExchangeSnapshot CaptureExchange(Exchange exchange)
    {
        return new ExchangeSnapshot(
            [.. exchange.Products
                .OrderBy(product => product.Kind)
                .ThenBy(product => product.Vintage)
                .Select(product => CaptureBook(exchange.Book(product)))],
            exchange.LastOrderId);
    }

    private static OrderBookSnapshot CaptureBook(OrderBook book)
    {
        ProductSnapshot product = Project(book.Product);

        return new OrderBookSnapshot(
            product,
            book.LastTradePrice,
            [.. book.OpenOrders.Select(order => new OrderSnapshot(
                order.Id,
                order.Unit.Id,
                order.Company.Id,
                product,
                order.Side,
                order.Kind,
                order.FillPolicy,
                order.Volume,
                order.Price,
                order.StopPrice,
                order.FilledVolume,
                order.Status,
                order.EscrowedCash))],
            [.. book.Trades.Select(trade => new BookTradeSnapshot(
                trade.Sequence,
                product,
                trade.BuyOrderId,
                trade.SellOrderId,
                trade.Price,
                trade.Volume))]);
    }

    private static OtcMarketSnapshot CaptureOtc(OtcMarket otc)
    {
        return new OtcMarketSnapshot(
            [.. otc.Offers.Select(offer => new OtcOfferSnapshot(
                offer.Id,
                offer.Seller.Id,
                offer.Buyer.Id,
                Project(offer.Product),
                offer.Price,
                offer.Volume,
                offer.State,
                offer.Year))]);
    }

    private static AuctionSnapshot CaptureAuction(Auction auction)
    {
        HashSet<int> served =
        [
            .. auction.Results
                .SelectMany(result => result.Awards)
                .Select(award => award.Bid.Id),
        ];

        return new AuctionSnapshot(
            auction.Year,
            auction.Sequence,
            [.. auction.Lots.Select(lot => new AuctionLotSnapshot(lot.Vintage, lot.Volume, lot.IsForward))],
            auction.IsCleared,
            auction.UnsoldVolume,
            [.. auction.Bids.Select(bid => new AuctionBidSnapshot(
                bid.Id,
                bid.Unit.Id,
                bid.Unit.Company.Id,
                bid.Vintage,
                bid.Price,
                bid.Volume,
                served.Contains(bid.Id)))],
            [.. auction.Results.Select(result => new AuctionResultSnapshot(
                result.Vintage,
                result.OfferedVolume,
                result.ClearingPrice,
                [.. result.Awards.Select(award => new AuctionAwardSnapshot(award.Bid.Id, award.Volume, award.Cost))],
                [.. result.Rejected.Select(bid => bid.Id)]))]);
    }

    private static ProductSnapshot Project(Product product) => new(product.Kind, product.Vintage);
}
