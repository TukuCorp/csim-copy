using System.Globalization;
using System.Text;
using CarbonSim.Engine.Allocation;
using CarbonSim.Engine.Bots;
using CarbonSim.Engine.Clock;
using CarbonSim.Engine.Domain;
using CarbonSim.Engine.Market;
using CarbonSim.Engine.Market.Exchange;
using CarbonSim.Engine.Market.Otc;
using CarbonSim.Engine.Reporting;
using CarbonSim.Engine.Run;
using CarbonSim.Engine.Scenarios;

namespace CarbonSim.Engine.Tests;

/// <summary>What one year of the run had to cover and everything it had to cover it with.</summary>
internal sealed record YearSupply(
    int Year,
    decimal Obligation,
    decimal FreeAllocation,
    decimal AuctionOffered,
    decimal AuctionBid,
    decimal AuctionSold,
    decimal ExchangeTraded,
    decimal OffsetsGranted,
    decimal OffsetsSurrendered,
    decimal Abatement,
    decimal Banked,
    decimal Shortfall);

/// <summary>What a seeded run of the shipped scenario produced.</summary>
internal sealed record FidelityMeasurements(
    int Units,
    int AutomatedUnits,
    int Years,
    decimal CapYear1,
    decimal AuctionableYear1,
    decimal[] AuctionAveragePriceByYear,
    decimal AuctionVolumeYear1,
    decimal AllowanceAveragePrice,
    decimal OffsetAveragePrice,
    decimal OffsetDiscountPercent,
    decimal Vintage1AveragePrice,
    decimal Vintage2AveragePrice,
    decimal Vintage3AveragePrice,
    decimal AbatementYear1,
    decimal AbatementShareOfCapPercent,
    decimal OffsetsSurrenderedYear1,
    decimal OffsetsShareOfCapPercent,
    int PenaltyCount,
    decimal PenaltyValue,
    decimal TotalShortfall,
    int CompliantCompanies,
    int Companies,
    decimal BotMarginalCostMin,
    decimal BotMarginalCostMax,
    decimal[] ExchangeAveragePriceByYear,
    decimal[] ExchangeVolumeByYear,
    int[] ExchangeFillsByYear,
    int[] RestingBidOrdersByYear,
    int[] RestingAskOrdersByYear,
    decimal[] RestingVolumeByYear,
    int[] PenaltyCountByYear,
    decimal[] PenaltyValueByYear,
    int LongCompaniesYearOne,
    int ShortCompaniesYearOne,
    IReadOnlyList<YearSupply> Supply,
    string ActionDigest,
    int ActionCount)
{
    public string ToMarkdown()
    {
        StringBuilder text = new();
        text.AppendLine(CultureInfo.InvariantCulture, $"# Fidelity run — {Units} units, {Years} years, {AutomatedUnits} automated units");
        text.AppendLine();
        text.AppendLine(CultureInfo.InvariantCulture, $"- Cap year 1: {CapYear1:N0} t; auctionable: {AuctionableYear1:N0} t");
        text.AppendLine(CultureInfo.InvariantCulture, $"- Auction average price by year: {string.Join(", ", AuctionAveragePriceByYear.Select(price => price.ToString("N2", CultureInfo.InvariantCulture)))}");
        text.AppendLine(CultureInfo.InvariantCulture, $"- Auction volume year 1: {AuctionVolumeYear1:N0} t");
        text.AppendLine(CultureInfo.InvariantCulture, $"- Average allowance price: {AllowanceAveragePrice:N2}; offsets: {OffsetAveragePrice:N2}; discount: {OffsetDiscountPercent:N1}%");
        text.AppendLine(CultureInfo.InvariantCulture, $"- Average price by vintage: 1 = {Vintage1AveragePrice:N2}, 2 = {Vintage2AveragePrice:N2}, 3 = {Vintage3AveragePrice:N2}");
        text.AppendLine(CultureInfo.InvariantCulture, $"- Abatement year 1: {AbatementYear1:N0} t ({AbatementShareOfCapPercent:N2}% of the cap)");
        text.AppendLine(CultureInfo.InvariantCulture, $"- Offsets surrendered year 1: {OffsetsSurrenderedYear1:N0} t ({OffsetsShareOfCapPercent:N2}% of the cap)");
        text.AppendLine(CultureInfo.InvariantCulture, $"- Penalties: {PenaltyCount} worth {PenaltyValue:N0}; market shortfall {TotalShortfall:N0} t");
        text.AppendLine(CultureInfo.InvariantCulture, $"- Compliant company-years: {CompliantCompanies} of {Companies * Years}");
        text.AppendLine(CultureInfo.InvariantCulture, $"- Bot cost of compliance per tonne: {BotMarginalCostMin:N2} to {BotMarginalCostMax:N2}");
        text.AppendLine(CultureInfo.InvariantCulture, $"- BotAction log: {ActionCount:N0} actions, sha256 {ActionDigest}");

        text.AppendLine();
        text.AppendLine("| Year | Resting bids | Resting asks | Resting volume | Exchange fills | Exchange volume | Exchange average price | Auction average price | Units penalised | Penalty value |");
        text.AppendLine("|---|---|---|---|---|---|---|---|---|---|");

        for (int index = 0; index < Years; index++)
        {
            text.AppendLine(
                CultureInfo.InvariantCulture,
                $"| {index + 1} | {RestingBidOrdersByYear[index]} | {RestingAskOrdersByYear[index]} | {RestingVolumeByYear[index]:N0} | {ExchangeFillsByYear[index]} | {ExchangeVolumeByYear[index]:N0} | {ExchangeAveragePriceByYear[index]:N2} | {AuctionAveragePriceByYear[index]:N2} | {PenaltyCountByYear[index]} | {PenaltyValueByYear[index]:N0} |");
        }

        text.AppendLine();
        text.AppendLine(CultureInfo.InvariantCulture, $"- Companies allocated above their need in year 1: {LongCompaniesYearOne}; below it: {ShortCompaniesYearOne}");

        text.AppendLine();
        text.AppendLine("| Year | Obligation | Free allocation | Auction offered | Auction bid | Auction sold | Exchange traded | Offsets granted | Offsets surrendered | Abatement | Banked | Shortfall |");
        text.AppendLine("|---|---|---|---|---|---|---|---|---|---|---|---|");

        foreach (YearSupply year in Supply)
        {
            text.AppendLine(
                CultureInfo.InvariantCulture,
                $"| {year.Year} | {year.Obligation:N0} | {year.FreeAllocation:N0} | {year.AuctionOffered:N0} | {year.AuctionBid:N0} | {year.AuctionSold:N0} | {year.ExchangeTraded:N0} | {year.OffsetsGranted:N0} | {year.OffsetsSurrendered:N0} | {year.Abatement:N0} | {year.Banked:N0} | {year.Shortfall:N0} |");
        }

        return text.ToString();
    }
}

