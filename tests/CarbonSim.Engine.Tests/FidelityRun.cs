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
using CarbonSim.Engine.Scenarios;

namespace CarbonSim.Engine.Tests;

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
    decimal BotMarginalCostMax)
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
        decimal forwardVintageShare = 0.20m,
        decimal offsetShareOfEmissions = 0.03m,
        decimal lumpyOffsetShare = 0.25m)
    {
        TimeSpan step = tick ?? TimeSpan.FromSeconds(30);
        Simulation simulation = ScenarioLoader.LoadFile(Path.Combine("scenarios", "vietnam-2024.json"));
        Parameters parameters = simulation.TradingSystems.Single().Parameters;
        TestTimeProvider time = new();
        SimulationClock clock = new(simulation, time);

        // The published exercises ran the whole fleet this way: every enterprise is automated.
        foreach (Unit unit in simulation.Units)
        {
            unit.AutoTrade = true;
        }

        SimulationBots bots = SimulationBots.Create(simulation, BotSettings.For(difficulty));
        AuctionSchedule schedule = AuctionSchedule.Build(simulation, forwardVintageShare);
        BotMarkets markets = new(new Exchange(simulation), new OtcMarket(simulation), schedule);
        CompliancePosition position = new(simulation);

        clock.Start();

        for (int year = 1; year <= parameters.Years; year++)
        {
            simulation.Ledger.GrantFreeAllocation(year);
            DisburseOffsets(simulation, position, year, offsetShareOfEmissions, lumpyOffsetShare);

            while (clock.State != SimulationState.YearEnded)
            {
                time.Advance(step);
                IReadOnlyList<ClockEvent> events = clock.Advance();

                foreach (ClockEvent closed in events.Where(@event => @event.Kind == ClockEventKind.AuctionClosed))
                {
                    schedule.ForSection(closed.Year, closed.Auction).Clear();
                }

                bots.Act(simulation, clock, markets);

                if (clock.State == SimulationState.TradingHalted)
                {
                    clock.EndYear();
                }
            }

            simulation.Compliance.Reconcile(year);
            simulation.Finance.CloseYear(year);

            if (year < parameters.Years)
            {
                clock.BeginNextYear();
            }
        }

        clock.EndSimulation();

        return Measure(simulation, position);
    }

    /// <summary>Stands in for the administrator's "disburse offsets" action.</summary>
    private static void DisburseOffsets(
        Simulation simulation,
        CompliancePosition position,
        int year,
        decimal shareOfEmissions,
        decimal lumpyShare)
    {
        foreach (Company company in simulation.Companies)
        {
            decimal emissions = position.EmissionsFor(company, year);
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

    private static FidelityMeasurements Measure(Simulation simulation, CompliancePosition position)
    {
        Parameters parameters = simulation.TradingSystems.Single().Parameters;
        MarketJournal journal = simulation.Journal;
        decimal capYear1 = simulation.Allocation.CapForYear(1);

        decimal[] auctionPrices =
        [
            .. simulation.Allocation.Years.Select(year => journal.AveragePrice(year, year, TradeChannel.Auction, ProductKind.Allowance)),
        ];

        decimal allowanceAverage = journal.AveragePrice(1, parameters.Years, kind: ProductKind.Allowance);
        decimal offsetAverage = journal.AveragePrice(1, parameters.Years, kind: ProductKind.Offset);
        decimal vintage1 = AverageFor(simulation, 1);
        decimal abatementYear1 = simulation.Abatements.All
            .Where(project => project.ImplementedIn == 1)
            .Sum(project => project.Option.AnnualReduction);
        decimal offsetsYear1 = simulation.Compliance.Results
            .Where(result => result.Year == 1)
            .Sum(result => result.OffsetsSurrendered);
        IReadOnlyList<LeaderboardEntry> table = Leaderboard.Rank(simulation);
        LeaderboardEntry[] botEntries = [.. table.Where(entry => entry.IsAutomated)];

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
            allowanceAverage <= 0m ? 0m : (allowanceAverage - offsetAverage) / allowanceAverage * 100m,
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
            botEntries.Length == 0 ? 0m : botEntries.Max(entry => entry.OverallMarginalCostOfCompliance));
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
