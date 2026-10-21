using Bunit;
using CarbonSim.Web;
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
    public void The_run_screen_shows_the_two_market_settings_a_trainer_can_set()
    {
        IRenderedComponent<AdminRun> page = Render();

        page.Markup.Should().Contain(
            Localizer(Services)["GovernmentReserveToAuctionPercent"].Value,
            "the reserve re-offer share is a run setting the console has to show");
        page.Markup.Should().Contain(
            Localizer(Services)["ForwardVintageShare"].Value,
            "so is the forward vintage share the host plays the run with");
    }

    [Fact]
    public void The_runs_screen_lists_the_live_and_saved_exercises()
    {
        IRenderedComponent<AdminRuns> page = RenderComponent<AdminRuns>(parameters => parameters.AddCascadingValue(Snapshot));

        page.Markup.Should().Contain("Vietnam pilot");
        page.FindAll("button").Should().NotContain(
            button => button.TextContent.Contains("Load", StringComparison.Ordinal),
            "loading the saved copy over a live run would throw away what was played since");
    }

    [Fact]
    public void A_saved_exercise_that_is_not_running_can_be_loaded()
    {
        Snapshot = Snapshot with { Saved = [new AdminSavedRun(Guid.NewGuid(), "Last week's pilot", "YearEnded", 2, false)] };

        IRenderedComponent<AdminRuns> page = RenderComponent<AdminRuns>(parameters => parameters.AddCascadingValue(Snapshot));

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
    public void The_run_screen_names_a_unit_once_and_marks_the_ones_the_bots_run()
    {
        // A scenario usually names a one-unit company after the company itself, so "name (company)"
        // would read "Delta Power (Delta Power)"; two companies of the same name are not possible.
        AdminRunDetail detail = AdminViewFixture.Detail(SimulationId, "Running") with
        {
            Units =
            [
                new AdminUnitView(1, "Delta Power", "Delta Power", "Power", 100_000m, 90_000m, 100_000m, 5_000m, true, false, false, 0, 1, []),
                new AdminUnitView(2, "Red River Unit 1", "Red River Power", "Power", 100_000m, 90_000m, 100_000m, 0m, false, false, false, 0, 2, []),
            ],
        };

        Snapshot = AdminViewFixture.Sample(SimulationId) with { Run = detail };

        IRenderedComponent<AdminRun> page = Render();
        string[] choices = [.. page.FindAll("#fine-unit option").Select(option => option.TextContent.Trim())];

        choices.Should().Equal("Delta Power (AI)", "Red River Power - Red River Unit 1");
    }

    [Fact]
    public void The_run_screen_does_not_offer_to_resume_a_year_whose_time_is_up()
    {
        Snapshot = AdminViewFixture.Sample(SimulationId, state: "TradingHalted", detailState: "TradingHalted") with
        {
            Run = AdminViewFixture.Detail(SimulationId, "TradingHalted") with { TimeLeftInYear = TimeSpan.Zero },
        };

        IRenderedComponent<AdminRun> page = Render();

        page.FindAll("#admin-resume").Should().BeEmpty("there is no time left to resume into");
        page.Find("#admin-end-year").Should().NotBeNull("the way on is to close the year");
        page.Markup.Should().Contain(Localizer(Services)["AdminYearOver"].Value);
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
    public void The_reports_say_which_period_each_cost_and_volume_covers()
    {
        // The company report's costs are the year in play while the leaderboard beside it is the whole
        // run, and the price report's volume counts every channel: each header says so.
        IRenderedComponent<AdminReports> page = RenderComponent<AdminReports>(parameters => parameters
            .AddCascadingValue(Snapshot)
            .Add(component => component.SimulationId, SimulationId));
        IReadOnlyList<string> headers = [.. page.FindAll("th").Select(header => header.TextContent.Trim())];

        headers.Should().Contain("Cost of compliance (this year)");
        headers.Should().Contain("Marginal cost of compliance (this year)");
        headers.Should().Contain("Allowance volume (all channels)");
        headers.Should().NotContain(header => header.Contains("Allowance auction", StringComparison.Ordinal));
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
    public void The_setup_screen_offers_the_government_reserve_reoffer_share()
    {
        Session.Draft = Draft();
        Session.Draft.Parameters.GovernmentReserveToAuctionPercent = 0.5m;

        IRenderedComponent<AdminSetup> page = RenderComponent<AdminSetup>();

        AngleSharp.Dom.IElement input = page.Find("#setup-reserve-to-auction");
        string label = Localizer(Services)["GovernmentReserveToAuctionPercent"].Value;

        page.FindAll("label").Should().Contain(candidate =>
            candidate.GetAttribute("for") == "setup-reserve-to-auction" && candidate.TextContent.Trim() == label);
        input.GetAttribute("value").Should().Be("50%", "a share the console shows as a percentage is typed as one");
    }

    [Fact]
    public void Every_setup_input_has_an_id_a_label_points_at()
    {
        Session.Draft = Draft();

        IRenderedComponent<AdminSetup> page = RenderComponent<AdminSetup>();
        string[] ids = [.. page.FindAll("input").Select(input => input.GetAttribute("id") ?? string.Empty)];
        string[] labelled = [.. page.FindAll("label").Select(label => label.GetAttribute("for") ?? string.Empty)];

        ids.Should().NotBeEmpty();
        ids.Should().OnlyContain(id => !string.IsNullOrWhiteSpace(id), "an input with no id cannot be labelled");
        ids.Should().OnlyHaveUniqueItems("two inputs sharing an id would both read the one label");

        foreach (string id in ids)
        {
            labelled.Should().Contain(id, $"a <label for=\"{id}\"> has to name the input");
        }
    }

    [Fact]
    public void The_setup_screen_writes_the_shares_as_percentages_and_reads_a_bare_number_back_as_one()
    {
        Session.Draft = Draft();
        Session.Draft.Parameters.AnnualCapReductionRate = 0.03m;
        Session.Draft.Parameters.FreeAllocationShare = 0.9m;
        Session.Draft.Parameters.OffsetUsageLimit = 0.1m;
        Session.Draft.Parameters.VolatilityBand = 0.1m;
        Session.Draft.Parameters.OverdraftInterestRate = 0.07m;
        Session.Draft.Parameters.BankingLimit = 0.2m;
        Session.Draft.Parameters.GovernmentReserveToAuctionPercent = 0.5m;

        IRenderedComponent<AdminSetup> page = RenderComponent<AdminSetup>();

        page.Find("#setup-reduction").GetAttribute("value").Should().Be("3%");
        page.Find("#setup-free").GetAttribute("value").Should().Be("90%");
        page.Find("#setup-offset-limit").GetAttribute("value").Should().Be("10%");
        page.Find("#setup-volatility").GetAttribute("value").Should().Be("10%");
        page.Find("#setup-interest").GetAttribute("value").Should().Be("7%");
        page.Find("#setup-banking-limit").GetAttribute("value").Should().Be("20%");
        page.Find("#setup-reserve-to-auction").GetAttribute("value").Should().Be("50%");

        // The trainer types the number the console showed them, with or without the sign.
        page.Find("#setup-reduction").Change("3");
        Session.Draft.Parameters.AnnualCapReductionRate.Should().Be(0.03m);
        page.Find("#setup-free").Change("90%");
        Session.Draft.Parameters.FreeAllocationShare.Should().Be(0.9m);
    }

    [Fact]
    public void The_console_says_which_build_it_is()
    {
        IRenderedComponent<AdminLayout> layout = RenderComponent<AdminLayout>(parameters =>
            parameters.Add(component => component.Body, builder => { }));

        // A tester's report is tied to a build by the stamp they copy out of the page footer.
        layout.Find("footer.stamp").TextContent.Should().Contain(BuildInfo.Version);
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
        layout.FindAll("a").Should().Contain(
            link => link.GetAttribute("href") == "/sign-in",
            "a refusal to a signed-out visitor has to offer the way in");
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
        Parameters = new DraftParameters
        {
            Cap = 1_000_000m,
            Years = 3,
            AuctionsPerYear = 4,
            BausGrowthBySector = [new DraftGrowth { Sector = "Power", MinAnnualRate = 0.02m, MaxAnnualRate = 0.06m }],
        },
        Sectors = [new DraftSector { Name = "Power", EmissionShare = 1m, AbatementOptions = [new DraftAbatementOption { Code = "P1", Name = "Efficiency" }] }],
        Companies = [new DraftCompany { Name = "Delta Power", Sector = "Power", OwnerKind = "human", Capital = 1_000_000m, Units = [new DraftUnit { Name = "Delta Unit 1", BaselineEmissions = 100_000m }] }],
    };
}
