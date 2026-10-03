using Bunit;
using CarbonSim.Web.Admin;
using CarbonSim.Web.Components.Admin;
using CarbonSim.Web.Resources;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;

namespace CarbonSim.Web.Tests.Components;

/// <summary>Each console screen renders the run it was handed, and the layout turns a player away.</summary>
public sealed class AdminScreensTests : AdminScreenTestContext
{
    public AdminScreensTests()
    {
        Snapshot = AdminViewFixture.Sample(SimulationId);
    }

    [Fact]
    public void The_runs_screen_lists_the_live_and_saved_exercises()
    {
        IRenderedComponent<AdminRuns> page = RenderComponent<AdminRuns>(parameters => parameters.AddCascadingValue(Snapshot));

        page.Markup.Should().Contain("Vietnam pilot");
        page.FindAll("button").Should().Contain(button => button.TextContent.Contains("Load", StringComparison.Ordinal));
    }

    [Fact]
    public void The_run_screen_shows_the_clock_and_the_units()
    {
        IRenderedComponent<AdminRun> page = Render();

        page.Find("h1").TextContent.Should().Contain("Vietnam pilot");
        page.Markup.Should().Contain("Delta Unit 1");
        page.Find("#admin-pause").Should().NotBeNull();
    }

    [Fact]
    public void The_run_screen_offers_to_start_a_pending_year()
    {
        Snapshot = AdminViewFixture.Sample(SimulationId, state: "Pending", detailState: "Pending");

        IRenderedComponent<AdminRun> page = Render();

        page.Find("#admin-begin-year").Should().NotBeNull();
    }

    [Fact]
    public void The_reports_screen_links_each_export()
    {
        IRenderedComponent<AdminReports> page = RenderComponent<AdminReports>(parameters => parameters
            .AddCascadingValue(Snapshot)
            .Add(component => component.SimulationId, SimulationId));

        foreach (string report in AdminReport.All)
        {
            page.FindAll("a").Should().Contain(link => link.GetAttribute("href")!.Contains($"reports/{report}.csv", StringComparison.Ordinal));
        }
    }

    [Fact]
    public void The_surrender_screen_lists_every_company()
    {
        IRenderedComponent<AdminSurrender> page = RenderComponent<AdminSurrender>(parameters => parameters
            .AddCascadingValue(Snapshot)
            .Add(component => component.SimulationId, SimulationId));

        page.Markup.Should().Contain("Delta Power");
        page.Markup.Should().Contain("Red River Power");
    }

    [Fact]
    public void The_setup_screen_shows_the_parameters_and_registration()
    {
        Session.Draft = Draft();

        IRenderedComponent<AdminSetup> page = RenderComponent<AdminSetup>();

        page.Find("#setup-cap").Should().NotBeNull();
        page.Find("#admin-save-registration").Should().NotBeNull();
        page.Find("#admin-create-run").Should().NotBeNull();
    }

    [Fact]
    public void The_console_layout_turns_a_player_away()
    {
        Session.Administrator = false;
        Session.Snapshot = AdminSnapshot.Denied("A player");

        IRenderedComponent<AdminLayout> layout = RenderComponent<AdminLayout>(parameters =>
            parameters.Add(component => component.Body, builder => { }));

        string denied = Localizer(Services)["AdminAccessDenied"];

        layout.Markup.Should().Contain(denied);
    }

    private IRenderedComponent<AdminRun> Render() => RenderComponent<AdminRun>(parameters => parameters
        .AddCascadingValue(Snapshot)
        .Add(component => component.SimulationId, SimulationId));

    private static IStringLocalizer<SharedStrings> Localizer(IServiceProvider services) =>
        services.GetRequiredService<IStringLocalizer<SharedStrings>>();

    private static ScenarioDraft Draft() => new()
    {
        Name = "Draft run",
        Seed = 5,
        Parameters = new DraftParameters { Cap = 1_000_000m, Years = 3, AuctionsPerYear = 4 },
        Sectors = [new DraftSector { Name = "Power", EmissionShare = 1m, AbatementOptions = [new DraftAbatementOption { Code = "P1", Name = "Efficiency" }] }],
        Companies = [new DraftCompany { Name = "Delta Power", Sector = "Power", OwnerKind = "human", Capital = 1_000_000m, Units = [new DraftUnit { Name = "Delta Unit 1", BaselineEmissions = 100_000m }] }],
    };
}
