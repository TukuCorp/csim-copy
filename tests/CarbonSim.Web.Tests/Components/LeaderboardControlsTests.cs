using Bunit;
using CarbonSim.Web.Admin;
using CarbonSim.Web.Components.Admin;
using CarbonSim.Web.Components.Pages;
using CarbonSim.Web.Player;
using FluentAssertions;

namespace CarbonSim.Web.Tests.Components;

/// <summary>
/// The leaderboard reads for the whole run or for one year (catalog `LeaderboardYear_X`), can be
/// filtered by sector (`FilterBySector`), and shows each company's penalties, on the player's screen
/// and on the console alike.
/// </summary>
public sealed class LeaderboardControlsTests : ScreenTestContext
{
    private static readonly LeaderboardRow Power = new(1, "Delta Power", "Delta Unit 1", "Delta Power", 250_000m, 2.5m, 0m, false, true)
    {
        Sector = "Power",
        Penalties = 0m,
    };

    private static readonly LeaderboardRow Cement = new(2, "Ha Long Cement", "Ha Long Kiln", "Ha Long Cement", 900_000m, 9.1m, 1_000m, false, false)
    {
        Sector = "Cement",
        Penalties = 300_000m,
    };

    public LeaderboardControlsTests()
    {
        View = PlayerViewFixture.Sample(SimulationId) with
        {
            Leaderboard = [Power, Cement],
            LeaderboardYears = [new LeaderboardYearView(1, [Cement with { Rank = 1, Penalties = 0m }, Power with { Rank = 2 }])],
        };
    }

    [Fact]
    public void The_whole_run_board_shows_every_company_with_its_penalties()
    {
        IRenderedComponent<Leaderboard> page = Render();

        Companies(page).Should().Equal("Delta Power", "Ha Long Cement");
        page.Find("#leaderboard-table").TextContent.Should().Contain("300,000");
    }

    [Fact]
    public void Choosing_a_year_shows_that_years_ranking()
    {
        IRenderedComponent<Leaderboard> page = Render();

        page.Find("#leaderboard-period").Change("1");

        Companies(page).Should().Equal("Ha Long Cement", "Delta Power");
    }

    [Fact]
    public void Filtering_by_sector_keeps_only_that_sectors_companies()
    {
        IRenderedComponent<Leaderboard> page = Render();

        page.Find("#leaderboard-sector").Change("Cement");

        Companies(page).Should().Equal("Ha Long Cement");
    }

    [Fact]
    public void The_console_board_has_the_same_year_and_sector_controls()
    {
        AdminRunDetail detail = AdminViewFixture.Detail(SimulationId, "Running");
        AdminLeaderView power = new(1, "Delta Power", "Delta Unit 1", "Delta Power", 2.5m, 0m, false) { Sector = "Power" };
        AdminLeaderView cement = new(2, "Ha Long Cement", "Ha Long Kiln", "Ha Long Cement", 9.1m, 0m, false) { Sector = "Cement", Penalties = 300_000m };
        AdminSnapshot snapshot = AdminViewFixture.Sample(SimulationId) with
        {
            Run = detail with
            {
                Leaderboard = [power, cement],
                LeaderboardYears = [new AdminLeaderYearView(1, [cement with { Rank = 1 }, power with { Rank = 2 }])],
            },
        };

        IRenderedComponent<AdminReports> page = RenderComponent<AdminReports>(parameters => parameters
            .AddCascadingValue(snapshot)
            .Add(component => component.SimulationId, SimulationId));

        page.Find("#admin-leaderboard-table").TextContent.Should().Contain("300,000");
        page.Find("#admin-leaderboard-period").Change("1");
        page.Find("#admin-leaderboard-sector").Change("Power");

        page.FindAll("#admin-leaderboard-table tbody tr").Should().ContainSingle()
            .Which.TextContent.Should().Contain("Delta Power");
    }

    private IRenderedComponent<Leaderboard> Render() =>
        RenderComponent<Leaderboard>(parameters => parameters
            .AddCascadingValue(Snapshot)
            .Add(component => component.SimulationId, SimulationId));

    private static List<string> Companies(IRenderedComponent<Leaderboard> page) =>
        [.. page.FindAll("#leaderboard-table tbody tr td.leaderboard-company").Select(cell => cell.TextContent.Trim().TrimEnd('*').Trim())];
}
