using CarbonSim.Engine.Market;
using CarbonSim.Engine.Snapshot;

namespace CarbonSim.Data.Persistence;

/// <summary>
/// Turns a saved run into engine state and engine state into a saved run. Everything the
/// database knows about the engine lives here: the tables themselves are flat, and this is
/// where the engine's object graph is taken apart and put back together.
/// </summary>
/// <remarks>
/// Two habits keep the mapping faithful. Lists that have an engine order are stored with the
/// order they need and read back in it, and values the engine drew from its random stream are
/// stored rather than recomputed, so loading a run never advances the stream.
/// </remarks>
public static class SimulationMapper
{
    public static SavedRun ToSavedRun(SimulationSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        Guid id = snapshot.Id;
        int systemId = snapshot.TradingSystem.Id;
        ParametersSnapshot parameters = snapshot.TradingSystem.Parameters;

        return new SavedRun
        {
            Root = new SimulationRecord
            {
                SimulationId = id,
                Name = snapshot.Name,
                Seed = snapshot.Seed,
                RandomState = snapshot.RandomState,
                State = snapshot.State,
                CurrentYear = snapshot.CurrentYear,
            },
            TradingSystems =
            [
                new TradingSystemRecord
                {
                    SimulationId = id,
                    TradingSystemId = systemId,
                    Name = snapshot.TradingSystem.Name,
                },
            ],
            Parameters =
            [
                new ParametersRecord
                {
                    SimulationId = id,
                    TradingSystemId = systemId,
                    Cap = parameters.Cap,
                    AnnualCapReductionRate = parameters.AnnualCapReductionRate,
                    FreeAllocationShare = parameters.FreeAllocationShare,
                    Years = parameters.Years,
                    OffsetUsageLimit = parameters.OffsetUsageLimit,
                    BankingLimit = parameters.BankingLimit,
                    PenaltyPerTonne = parameters.PenaltyPerTonne,
                    PenaltyAllowanceDebit = parameters.PenaltyAllowanceDebit,
                    AuctionFloorPrice = parameters.AuctionFloorPrice,
                    AuctionCeilingPrice = parameters.AuctionCeilingPrice,
                    AuctionsPerYear = parameters.AuctionsPerYear,
                    YearLength = parameters.YearLength,
                    AuctionDuration = parameters.AuctionDuration,
                    TradingOpenShareOfYear = parameters.TradingOpenShareOfYear,
                    VolatilityBand = parameters.VolatilityBand,
                    OverdraftInterestRate = parameters.OverdraftInterestRate,
                },
            ],
            BausGrowth =
            [
                .. parameters.BausGrowthBySector.Select(
                    (growth, index) => new BausGrowthRecord
                    {
                        SimulationId = id,
                        TradingSystemId = systemId,
                        BausGrowthId = index + 1,
                        Sector = growth.Sector,
                        MinAnnualRate = growth.MinAnnualRate,
                        MaxAnnualRate = growth.MaxAnnualRate,
                    }),
            ],
            Sectors =
            [
                .. snapshot.Sectors.Select(
                    sector => new SectorRecord
                    {
                        SimulationId = id,
                        Name = sector.Name,
                        EmissionShare = sector.EmissionShare,
                    }),
            ],
            AbatementMenu =
            [
                .. snapshot.Sectors.SelectMany(
                    sector => sector.AbatementMenu.Select(
                        (entry, index) => new AbatementMenuRecord
                        {
                            SimulationId = id,
                            Sector = sector.Name,
                            Code = entry.Code,
                            Ordinal = index,
                            Name = entry.Name,
                            AnnualReductionShare = entry.AnnualReductionShare,
                            UpfrontCostPerTonne = entry.UpfrontCostPerTonne,
                            AnnualNetRevenuePerTonne = entry.AnnualNetRevenuePerTonne,
                            ImplementationYears = entry.ImplementationYears,
                            LifetimeYears = entry.LifetimeYears,
                        })),
            ],
            Players =
            [
                .. snapshot.Players.Select(
                    player => new PlayerRecord
                    {
                        SimulationId = id,
                        PlayerId = player.Id,
                        Name = player.Name,
                        Kind = player.Kind,
                    }),
            ],
            Companies =
            [
                .. snapshot.Companies.Select(
                    company => new CompanyRecord
                    {
                        SimulationId = id,
                        CompanyId = company.Id,
                        Name = company.Name,
                        Sector = company.SectorName,
                        OwnerPlayerId = company.OwnerPlayerId,
                        Capital = company.Capital,
                        EscrowedCash = company.EscrowedCash,
                        OverdraftLimit = company.OverdraftLimit,
                    }),
            ],
            Units =
            [
                .. snapshot.Units.Select(
                    unit => new UnitRecord
                    {
                        SimulationId = id,
                        UnitId = unit.Id,
                        Name = unit.Name,
                        CompanyId = unit.CompanyId,
                        BaselineEmissions = unit.BaselineEmissions,
                        NormalOperatingProfit = unit.NormalOperatingProfit,
                        AutoTrade = unit.AutoTrade,
                    }),
            ],
            AbatementOptions =
            [
                .. snapshot.Units.SelectMany(
                    unit => unit.AbatementOptions.Select(
                        (option, index) => new AbatementOptionRecord
                        {
                            SimulationId = id,
                            UnitId = unit.Id,
                            Code = option.Code,
                            Ordinal = index,
                            Name = option.Name,
                            UpfrontCost = option.UpfrontCost,
                            AnnualReduction = option.AnnualReduction,
                            ImplementationYears = option.ImplementationYears,
                            LifetimeYears = option.LifetimeYears,
                            AnnualNetRevenue = option.AnnualNetRevenue,
                        })),
            ],
            AllocationYears =
            [
                .. snapshot.Allocation.Caps.Select(
                    (cap, index) => new AllocationYearRecord
                    {
                        SimulationId = id,
                        Year = index + 1,
                        Cap = cap,
                        FreeAllocation = snapshot.Allocation.FreeAllocation[index],
                    }),
            ],
            UnitAllocations =
            [
                .. snapshot.Allocation.Units.Select(
                    unit => new UnitAllocationRecord
                    {
                        SimulationId = id,
                        UnitId = unit.UnitId,
                        BausGrowth = unit.BausGrowth,
                    }),
            ],
            UnitAllocationYears =
            [
                .. snapshot.Allocation.Units.SelectMany(
                    unit => unit.FreeAllocation.Select(
                        (free, index) => new UnitAllocationYearRecord
                        {
                            SimulationId = id,
                            UnitId = unit.UnitId,
                            Year = index + 1,
                            FreeAllocation = free,
                            BausEmissions = unit.BausEmissions[index],
                        })),
            ],
            ImplementedAbatements =
            [
                .. snapshot.Abatements.Projects.Select(
                    project => new ImplementedAbatementRecord
                    {
                        SimulationId = id,
                        UnitId = project.UnitId,
                        OptionIndex = project.OptionIndex,
                        OptionCode = project.OptionCode,
                        ImplementedIn = project.ImplementedIn,
                        OperatingFromYear = project.OperatingFromYear,
                        ExpiresAfterYear = project.ExpiresAfterYear,
                    }),
            ],
            Shutdowns =
            [
                .. snapshot.Abatements.Shutdowns.Select(
                    shutdown => new ShutdownRecord
                    {
                        SimulationId = id,
                        UnitId = shutdown.UnitId,
                        Year = shutdown.Year,
                    }),
            ],
            LedgerAvailable =
            [
                .. snapshot.Ledger.Available.Select(
                    balance => new LedgerAvailableRecord
                    {
                        SimulationId = id,
                        CompanyId = balance.CompanyId,
                        ProductKind = balance.Product.Kind,
                        Vintage = balance.Product.Vintage,
                        Volume = balance.Volume,
                    }),
            ],
            LedgerEscrowed =
            [
                .. snapshot.Ledger.Escrowed.Select(
                    balance => new LedgerEscrowedRecord
                    {
                        SimulationId = id,
                        CompanyId = balance.CompanyId,
                        ProductKind = balance.Product.Kind,
                        Vintage = balance.Product.Vintage,
                        Volume = balance.Volume,
                    }),
            ],
            LedgerGrantedYears =
            [
                .. snapshot.Ledger.GrantedYears.Select(
                    year => new LedgerGrantedYearRecord { SimulationId = id, Year = year }),
            ],
            Government = new GovernmentRecord
            {
                SimulationId = id,
                Revenue = snapshot.Government.Revenue,
                HasIssued = snapshot.Government.HasIssued,
            },
            GovernmentReserves =
            [
                .. snapshot.Government.Reserve.Select(
                    vintage => new GovernmentReserveRecord
                    {
                        SimulationId = id,
                        Vintage = vintage.Vintage,
                        Volume = vintage.Volume,
                    }),
            ],
            GovernmentIssued =
            [
                .. snapshot.Government.IssuedVolumes.Select(
                    vintage => new GovernmentIssuedRecord
                    {
                        SimulationId = id,
                        Vintage = vintage.Vintage,
                        Volume = vintage.Volume,
                    }),
            ],
            CashMovements =
            [
                .. snapshot.Cash.Movements.Select(
                    movement => new CashMovementRecord
                    {
                        SimulationId = id,
                        Sequence = movement.Sequence,
                        Year = movement.Year,
                        CompanyId = movement.CompanyId,
                        Category = movement.Category,
                        Amount = movement.Amount,
                        Description = movement.Description,
                    }),
            ],
            Compliances =
            [
                .. snapshot.Compliance.Results.Select(
                    result => new CompanyComplianceRecord
                    {
                        SimulationId = id,
                        Year = result.Year,
                        CompanyId = result.CompanyId,
                        Obligation = result.Obligation,
                        OffsetsSurrendered = result.OffsetsSurrendered,
                        AllowancesSurrendered = result.AllowancesSurrendered,
                        Banked = result.Banked,
                        Forfeited = result.Forfeited,
                        Shortfall = result.Shortfall,
                        PenaltyCash = result.PenaltyCash,
                        PenaltyAllowanceDebit = result.PenaltyAllowanceDebit,
                    }),
            ],
            Fines =
            [
                .. snapshot.Compliance.Fines.Select(
                    fine => new FineRecord
                    {
                        SimulationId = id,
                        FineId = fine.Id,
                        UnitId = fine.UnitId,
                        CompanyId = fine.CompanyId,
                        Amount = fine.Amount,
                        Description = fine.Description,
                        Year = fine.Year,
                    }),
            ],
            Reconciled =
            [
                .. snapshot.Compliance.Reconciled.Select(
                    reconciled => new ReconciledCompanyYearRecord
                    {
                        SimulationId = id,
                        CompanyId = reconciled.CompanyId,
                        Year = reconciled.Year,
                    }),
            ],
            JournalTrades =
            [
                .. snapshot.Journal.Trades.Select(
                    trade => new JournalTradeRecord
                    {
                        SimulationId = id,
                        Sequence = trade.Sequence,
                        Year = trade.Year,
                        Channel = trade.Channel,
                        ProductKind = trade.Product.Kind,
                        Vintage = trade.Product.Vintage,
                        Price = trade.Price,
                        Volume = trade.Volume,
                        BuyerCompanyId = trade.BuyerCompanyId,
                        SellerCompanyId = trade.SellerCompanyId,
                    }),
            ],
            Exchange = new ExchangeRecord { SimulationId = id, LastOrderId = snapshot.Exchange.LastOrderId },
            OrderBooks =
            [
                .. snapshot.Exchange.Books.Select(
                    book => new OrderBookRecord
                    {
                        SimulationId = id,
                        ProductKind = book.Product.Kind,
                        Vintage = book.Product.Vintage,
                        LastTradePrice = book.LastTradePrice,
                    }),
            ],
            Orders =
            [
                .. snapshot.Exchange.Books.SelectMany(
                    book => book.RestingOrders.Select(
                        order => new OrderRecord
                        {
                            SimulationId = id,
                            OrderId = order.Id,
                            UnitId = order.UnitId,
                            CompanyId = order.CompanyId,
                            ProductKind = order.Product.Kind,
                            Vintage = order.Product.Vintage,
                            Side = order.Side,
                            Kind = order.Kind,
                            FillPolicy = order.FillPolicy,
                            Volume = order.Volume,
                            Price = order.Price,
                            StopPrice = order.StopPrice,
                            FilledVolume = order.FilledVolume,
                            Status = order.Status,
                            EscrowedCash = order.EscrowedCash,
                        })),
            ],
            BookTrades =
            [
                .. snapshot.Exchange.Books.SelectMany(
                    book => book.Trades.Select(
                        trade => new BookTradeRecord
                        {
                            SimulationId = id,
                            ProductKind = trade.Product.Kind,
                            Vintage = trade.Product.Vintage,
                            Sequence = trade.Sequence,
                            BuyOrderId = trade.BuyOrderId,
                            SellOrderId = trade.SellOrderId,
                            Price = trade.Price,
                            Volume = trade.Volume,
                        })),
            ],
            OtcOffers =
            [
                .. snapshot.Otc.Offers.Select(
                    offer => new OtcOfferRecord
                    {
                        SimulationId = id,
                        OfferId = offer.Id,
                        SellerUnitId = offer.SellerUnitId,
                        BuyerUnitId = offer.BuyerUnitId,
                        ProductKind = offer.Product.Kind,
                        Vintage = offer.Product.Vintage,
                        Price = offer.Price,
                        Volume = offer.Volume,
                        State = offer.State,
                        Year = offer.Year,
                    }),
            ],
            Auctions =
            [
                .. snapshot.Auctions.Auctions.Select(
                    auction => new AuctionRecord
                    {
                        SimulationId = id,
                        Year = auction.Year,
                        Sequence = auction.Sequence,
                        IsCleared = auction.IsCleared,
                        UnsoldVolume = auction.UnsoldVolume,
                    }),
            ],
            AuctionLots =
            [
                .. snapshot.Auctions.Auctions.SelectMany(
                    auction => auction.Lots.Select(
                        lot => new AuctionLotRecord
                        {
                            SimulationId = id,
                            Year = auction.Year,
                            Sequence = auction.Sequence,
                            Vintage = lot.Vintage,
                            Volume = lot.Volume,
                            IsForward = lot.IsForward,
                        })),
            ],
            AuctionBids =
            [
                .. snapshot.Auctions.Auctions.SelectMany(
                    auction => auction.Bids.Select(
                        bid => new AuctionBidRecord
                        {
                            SimulationId = id,
                            Year = auction.Year,
                            Sequence = auction.Sequence,
                            BidId = bid.Id,
                            UnitId = bid.UnitId,
                            CompanyId = bid.CompanyId,
                            Vintage = bid.Vintage,
                            Price = bid.Price,
                            Volume = bid.Volume,
                            Won = bid.Won,
                        })),
            ],
            AuctionResults =
            [
                .. snapshot.Auctions.Auctions.SelectMany(
                    auction => auction.Results.Select(
                        result => new AuctionResultRecord
                        {
                            SimulationId = id,
                            Year = auction.Year,
                            Sequence = auction.Sequence,
                            Vintage = result.Vintage,
                            OfferedVolume = result.OfferedVolume,
                            ClearingPrice = result.ClearingPrice,
                        })),
            ],
            AuctionAwards =
            [
                .. snapshot.Auctions.Auctions.SelectMany(
                    auction => auction.Results.SelectMany(
                        result => result.Awards.Select(
                            award => new AuctionAwardRecord
                            {
                                SimulationId = id,
                                Year = auction.Year,
                                Sequence = auction.Sequence,
                                Vintage = result.Vintage,
                                BidId = award.BidId,
                                Volume = award.Volume,
                                Cost = award.Cost,
                            }))),
            ],
            AuctionRejectedBids =
            [
                .. snapshot.Auctions.Auctions.SelectMany(
                    auction => auction.Results.SelectMany(
                        result => result.RejectedBidIds.Select(
                            bidId => new AuctionRejectedBidRecord
                            {
                                SimulationId = id,
                                Year = auction.Year,
                                Sequence = auction.Sequence,
                                Vintage = result.Vintage,
                                BidId = bidId,
                            }))),
            ],
            Clock = new ClockRecord
            {
                SimulationId = id,
                State = snapshot.Clock.State,
                CurrentYear = snapshot.Clock.CurrentYear,
                Elapsed = snapshot.Clock.Elapsed,
                RunningSince = snapshot.Clock.RunningSince,
                HaltAt = snapshot.Clock.HaltAt,
                OpenNoticedThrough = snapshot.Clock.OpenNoticedThrough,
                OpenedThrough = snapshot.Clock.OpenedThrough,
                CloseNoticedThrough = snapshot.Clock.CloseNoticedThrough,
                ClosedThrough = snapshot.Clock.ClosedThrough,
                HaltedForYearEnd = snapshot.Clock.HaltedForYearEnd,
                PauseAfterAuction = snapshot.Clock.PauseAfterAuction,
                AuctionNotice = snapshot.Clock.AuctionNotice,
            },
            Bots = [.. BotRecords(id, snapshot)],
            BotUnits =
            [
                .. snapshot.Bots?.Bots.SelectMany(
                    (bot, index) => bot.UnitIds.Select(
                        unitId => new BotUnitRecord
                        {
                            SimulationId = id,
                            BotId = index + 1,
                            UnitId = unitId,
                        })) ?? [],
            ],
            BotTriggerTimes =
            [
                .. snapshot.Bots?.Bots.SelectMany(
                    (bot, index) => bot.TriggerTimes.Select(
                        time => new BotTriggerTimeRecord
                        {
                            SimulationId = id,
                            BotId = index + 1,
                            Trigger = time.Trigger,
                            AtFractionOfYear = time.AtFractionOfYear,
                        })) ?? [],
            ],
            BotTriggerDone =
            [
                .. snapshot.Bots?.Bots.SelectMany(
                    (bot, index) => bot.Done.Select(
                        done => new BotTriggerDoneRecord
                        {
                            SimulationId = id,
                            BotId = index + 1,
                            Year = done.Year,
                            Trigger = done.Trigger,
                        })) ?? [],
            ],
            BotBidSections =
            [
                .. snapshot.Bots?.Bots.SelectMany(
                    (bot, index) => bot.BidIn.Select(
                        section => new BotBidSectionRecord
                        {
                            SimulationId = id,
                            BotId = index + 1,
                            Year = section.Year,
                            Section = section.Section,
                        })) ?? [],
            ],
        };
    }

