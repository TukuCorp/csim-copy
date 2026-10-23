using CarbonSim.Engine.Clock;
using CarbonSim.Engine.Domain;
using CarbonSim.Engine.Finance;
using CarbonSim.Engine.Reporting;
using FluentAssertions;

namespace CarbonSim.Engine.Tests.Reporting;

/// <summary>
/// The leaderboard can be read for one year (catalog `LeaderboardYear_X`) as well as for the whole run,
/// and every entry carries the penalties it paid, so a company that was penalised shows it.
/// </summary>
public sealed class YearLeaderboardTests
{
    [Fact]
    public void A_years_board_ranks_on_that_years_cost_alone_and_shows_the_penalty()
    {
        Simulation simulation = TestSimulation.Build();
        SimulationClock clock = new(simulation, new TestTimeProvider());
        clock.Start();
        Company first = simulation.FindCompany(1);
        Company second = simulation.FindCompany(2);

        // Year 1: the first company pays a lot, the second nothing.
        simulation.Cash.Withdraw(first, 900_000m, CashCategory.AuctionPurchase, "year 1 buying");
        clock.RequestHalt();
        clock.EndYear();
        clock.BeginNextYear();

        // Year 2: the second company is fined on its shortfall; the first spends nothing.
        simulation.Cash.Charge(second, 600_000m, CashCategory.Penalty, "year 2 shortfall");

        IReadOnlyList<LeaderboardEntry> year1 = Leaderboard.RankYear(simulation, 1);
        IReadOnlyList<LeaderboardEntry> year2 = Leaderboard.RankYear(simulation, 2);

        Ranked(year1, second).Should().BeLessThan(Ranked(year1, first), "in year 1 the second company spent nothing");
        Ranked(year2, first).Should().BeLessThan(Ranked(year2, second), "in year 2 the second company was the dear one");
        year2.Single(entry => entry.Company == second).Penalties.Should().Be(600_000m);
        year1.Single(entry => entry.Company == second).Penalties.Should().Be(0m);
        Leaderboard.Rank(simulation).Single(entry => entry.Company == second).Penalties.Should().Be(600_000m, "the overall board adds every year's");
    }

    private static int Ranked(IReadOnlyList<LeaderboardEntry> board, Company company) =>
        board.Single(entry => entry.Company == company).Rank;
}