/// <summary>
/// Plays the shipped scenario end to end with bots in charge and measures what the fidelity
/// envelopes in the bot brief talk about. Everything is driven by the seeded stream and the
/// injected clock, so a run is reproducible and the test never sleeps.
/// </summary>
internal static class FidelityRun
{
    public static FidelityMeasurements Play(
        BotDifficulty difficulty = BotDifficulty.Normal,
        TimeSpan? tick = null,
        // The administrator's "hand out offsets" action on top of the scenario's own offset supply,
        // which is granted with each year's allocation. None by default: the shipped run is measured
        // on the scenario as it ships; the calibration grid adds some to show the trade-off.
        decimal offsetShareOfEmissions = 0m,
        decimal lumpyOffsetShare = 0m)
    {
        TimeSpan step = tick ?? TimeSpan.FromSeconds(30);
        Simulation simulation = ScenarioLoader.LoadFile(Path.Combine("scenarios", "vietnam-2024.json"));
        Parameters parameters = simulation.Rules;
        TestTimeProvider time = new();

        // The published exercises ran the whole fleet this way: every enterprise is automated.
        foreach (Unit unit in simulation.Units)
        {
            unit.AutoTrade = true;
        }

        SimulationBots bots = SimulationBots.Create(simulation, BotSettings.For(difficulty));
        // The run module plays this run: the same order of operations the host uses, the schedule from
        // the run's own settings, and the year closed by the same verb. Nothing here re-implements it.
        SimulationRun run = SimulationRun.Create(simulation, time, bots);
        Standing standing = new(simulation);
        List<BotAction> botLog = [];
        List<(int Bids, int Asks, decimal Volume)> booksByYear = [];
        (int Bids, int Asks, decimal Volume) peak = (0, 0, 0m);
        List<YearSupply> supplyByYear = [];

        // Year 1: the clock begins and its free allocation is granted, exactly as the host's Start does.
        run.Start();

        for (int year = 1; year <= parameters.Years; year++)
        {
            DisburseOffsets(simulation, standing, year, offsetShareOfEmissions, lumpyOffsetShare);

            decimal obligation = simulation.Companies.Sum(company => standing.Emissions(company, year));
            decimal offsetsGranted = simulation.Companies.Sum(company => simulation.Ledger.Held(company, Product.Offset));
            decimal abatement = simulation.Units.Sum(unit => simulation.Abatements.ReductionsFor(unit, year));

            while (run.Clock.State != SimulationState.YearEnded)
            {
                time.Advance(step);
                botLog.AddRange(run.Advance().BotActions);

                // What the book holds at its widest during the year: the depth a person watching
                // the exchange would have seen, rather than whatever is left when the year closes.
                (int Bids, int Asks, decimal Volume) now = Sample(run.Exchange);

                peak = (
                    Math.Max(peak.Bids, now.Bids),
                    Math.Max(peak.Asks, now.Asks),
                    Math.Max(peak.Volume, now.Volume));

                // The year has run out of time: close it the way the host's End Year control does,
                // which clears the auction still open before it reconciles.
                if (run.Clock.State == SimulationState.TradingHalted)
                {
                    run.EndYear();
                }
            }

            booksByYear.Add(peak);
            peak = (0, 0, 0m);

            Auction[] auctions = [.. Enumerable.Range(1, parameters.AuctionsPerYear).Select(section => run.Auctions.ForSection(year, section))];

            supplyByYear.Add(new YearSupply(
                year,
                obligation,
                simulation.Allocation.FreeAllocationForYear(year),
                auctions.Sum(auction => auction.OfferedVolume),
                auctions.Sum(auction => auction.Bids.Sum(bid => bid.Volume)),
                auctions.Sum(auction => auction.Results.Sum(result => result.VolumeSold)),
                simulation.Journal.Volume(year, year, TradeChannel.Exchange, ProductKind.Allowance),
                offsetsGranted,
                simulation.Compliance.Results.Where(result => result.Year == year).Sum(result => result.OffsetsSurrendered),
                abatement,
                simulation.Compliance.Results.Where(result => result.Year == year).Sum(result => result.Banked),
                simulation.Compliance.Results.Where(result => result.Year == year).Sum(result => result.Shortfall)));

            if (year < parameters.Years)
            {
                run.BeginNextYear();
            }
        }

        run.EndSimulation();

        return Measure(simulation, standing, booksByYear, supplyByYear, botLog);
    }

