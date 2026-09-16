using CarbonSim.Engine.Allocation;
using CarbonSim.Engine.Compliance;
using CarbonSim.Engine.Domain;
using CarbonSim.Engine.Finance;
using CarbonSim.Engine.Market;

namespace CarbonSim.Engine.Reporting;

/// <summary>
/// What a company's compliance cost it, and what it is left holding. The cost of compliance is
/// every compliance-related movement of its money — allowances and offsets it bought, abatement
/// it paid for or earned from, penalties and fines, interest — less what it made selling
/// instruments. Dividing that by what it emitted gives the figure the leaderboard ranks on.
/// </summary>
public static class Scoring
{
    private static readonly CashCategory[] CostCategories =
    [
        CashCategory.AuctionPurchase,
        CashCategory.SecondaryPurchase,
        CashCategory.OtcPurchase,
        CashCategory.AbatementCapital,
        CashCategory.AbatementNetRevenue,
        CashCategory.InstrumentSale,
        CashCategory.Penalty,
        CashCategory.Fine,
        CashCategory.Interest,
    ];

    /// <summary>One company's year: emissions, what it spent, and what that costs per tonne.</summary>
    public static CompanyScore ForYear(Simulation simulation, Company company, int year)
    {
        ArgumentNullException.ThrowIfNull(simulation);
        ArgumentNullException.ThrowIfNull(company);

        decimal emissions = new CompliancePosition(simulation).EmissionsFor(company, year);
        decimal cost = -simulation.Cash.MovementsFor(company, year, CostCategories);
        decimal penalties = simulation.Compliance.Results
            .Where(result => ReferenceEquals(result.Company, company) && result.Year == year)
            .Sum(result => result.PenaltyCash);
        CompanyCompliance? reconciliation = simulation.Compliance.Results
            .FirstOrDefault(result => ReferenceEquals(result.Company, company) && result.Year == year);

        return new CompanyScore(
            company,
            year,
            emissions,
            cost,
            emissions > 0m ? cost / emissions : 0m,
            reconciliation?.Banked ?? 0m,
            reconciliation?.Forfeited ?? 0m,
            reconciliation?.Shortfall ?? 0m,
            penalties);
    }

    /// <summary>The whole run for a company.</summary>
    public static CompanyScore Overall(Simulation simulation, Company company)
    {
        ArgumentNullException.ThrowIfNull(simulation);
        ArgumentNullException.ThrowIfNull(company);

        decimal emissions = 0m;
        decimal cost = 0m;
        decimal banked = 0m;
        decimal forfeited = 0m;
        decimal shortfall = 0m;
        decimal penalties = 0m;

        foreach (int year in simulation.Allocation.Years)
        {
            CompanyScore score = ForYear(simulation, company, year);
            emissions += score.Emissions;
            cost += score.CostOfCompliance;
            banked += score.Banked;
            forfeited += score.Forfeited;
            shortfall += score.Shortfall;
            penalties += score.PenaltyCash;
        }

        return new CompanyScore(
            company,
            simulation.Allocation.Years.Count,
            emissions,
            cost,
            emissions > 0m ? cost / emissions : 0m,
            banked,
            forfeited,
            shortfall,
            penalties);
    }

    /// <summary>
    /// What a company is left holding at the end of the run: its instruments, less anything it
    /// never managed to cover. Positive means it finished long, negative means short.
    /// </summary>
    public static decimal FinalPosition(Simulation simulation, Company company)
    {
        ArgumentNullException.ThrowIfNull(simulation);
        ArgumentNullException.ThrowIfNull(company);

        decimal held = simulation.Ledger.Held(company, Product.Offset);

        for (int vintage = 0; vintage <= simulation.Allocation.Years.Count; vintage++)
        {
            held += simulation.Ledger.Held(company, Product.Allowance(vintage));
        }

        decimal uncovered = simulation.Compliance.Results
            .Where(result => ReferenceEquals(result.Company, company))
            .Sum(result => result.Shortfall);

        return held - uncovered;
    }
}

/// <summary>One company's compliance result for a year, or for the whole run.</summary>
public sealed record CompanyScore(
    Company Company,
    int Year,
    decimal Emissions,
    decimal CostOfCompliance,
    decimal MarginalCostOfCompliance,
    decimal Banked,
    decimal Forfeited,
    decimal Shortfall,
    decimal PenaltyCash)
{
    public bool IsCompliant => Shortfall == 0m;

    public override string ToString() =>
        $"{Company.Name} year {Year}: {CostOfCompliance} for {Emissions} t ({MarginalCostOfCompliance} per tonne)";
}

/// <summary>Where a company sits on the leaderboard.</summary>
public sealed record LeaderboardEntry(
    int Rank,
    Company Company,
    decimal OverallCostOfCompliance,
    decimal OverallMarginalCostOfCompliance,
    decimal FinalPosition,
    bool IsAutomated)
{
    public override string ToString() =>
        $"#{Rank} {Company.Name}: {OverallMarginalCostOfCompliance} per tonne, position {FinalPosition}";
}

/// <summary>
/// The leaderboard: cheapest cost of compliance per tonne first, and where two companies are
/// level, the one left holding the larger position. AI-run companies are flagged so the UI can
/// mark them the way the original does.
/// </summary>
public static class Leaderboard
{
    public static IReadOnlyList<LeaderboardEntry> Rank(Simulation simulation)
    {
        ArgumentNullException.ThrowIfNull(simulation);

        List<LeaderboardEntry> entries = [];

        foreach (Company company in simulation.Companies)
        {
            CompanyScore score = Scoring.Overall(simulation, company);

            entries.Add(new LeaderboardEntry(
                0,
                company,
                score.CostOfCompliance,
                score.MarginalCostOfCompliance,
                Scoring.FinalPosition(simulation, company),
                company.Owner.Kind == PlayerKind.Ai));
        }

        List<LeaderboardEntry> ranked =
        [
            .. entries
                .OrderBy(entry => entry.OverallMarginalCostOfCompliance)
                .ThenByDescending(entry => entry.FinalPosition)
                .ThenBy(entry => entry.Company.Id)
                .Select((entry, index) => entry with { Rank = index + 1 }),
        ];

        return ranked;
    }
}
