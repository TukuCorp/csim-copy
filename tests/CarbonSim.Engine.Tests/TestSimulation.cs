using CarbonSim.Engine.Domain;

namespace CarbonSim.Engine.Tests;

/// <summary>
/// A hand-driven <see cref="TimeProvider"/>: engine tests advance virtual time explicitly so
/// no test ever sleeps.
/// </summary>
internal sealed class TestTimeProvider : TimeProvider
{
    private DateTimeOffset _now = new(2026, 9, 16, 8, 0, 0, TimeSpan.Zero);

    public override DateTimeOffset GetUtcNow() => _now;

    public void Advance(TimeSpan amount) => _now = _now.Add(amount);
}

/// <summary>
/// The simulation shape shared by the engine tests: two sectors with different shares, four
/// units across three companies (one company owning two units), abatement menus on both
/// sectors, and capital the tests can spend.
/// </summary>
internal static class TestSimulation
{
    public const decimal PowerShare = 0.6m;
    public const decimal CementShare = 0.4m;

    public const decimal Cap = 9_500_000m;
    public const decimal PowerUnitBaseline = 2_000_000m;
    public const decimal CementUnitBaseline = 4_000_000m;
    public const decimal CompanyCapital = 1_000_000_000m;

    public static Parameters Parameters(int years = 3, int auctionsPerYear = 4, TimeSpan? auctionDuration = null)
    {
        return new Parameters
        {
            Cap = Cap,
            AnnualCapReductionRate = 0.03m,
            FreeAllocationShare = 0.90m,
            Years = years,
            OffsetUsageLimit = 0.10m,
            BankingLimit = 1.00m,
            PenaltyPerTonne = 300m,
            PenaltyAllowanceDebit = 1m,
            AuctionFloorPrice = 100m,
            AuctionCeilingPrice = 300m,
            AuctionsPerYear = auctionsPerYear,
            YearLength = TimeSpan.FromMinutes(20),
            AuctionDuration = auctionDuration ?? TimeSpan.FromMinutes(3),
            TradingOpenShareOfYear = 0.60m,
            VolatilityBand = 0.10m,
            OverdraftInterestRate = 0.07m,
            BausGrowthBySector =
            [
                new SectorBausGrowth("Power", 0.02m, 0.06m),
                new SectorBausGrowth("Cement", 0.02m, 0.04m),
            ],
        };
    }

    public static IReadOnlyList<AbatementMenu> PowerMenu =>
    [
        new AbatementMenu("P1", "Boiler and turbine efficiency", 0.05m, 100m, -2m, 1, 10),
        new AbatementMenu("P2", "Carbon capture retrofit", 0.20m, 3000m, -30m, 2, 20),
        new AbatementMenu("P3", "Waste heat recovery", 0.05m, 10m, 50m, 1, 10),
    ];

    public static IReadOnlyList<AbatementMenu> CementMenu =>
    [
        new AbatementMenu("C1", "Kiln thermal efficiency", 0.04m, 250m, 5m, 2, 8),
    ];

    /// <summary>Units: EVN Genco 1 owns two power units, the AI player owns one more, cement one.</summary>
    public static Simulation Build(
        ulong seed = 1,
        int years = 3,
        TimeSpan? auctionDuration = null,
        decimal? capital = null,
        decimal? overdraftLimit = null)
    {
        decimal companyCapital = capital ?? CompanyCapital;
        decimal credit = overdraftLimit ?? 0m;
        Sector power = new("Power", PowerShare, PowerMenu);
        Sector cement = new("Cement", CementShare, CementMenu);

        Player human = new(1, "Nguyen Van A", PlayerKind.Human);
        Player bot = new(2, "AI Unit 2", PlayerKind.Ai);

        Company evn = new(1, "Cong ty Dien luc Binh Minh", power, human, companyCapital, credit);
        Company cementCompany = new(2, "Cong ty Xi mang Da Trang", cement, human, companyCapital, credit);
        Company aiPower = new(3, "AI Power 1", power, bot, companyCapital, credit);

        AddUnit(evn, 1, "Phu My 1", PowerUnitBaseline);
        AddUnit(evn, 2, "Phu My 2", PowerUnitBaseline);
        AddUnit(aiPower, 3, "Song Xanh", PowerUnitBaseline);
        AddUnit(cementCompany, 4, "Kien Giang", CementUnitBaseline);

        TradingSystem system = new(1, "Vietnam ETS", Parameters(years, auctionDuration: auctionDuration), [power, cement], [evn, cementCompany, aiPower]);

        return Simulation.Create("Test simulation", seed, [system], [human, bot]);
    }

    public static Unit Unit(Simulation simulation, int id) => simulation.FindUnit(id);

    public static Company Company(Simulation simulation, int id) => simulation.FindCompany(id);

    public const decimal ProfitPerTonne = 40m;

    private static void AddUnit(Company company, int id, string name, decimal baselineEmissions)
    {
        company.AddUnit(new Unit(
            id,
            name,
            company,
            baselineEmissions,
            AbatementMenu.For(company.Sector, baselineEmissions),
            baselineEmissions * ProfitPerTonne));
    }
}
