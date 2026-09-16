using CarbonSim.Engine.Abatement;
using CarbonSim.Engine.Allocation;
using CarbonSim.Engine.Compliance;
using CarbonSim.Engine.Domain;
using CarbonSim.Engine.Market;

namespace CarbonSim.Engine.Reporting;

/// <summary>
/// The system report an administrator and the players both look at: what the fleet is emitting,
/// what it surrendered, what the markets did, what abatement it undertook, and what penalties
/// fell — for the year in question and for the run so far.
/// </summary>
public sealed class SystemReport
{
    private SystemReport(int year, SystemTotals thisYear, SystemTotals toDate)
    {
        Year = year;
        ThisYear = thisYear;
        ToDate = toDate;
    }

    public int Year { get; }

    public SystemTotals ThisYear { get; }

    public SystemTotals ToDate { get; }

    public static SystemReport For(Simulation simulation, int year)
    {
        ArgumentNullException.ThrowIfNull(simulation);
        RequireYear(simulation, year);

        return new SystemReport(
            year,
            Totals(simulation, year, year),
            Totals(simulation, 1, year));
    }

    private static SystemTotals Totals(Simulation simulation, int from, int to)
    {
        CompliancePosition position = new(simulation);
        MarketJournal journal = simulation.Journal;

        decimal forecastEmissions = 0m;
        decimal allowancesSurrendered = 0m;
        decimal offsetsSurrendered = 0m;
        decimal banked = 0m;
        decimal forfeited = 0m;
        int penaltyCount = 0;
        decimal penaltyValue = 0m;
        decimal fineValue = 0m;
        decimal emissionsReduced = 0m;

        for (int year = from; year <= to; year++)
        {
            foreach (Company company in simulation.Companies)
            {
                forecastEmissions += position.EmissionsFor(company, year);
            }

            foreach (CompanyCompliance result in simulation.Compliance.Results.Where(result => result.Year == year))
            {
                allowancesSurrendered += result.AllowancesSurrendered;
                offsetsSurrendered += result.OffsetsSurrendered;
                banked += result.Banked;
                forfeited += result.Forfeited;

                if (result.Shortfall > 0m)
                {
                    penaltyCount++;
                    penaltyValue += result.PenaltyCash;
                }
            }

            emissionsReduced += simulation.Units.Sum(unit => simulation.Abatements.ReductionsFor(unit, year));
        }

        foreach (Fine fine in simulation.Compliance.Fines.Where(fine => fine.Year >= from && fine.Year <= to))
        {
            fineValue += fine.Amount;
        }

        ImplementedAbatement[] projects =
        [
            .. simulation.Units
                .SelectMany(unit => simulation.Abatements.For(unit))
                .Where(project => project.ImplementedIn >= from && project.ImplementedIn <= to),
        ];

        // Every trade has two sides, so at system level what the fleet sold is what it bought;
        // the per-company figures are the ones that differ.
        decimal allowanceVolume = journal.Volume(from, to, kind: ProductKind.Allowance);
        decimal offsetVolume = journal.Volume(from, to, kind: ProductKind.Offset);

        return new SystemTotals(
            forecastEmissions,
            allowancesSurrendered,
            offsetsSurrendered,
            banked,
            forfeited,
            allowanceVolume - journal.Volume(from, to, TradeChannel.Auction),
            allowanceVolume - journal.Volume(from, to, TradeChannel.Auction),
            offsetVolume,
            offsetVolume,
            journal.Volume(from, to, TradeChannel.Auction),
            journal.Consideration(from, to, TradeChannel.Auction),
            journal.AveragePrice(from, to, kind: ProductKind.Allowance),
            journal.AveragePrice(from, to, kind: ProductKind.Offset),
            projects.Length,
            projects.Sum(project => project.Option.AnnualReduction),
            emissionsReduced,
            penaltyCount,
            penaltyValue,
            fineValue);
    }

    private static void RequireYear(Simulation simulation, int year)
    {
        if (year < 1 || year > simulation.Allocation.Years.Count)
        {
            throw new ArgumentOutOfRangeException(
                nameof(year),
                year,
                $"This simulation runs for {simulation.Allocation.Years.Count} years.");
        }
    }
}

/// <summary>The report's figures, all in tonnes CO2e except the money ones.</summary>
public sealed record SystemTotals(
    decimal ForecastEmissions,
    decimal AllowancesSurrendered,
    decimal OffsetsSurrendered,
    decimal Banked,
    decimal Forfeited,
    decimal AllowancesSold,
    decimal AllowancesBought,
    decimal OffsetsSold,
    decimal OffsetsBought,
    decimal AuctionVolume,
    decimal AuctionRevenue,
    decimal AverageAllowancePrice,
    decimal AverageOffsetPrice,
    int AbatementsImplemented,
    decimal AbatementTonnes,
    decimal EmissionsReduced,
    int PenaltyCount,
    decimal PenaltyValue,
    decimal FineValue);