    public static SimulationSnapshot ToSnapshot(SavedRun run)
    {
        ArgumentNullException.ThrowIfNull(run);

        Guid id = run.Root.SimulationId;
        TradingSystemRecord system = run.TradingSystems.Single();
        ParametersRecord parameters = run.Parameters.Single();

        ILookup<string, AbatementMenuRecord> menuBySector = run.AbatementMenu.ToLookup(entry => entry.Sector);
        ILookup<int, AbatementOptionRecord> optionsByUnit = run.AbatementOptions.ToLookup(option => option.UnitId);
        ILookup<int, UnitAllocationYearRecord> allocationYearsByUnit = run.UnitAllocationYears.ToLookup(year => year.UnitId);
        ILookup<int, ImplementedAbatementRecord> projectsByUnit = run.ImplementedAbatements.ToLookup(project => project.UnitId);
        ILookup<int, ShutdownRecord> shutdownsByUnit = run.Shutdowns.ToLookup(shutdown => shutdown.UnitId);
        ILookup<(ProductKind Kind, int Vintage), OrderRecord> ordersByBook = run.Orders.ToLookup(order => (order.ProductKind, order.Vintage));
        ILookup<(ProductKind Kind, int Vintage), BookTradeRecord> tradesByBook = run.BookTrades.ToLookup(trade => (trade.ProductKind, trade.Vintage));
        ILookup<(int Year, int Sequence), AuctionLotRecord> lotsByAuction = run.AuctionLots.ToLookup(lot => (lot.Year, lot.Sequence));
        ILookup<(int Year, int Sequence), AuctionBidRecord> bidsByAuction = run.AuctionBids.ToLookup(bid => (bid.Year, bid.Sequence));
        ILookup<(int Year, int Sequence), AuctionResultRecord> resultsByAuction = run.AuctionResults.ToLookup(result => (result.Year, result.Sequence));
        ILookup<(int Year, int Sequence, int Vintage), AuctionAwardRecord> awardsByResult = run.AuctionAwards.ToLookup(award => (award.Year, award.Sequence, award.Vintage));
        ILookup<(int Year, int Sequence, int Vintage), AuctionRejectedBidRecord> rejectedByResult = run.AuctionRejectedBids.ToLookup(rejected => (rejected.Year, rejected.Sequence, rejected.Vintage));
        ILookup<int, BotUnitRecord> unitsByBot = run.BotUnits.ToLookup(unit => unit.BotId);
        ILookup<int, BotTriggerTimeRecord> triggerTimesByBot = run.BotTriggerTimes.ToLookup(time => time.BotId);
        ILookup<int, BotTriggerDoneRecord> doneByBot = run.BotTriggerDone.ToLookup(done => done.BotId);
        ILookup<int, BotBidSectionRecord> sectionsByBot = run.BotBidSections.ToLookup(section => section.BotId);

        // The two buckets are read back exactly as they were stored: a key with a zero balance is
        // a key the engine has, and dropping it here would be a change of state.
        List<LedgerBalanceSnapshot> available =
        [
            .. run.LedgerAvailable
                .OrderBy(balance => balance.CompanyId)
                .ThenBy(balance => balance.ProductKind)
                .ThenBy(balance => balance.Vintage)
                .Select(balance => new LedgerBalanceSnapshot(
                    balance.CompanyId,
                    new ProductSnapshot(balance.ProductKind, balance.Vintage),
                    balance.Volume)),
        ];

        List<LedgerBalanceSnapshot> escrowed =
        [
            .. run.LedgerEscrowed
                .OrderBy(balance => balance.CompanyId)
                .ThenBy(balance => balance.ProductKind)
                .ThenBy(balance => balance.Vintage)
                .Select(balance => new LedgerBalanceSnapshot(
                    balance.CompanyId,
                    new ProductSnapshot(balance.ProductKind, balance.Vintage),
                    balance.Volume)),
        ];

        List<VintageVolumeSnapshot> reserve =
        [
            .. run.GovernmentReserves
                .OrderBy(vintage => vintage.Vintage)
                .Select(vintage => new VintageVolumeSnapshot(vintage.Vintage, vintage.Volume)),
        ];

        List<VintageVolumeSnapshot> issued =
        [
            .. run.GovernmentIssued
                .OrderBy(vintage => vintage.Vintage)
                .Select(vintage => new VintageVolumeSnapshot(vintage.Vintage, vintage.Volume)),
        ];

        return new SimulationSnapshot(
            id,
            run.Root.Name,
            run.Root.Seed,
            run.Root.RandomState,
            run.Root.State,
            run.Root.CurrentYear,
            new TradingSystemSnapshot(
                system.TradingSystemId,
                system.Name,
                new ParametersSnapshot(
                    parameters.Cap,
                    parameters.AnnualCapReductionRate,
                    parameters.FreeAllocationShare,
                    parameters.Years,
                    [
                        .. run.BausGrowth
                            .OrderBy(growth => growth.BausGrowthId)
                            .Select(growth => new SectorBausGrowthSnapshot(growth.Sector, growth.MinAnnualRate, growth.MaxAnnualRate)),
                    ],
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
                    parameters.OverdraftInterestRate)),
            [
                .. run.Sectors
                    .OrderBy(sector => sector.Name, StringComparer.Ordinal)
                    .Select(sector => new SectorSnapshot(
                        sector.Name,
                        sector.EmissionShare,
                        [
                            .. menuBySector[sector.Name]
                                .OrderBy(entry => entry.Ordinal)
                                .Select(entry => new AbatementMenuSnapshot(
                                    entry.Code,
                                    entry.Name,
                                    entry.AnnualReductionShare,
                                    entry.UpfrontCostPerTonne,
                                    entry.AnnualNetRevenuePerTonne,
                                    entry.ImplementationYears,
                                    entry.LifetimeYears)),
                        ])),
            ],
            [
                .. run.Players
                    .OrderBy(player => player.PlayerId)
                    .Select(player => new PlayerSnapshot(player.PlayerId, player.Name, player.Kind)),
            ],
            [
                .. run.Companies
                    .OrderBy(company => company.CompanyId)
                    .Select(company => new CompanySnapshot(
                        company.CompanyId,
                        company.Name,
                        company.Sector,
                        company.OwnerPlayerId,
                        company.Capital,
                        company.EscrowedCash,
                        company.OverdraftLimit)),
            ],
            [
                .. run.Units
                    .OrderBy(unit => unit.UnitId)
                    .Select(unit => new UnitSnapshot(
                        unit.UnitId,
                        unit.Name,
                        unit.CompanyId,
                        unit.BaselineEmissions,
                        unit.NormalOperatingProfit,
                        unit.AutoTrade,
                        [
                            .. optionsByUnit[unit.UnitId]
                                .OrderBy(option => option.Ordinal)
                                .Select(option => new AbatementOptionSnapshot(
                                    option.Code,
                                    option.Name,
                                    option.UpfrontCost,
                                    option.AnnualReduction,
                                    option.ImplementationYears,
                                    option.LifetimeYears,
                                    option.AnnualNetRevenue)),
                        ])),
            ],
            new AllocationPlanSnapshot(
                [
                    .. run.AllocationYears.OrderBy(year => year.Year).Select(year => year.Cap),
                ],
                [
                    .. run.AllocationYears.OrderBy(year => year.Year).Select(year => year.FreeAllocation),
                ],
                [
                    .. run.UnitAllocations
                        .OrderBy(allocation => allocation.UnitId)
                        .Select(allocation => new UnitAllocationSnapshot(
                            allocation.UnitId,
                            allocation.BausGrowth,
                            [
                                .. allocationYearsByUnit[allocation.UnitId].OrderBy(year => year.Year).Select(year => year.FreeAllocation),
                            ],
                            [
                                .. allocationYearsByUnit[allocation.UnitId].OrderBy(year => year.Year).Select(year => year.BausEmissions),
                            ])),
                ]),
            new AbatementPortfolioSnapshot(
                [
                    .. run.ImplementedAbatements
                        .OrderBy(project => project.UnitId)
                        .ThenBy(project => project.OptionIndex)
                        .Select(project => new ImplementedAbatementSnapshot(
                            project.UnitId,
                            project.OptionIndex,
                            project.OptionCode,
                            project.ImplementedIn,
                            project.OperatingFromYear,
                            project.ExpiresAfterYear)),
                ],
                [
                    .. run.Shutdowns
                        .OrderBy(shutdown => shutdown.UnitId)
                        .ThenBy(shutdown => shutdown.Year)
                        .Select(shutdown => new UnitYearSnapshot(shutdown.UnitId, shutdown.Year)),
                ]),
            new AllowanceLedgerSnapshot(
                available,
                escrowed,
                [.. run.LedgerGrantedYears.OrderBy(year => year.Year).Select(year => year.Year)]),
            new GovernmentSnapshot(
                run.Government?.Revenue ?? 0m,
                run.Government?.HasIssued ?? false,
                reserve,
                issued),
            new CashLedgerSnapshot(
                [
                    .. run.CashMovements
                        .OrderBy(movement => movement.Sequence)
                        .Select(movement => new CashMovementSnapshot(
                            movement.Sequence,
                            movement.Year,
                            movement.CompanyId,
                            movement.Category,
                            movement.Amount,
                            movement.Description)),
                ]),
            new ComplianceRegisterSnapshot(
                [
                    .. run.Compliances
                        .OrderBy(result => result.Year)
                        .ThenBy(result => result.CompanyId)
                        .Select(result => new CompanyComplianceSnapshot(
                            result.Year,
                            result.CompanyId,
                            result.Obligation,
                            result.OffsetsSurrendered,
                            result.AllowancesSurrendered,
                            result.Banked,
                            result.Forfeited,
                            result.Shortfall,
                            result.PenaltyCash,
                            result.PenaltyAllowanceDebit)),
                ],
                [
                    .. run.Fines
                        .OrderBy(fine => fine.FineId)
                        .Select(fine => new FineSnapshot(
                            fine.FineId,
                            fine.UnitId,
                            fine.CompanyId,
                            fine.Amount,
                            fine.Description,
                            fine.Year)),
                ],
                [
                    .. run.Reconciled
                        .OrderBy(reconciled => reconciled.CompanyId)
                        .ThenBy(reconciled => reconciled.Year)
                        .Select(reconciled => new CompanyYearSnapshot(reconciled.CompanyId, reconciled.Year)),
                ]),
            new MarketJournalSnapshot(
                [
                    .. run.JournalTrades
                        .OrderBy(trade => trade.Sequence)
                        .Select(trade => new MarketTradeSnapshot(
                            trade.Sequence,
                            trade.Year,
                            trade.Channel,
                            new ProductSnapshot(trade.ProductKind, trade.Vintage),
                            trade.Price,
                            trade.Volume,
                            trade.BuyerCompanyId,
                            trade.SellerCompanyId)),
                ]),
            new ExchangeSnapshot(
                [
                    .. run.OrderBooks
                        .OrderBy(book => book.ProductKind)
                        .ThenBy(book => book.Vintage)
                        .Select(book => new OrderBookSnapshot(
                            new ProductSnapshot(book.ProductKind, book.Vintage),
                            book.LastTradePrice,
                            [
                                .. ordersByBook[(book.ProductKind, book.Vintage)]
                                    .OrderBy(order => order.OrderId)
                                    .Select(order => new OrderSnapshot(
                                        order.OrderId,
                                        order.UnitId,
                                        order.CompanyId,
                                        new ProductSnapshot(order.ProductKind, order.Vintage),
                                        order.Side,
                                        order.Kind,
                                        order.FillPolicy,
                                        order.Volume,
                                        order.Price,
                                        order.StopPrice,
                                        order.FilledVolume,
                                        order.Status,
                                        order.EscrowedCash)),
                            ],
                            [
                                .. tradesByBook[(book.ProductKind, book.Vintage)]
                                    .OrderBy(trade => trade.Sequence)
                                    .Select(trade => new BookTradeSnapshot(
                                        trade.Sequence,
                                        new ProductSnapshot(trade.ProductKind, trade.Vintage),
                                        trade.BuyOrderId,
                                        trade.SellOrderId,
                                        trade.Price,
                                        trade.Volume)),
                            ])),
                ],
                run.Exchange?.LastOrderId ?? 0L),
            new OtcMarketSnapshot(
                [
                    .. run.OtcOffers
                        .OrderBy(offer => offer.OfferId)
                        .Select(offer => new OtcOfferSnapshot(
                            offer.OfferId,
                            offer.SellerUnitId,
                            offer.BuyerUnitId,
                            new ProductSnapshot(offer.ProductKind, offer.Vintage),
                            offer.Price,
                            offer.Volume,
                            offer.State,
                            offer.Year)),
                ]),
            new AuctionScheduleSnapshot(
                [
                    .. run.Auctions
                        .OrderBy(auction => auction.Year)
                        .ThenBy(auction => auction.Sequence)
                        .Select(auction => new AuctionSnapshot(
                            auction.Year,
                            auction.Sequence,
                            [
                                .. lotsByAuction[(auction.Year, auction.Sequence)]
                                    .OrderBy(lot => lot.Vintage)
                                    .Select(lot => new AuctionLotSnapshot(lot.Vintage, lot.Volume, lot.IsForward)),
                            ],
                            auction.IsCleared,
                            auction.UnsoldVolume,
                            [
                                .. bidsByAuction[(auction.Year, auction.Sequence)]
                                    .OrderBy(bid => bid.BidId)
                                    .Select(bid => new AuctionBidSnapshot(
                                        bid.BidId,
                                        bid.UnitId,
                                        bid.CompanyId,
                                        bid.Vintage,
                                        bid.Price,
                                        bid.Volume,
                                        bid.Won)),
                            ],
                            [
                                .. resultsByAuction[(auction.Year, auction.Sequence)]
                                    .OrderBy(result => result.Vintage)
                                    .Select(result => new AuctionResultSnapshot(
                                        result.Vintage,
                                        result.OfferedVolume,
                                        result.ClearingPrice,
                                        [
                                            .. awardsByResult[(result.Year, result.Sequence, result.Vintage)]
                                                .OrderBy(award => award.BidId)
                                                .Select(award => new AuctionAwardSnapshot(award.BidId, award.Volume, award.Cost)),
                                        ],
                                        [
                                            .. rejectedByResult[(result.Year, result.Sequence, result.Vintage)]
                                                .OrderBy(rejected => rejected.BidId)
                                                .Select(rejected => rejected.BidId),
                                        ])),
                            ])),
                ]),
            SnapshotClock(run),
            run.Bots.Count == 0
                ? null
                : new BotsSnapshot(
                    [
                        .. run.Bots
                            .OrderBy(bot => bot.BotId)
                            .Select(bot => new BotSnapshot(
                                bot.CompanyId,
                                [.. unitsByBot[bot.BotId].OrderBy(unit => unit.UnitId).Select(unit => unit.UnitId)],
                                new BotSettingsSnapshot(
                                    bot.Difficulty,
                                    bot.AbatementMargin,
                                    bot.BidPriceNoise,
                                    bot.BidVolumeFraction,
                                    bot.OffsetDiscount,
                                    bot.ReservationPriceFactor),
                                bot.ExpectedPrice,
                                [
                                    .. triggerTimesByBot[bot.BotId]
                                        .OrderBy(time => time.Trigger)
                                        .Select(time => new BotTriggerTimeSnapshot(time.Trigger, time.AtFractionOfYear)),
                                ],
                                [
                                    .. doneByBot[bot.BotId]
                                        .OrderBy(done => done.Year)
                                        .ThenBy(done => done.Trigger)
                                        .Select(done => new BotTriggerDoneSnapshot(done.Year, done.Trigger)),
                                ],
                                [
                                    .. sectionsByBot[bot.BotId]
                                        .OrderBy(section => section.Year)
                                        .ThenBy(section => section.Section)
                                        .Select(section => new BotSectionSnapshot(section.Year, section.Section)),
                                ])),
                    ]));
    }
    /// </summary>
    private static IEnumerable<BotRecord> BotRecords(Guid id, SimulationSnapshot snapshot)
    {
        if (snapshot.Bots is null)
        {
            yield break;
        }

        for (int index = 0; index < snapshot.Bots.Bots.Count; index++)
        {
            BotSnapshot bot = snapshot.Bots.Bots[index];

            yield return new BotRecord
            {
                SimulationId = id,
                BotId = index + 1,
                CompanyId = bot.CompanyId,
                ExpectedPrice = bot.ExpectedPrice,
                Difficulty = bot.Settings.Difficulty,
                AbatementMargin = bot.Settings.AbatementMargin,
                BidPriceNoise = bot.Settings.BidPriceNoise,
                BidVolumeFraction = bot.Settings.BidVolumeFraction,
                OffsetDiscount = bot.Settings.OffsetDiscount,
                ReservationPriceFactor = bot.Settings.ReservationPriceFactor,
            };
        }
    }

    private static ClockSnapshot SnapshotClock(SavedRun run)
    {
        ClockRecord clock = run.Clock ?? new ClockRecord
        {
            SimulationId = run.Root.SimulationId,
            State = run.Root.State,
            CurrentYear = run.Root.CurrentYear,
        };

        return new ClockSnapshot(
            clock.State,
            clock.CurrentYear,
            clock.Elapsed,
            clock.RunningSince,
            clock.HaltAt,
            clock.OpenNoticedThrough,
            clock.OpenedThrough,
            clock.CloseNoticedThrough,
            clock.ClosedThrough,
            clock.HaltedForYearEnd,
            clock.PauseAfterAuction,
            clock.AuctionNotice);
    }
}