    /// <summary>Every resting order across the exchange's books, by side.</summary>
    private static (int Bids, int Asks, decimal Volume) Sample(Exchange exchange)
    {
        int bids = 0;
        int asks = 0;
        decimal volume = 0m;

        foreach (Product product in exchange.Products)
        {
            foreach (Order order in exchange.Book(product).OpenOrders)
            {
                volume += order.Volume - order.FilledVolume;

                if (order.Side == OrderSide.Buy)
                {
                    bids++;
                }
                else
                {
                    asks++;
                }
            }
        }

        return (bids, asks, volume);
    }

    /// <summary>
    /// The discount offsets trade at to current-vintage allowances (the brief; FAQ #17): each year's
    /// exchange price of an offset against that year's exchange price of its own vintage, weighted by
    /// the offsets traded. A run-wide average of every allowance trade would set offsets bought in a
    /// dear year 3 against cheap year-1 auction tonnes and read the price path as a premium.
    /// </summary>
    private static decimal OffsetDiscount(Simulation simulation)
    {
        MarketJournal journal = simulation.Journal;
        decimal weighted = 0m;
        decimal volume = 0m;

        foreach (int year in simulation.Allocation.Years)
        {
            MarketTrade[] offsets = [.. journal.In(year, TradeChannel.Exchange, ProductKind.Offset)];
            MarketTrade[] current = [.. journal.In(year, TradeChannel.Exchange, ProductKind.Allowance).Where(trade => trade.Product.Vintage == year)];
            decimal offsetVolume = offsets.Sum(trade => trade.Volume);
            decimal currentVolume = current.Sum(trade => trade.Volume);

            if (offsetVolume <= 0m || currentVolume <= 0m)
            {
                continue;
            }

            decimal offsetPrice = offsets.Sum(trade => trade.Consideration) / offsetVolume;
            decimal currentPrice = current.Sum(trade => trade.Consideration) / currentVolume;

            weighted += (currentPrice - offsetPrice) / currentPrice * 100m * offsetVolume;
            volume += offsetVolume;
        }

        return volume <= 0m ? 0m : weighted / volume;
    }

