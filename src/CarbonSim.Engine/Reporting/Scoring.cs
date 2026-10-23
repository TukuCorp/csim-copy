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

        // The one module that answers where a company stands: its emissions here, its settlement below.
        Standing standing = new(simulation);
        decimal emissions = standing.Emissions(company, year);
        decimal cost = -simulation.Cash.MovementsFor(company, year, CostCategories);
        CompanyCompliance? reconciliation = simulation.Compliance.For(company, year);
        decimal penalties = reconciliation?.PenaltyCash ?? 0m;

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
    /// What a company is left holding: its instruments, less anything it never managed to cover.
    /// Positive means it finished long, negative means short.
    /// </summary>
    public static decimal Holdings(Simulation simulation, Company company)
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

    /// <summary>
    /// The position the leaderboard shows: where the company stands in the year the run is
    /// playing, against that year's forecast emissions - the same figure the player's own position
    /// panel reads. Once that year has been reconciled the obligation is settled, and what the
    /// company was left holding, less anything it could not cover, is the position it finished in.
    /// Signed the way every other position is: positive is short, negative is long.
    /// </summary>
    public static decimal FinalLongShortPosition(Simulation simulation, Company company)
    {
        ArgumentNullException.ThrowIfNull(simulation);
        ArgumentNullException.ThrowIfNull(company);

        int year = Math.Max(1, Math.Min(simulation.CurrentYear, simulation.Allocation.Years.Count));
        bool settled = simulation.Compliance.Results
            .Any(result => ReferenceEquals(result.Company, company) && result.Year == year);

        // The live year asks Standing, which counts an escrowed allowance as the company's. The settled
        // year keeps the left-holding rule for now: that is a different question (what it finished
        // holding, not the settled shortfall) and moving it is a decision, not a refactor.
        return settled
            ? -Holdings(simulation, company)
            : -new Standing(simulation).Position(company, year);
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
    bool IsAutomated,
    bool InCompliance)
{
    /// <summary>The cash penalties the company paid over the board's period.</summary>
    public decimal Penalties { get; init; }

    public override string ToString() =>
        $"#{Rank} {Company.Name}: {OverallMarginalCostOfCompliance} per tonne, position {FinalPosition}";
}

/// <summary>
/// The leaderboard: companies in compliance first, because a short player is not eligible to win
/// however cheap its compliance (FAQ #21; the spec ranks on cost and compliance status); then the
/// cheapest cost of compliance per tonne, and where two companies are level, the one left holding
/// the larger position. AI-run companies are flagged so the UI can mark them the way the original does.
/// </summary>
public static class Leaderboard
{
    /// <summary>The board for the whole run.</summary>
    public static IReadOnlyList<LeaderboardEntry> Rank(Simulation simulation)
    {
        ArgumentNullException.ThrowIfNull(simulation);

        Standing standing = new(simulation);
        int year = Math.Max(1, Math.Min(simulation.CurrentYear, simulation.Allocation.Years.Count));

        return Ranked(
            simulation,
            company => Scoring.Overall(simulation, company),
            // Every closed year covered, and the year in play covered as it stands.
            (company, score) => score.Shortfall == 0m
                && (standing.IsSettled(company, year) || standing.Shortfall(company, year) == 0m),
            company => -simulation.Allocation.Years.Sum(each => simulation.Cash.MovementsFor(company, each, CashCategory.Penalty)));
    }

    /// <summary>
    /// The board for one year (catalog `LeaderboardYear_X`): that year's cost per tonne, and compliance
    /// as that year settled, or as it stands while it is live.
    /// </summary>
    public static IReadOnlyList<LeaderboardEntry> RankYear(Simulation simulation, int year)
    {
        ArgumentNullException.ThrowIfNull(simulation);

        Standing standing = new(simulation);

        return Ranked(
            simulation,
            company => Scoring.ForYear(simulation, company, year),
            (company, _) => standing.Covered(company, year),
            company => -simulation.Cash.MovementsFor(company, year, CashCategory.Penalty));
    }

    private static List<LeaderboardEntry> Ranked(
        Simulation simulation,
        Func<Company, CompanyScore> score,
        Func<Company, CompanyScore, bool> inCompliance,
        Func<Company, decimal> penalties)
    {
        List<LeaderboardEntry> entries = [];

        foreach (Company company in simulation.Companies)
        {
            CompanyScore scored = score(company);

            entries.Add(new LeaderboardEntry(
                0,
                company,
                scored.CostOfCompliance,
                scored.MarginalCostOfCompliance,
                Scoring.FinalLongShortPosition(simulation, company),
                company.Owner.Kind == PlayerKind.Ai,
                inCompliance(company, scored))
            {
                Penalties = penalties(company),
            });
        }

        // Level on cost per tonne, the company that was left holding more ranks first.
        return
        [
            .. entries
                .OrderByDescending(entry => entry.InCompliance)
                .ThenBy(entry => entry.OverallMarginalCostOfCompliance)
                .ThenByDescending(entry => Scoring.Holdings(simulation, entry.Company))
                .ThenBy(entry => entry.Company.Id)
                .Select((entry, index) => entry with { Rank = index + 1 }),
        ];
    }
}