    /// <summary>Stands in for the administrator's "disburse offsets" action.</summary>
    private static void DisburseOffsets(
        Simulation simulation,
        Standing standing,
        int year,
        decimal shareOfEmissions,
        decimal lumpyShare)
    {
        foreach (Company company in simulation.Companies)
        {
            decimal emissions = standing.Emissions(company, year);
            decimal volume = emissions * shareOfEmissions;

            if (company.Id % 5 == 0)
            {
                volume += emissions * lumpyShare;
            }

            if (volume > 0m)
            {
                simulation.Ledger.Grant(company, Product.Offset, Math.Round(volume, 0, MidpointRounding.AwayFromZero));
            }
        }
    }

    private static FidelityMeasurements Measure(
        Simulation simulation,
        Standing standing,
        IReadOnlyList<(int Bids, int Asks, decimal Volume)> booksByYear,
        IReadOnlyList<YearSupply> supplyByYear,
        IReadOnlyList<BotAction> botLog)
    {
        Parameters parameters = simulation.Rules;
        MarketJournal journal = simulation.Journal;
        decimal capYear1 = simulation.Allocation.CapForYear(1);

        decimal[] auctionPrices =
        [
            .. simulation.Allocation.Years.Select(year => journal.AveragePrice(year, year, TradeChannel.Auction, ProductKind.Allowance)),
        ];

        decimal allowanceAverage = journal.AveragePrice(1, parameters.Years, kind: ProductKind.Allowance);
        decimal offsetAverage = journal.AveragePrice(1, parameters.Years, kind: ProductKind.Offset);
        decimal vintage1 = AverageFor(simulation, 1);
        // The brief's "abatement volume in year 1" is what abatement took off year 1's emissions, as
        // the original reports it ("by the end of this year I will have reduced ..."), not the yearly
        // tonnes of every project bought in year 1, most of which only start running in year 2.
        decimal abatementYear1 = simulation.Units.Sum(unit => simulation.Abatements.ReductionsFor(unit, 1));
        decimal offsetsYear1 = simulation.Compliance.Results
            .Where(result => result.Year == 1)
            .Sum(result => result.OffsetsSurrendered);
        IReadOnlyList<LeaderboardEntry> table = Leaderboard.Rank(simulation);
        LeaderboardEntry[] botEntries = [.. table.Where(entry => entry.IsAutomated)];
        decimal[] exchangePrices =
        [
            .. simulation.Allocation.Years.Select(year => journal.AveragePrice(year, year, TradeChannel.Exchange, ProductKind.Allowance)),
        ];
        decimal[] exchangeVolumes =
        [
            .. simulation.Allocation.Years.Select(year => journal.Volume(year, year, TradeChannel.Exchange, ProductKind.Allowance)),
        ];
        int[] exchangeFills =
        [
            .. simulation.Allocation.Years.Select(year => journal
                .In(year, TradeChannel.Exchange)
                .Count(trade => trade.Product.Kind == ProductKind.Allowance && trade.Buyer is not null)),
        ];
        int[] penaltiesByYear =
        [
            .. simulation.Allocation.Years.Select(year => simulation.Compliance.Results.Count(result => result.Year == year && result.Shortfall > 0m)),
        ];
        decimal[] penaltyValuesByYear =
        [
            .. simulation.Allocation.Years.Select(year => simulation.Compliance.Results.Where(result => result.Year == year).Sum(result => result.PenaltyCash)),
        ];
        // What the year-1 allocation alone leaves each company holding: the mix the market needs.
        int longCompanies = simulation.Companies.Count(company =>
        {
            decimal held = company.Units.Sum(unit => simulation.Allocation.FreeAllocationFor(unit, 1));

            return held > standing.Emissions(company, 1);
        });
        (int actionCount, string actionDigest) = BotLog.Summarise(botLog);

        return new FidelityMeasurements(
            simulation.Units.Count,
            simulation.Units.Count(unit => unit.Company.Owner.Kind == PlayerKind.Ai || unit.AutoTrade),
            parameters.Years,
            capYear1,
            simulation.Allocation.AuctionableVolumeForYear(1),
            auctionPrices,
            journal.Volume(1, 1, TradeChannel.Auction, ProductKind.Allowance),
            allowanceAverage,
            offsetAverage,
            OffsetDiscount(simulation),
            vintage1,
            AverageFor(simulation, 2),
            AverageFor(simulation, 3),
            abatementYear1,
            abatementYear1 / capYear1 * 100m,
            offsetsYear1,
            offsetsYear1 / capYear1 * 100m,
            simulation.Compliance.Results.Count(result => result.Shortfall > 0m),
            simulation.Compliance.Results.Sum(result => result.PenaltyCash),
            simulation.Compliance.Results.Sum(result => result.Shortfall),
            simulation.Compliance.Results.Count(result => result.IsCompliant),
            simulation.Companies.Count,
            botEntries.Length == 0 ? 0m : botEntries.Min(entry => entry.OverallMarginalCostOfCompliance),
            botEntries.Length == 0 ? 0m : botEntries.Max(entry => entry.OverallMarginalCostOfCompliance),
            exchangePrices,
            exchangeVolumes,
            exchangeFills,
            [.. booksByYear.Select(book => book.Bids)],
            [.. booksByYear.Select(book => book.Asks)],
            [.. booksByYear.Select(book => book.Volume)],
            penaltiesByYear,
            penaltyValuesByYear,
            longCompanies,
            simulation.Companies.Count - longCompanies,
            supplyByYear,
            actionDigest,
            actionCount);
    }

    private static decimal AverageFor(Simulation simulation, int vintage)
    {
        MarketTrade[] trades =
        [
            .. simulation.Journal.Trades.Where(trade => trade.Product == Product.Allowance(vintage)),
        ];

        decimal volume = trades.Sum(trade => trade.Volume);

        return volume <= 0m ? 0m : trades.Sum(trade => trade.Consideration) / volume;
    }
}
