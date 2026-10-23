using Bunit;
using CarbonSim.Engine.Domain;
using CarbonSim.Engine.Scenarios;
using CarbonSim.Web.Admin;
using CarbonSim.Web.Components.Admin;
using CarbonSim.Web.Resources;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;

namespace CarbonSim.Web.Tests.Components;

/// <summary>
/// What the console's controls do. Each control goes through the session, which is the
/// server-side driver's front door, so a test asserts the call a screen made rather than the
/// engine it would have moved.
/// </summary>
public sealed class AdminActionsTests : AdminScreenTestContext
{
    public AdminActionsTests()
    {
        Snapshot = AdminViewFixture.Sample(SimulationId);
    }

    [Fact]
    public void Starting_a_pending_year_asks_the_session_to_begin_it()
    {
        Snapshot = AdminViewFixture.Sample(SimulationId, state: "Pending", detailState: "Pending");

        Render().Find("#admin-begin-year").Click();

        Session.Actions.Should().ContainSingle().Which.Should().Be("begin");
    }

    [Fact]
    public void Pausing_the_clock_asks_the_session_to_pause_it()
    {
        Render().Find("#admin-pause").Click();

        Session.Actions.Should().ContainSingle().Which.Should().Be("pause");
    }

    [Fact]
    public void Halting_trading_sends_the_warning_the_form_shows()
    {
        IRenderedComponent<AdminRun> page = Render();
        page.Find("#admin-halt-warn").Change("30");
        page.Find("#admin-halt").Click();

        Session.Actions.Should().ContainSingle().Which.Should().Be("halt:30");
    }

    [Fact]
    public void Hiding_messaging_asks_the_session_to_switch_it_off()
    {
        Render().Find("#admin-messaging-toggle").Click();

        Session.Actions.Should().ContainSingle().Which.Should().Be("messaging:False");
    }

    [Fact]
    public void Fining_a_unit_sends_the_unit_amount_and_reason()
    {
        IRenderedComponent<AdminRun> page = Render();
        page.Find("#fine-unit").Change("1");
        page.Find("#fine-amount").Change("5000");
        page.Find("#fine-description").Change("late report");
        page.FindAll("button").First(button => button.TextContent.Contains("Fine a unit", StringComparison.Ordinal)).Click();

        Session.Actions.Should().ContainSingle().Which.Should().Be("fine:1:5000:late report");
    }

    [Fact]
    public void Disbursing_offsets_sends_the_company_and_volume()
    {
        IRenderedComponent<AdminRun> page = Render();
        page.Find("#offset-unit").Change("1");
        page.Find("#offset-volume").Change("1500");
        page.FindAll("button").First(button => button.TextContent.Contains("Hand out offsets", StringComparison.Ordinal)).Click();

        Session.Actions.Should().ContainSingle().Which.Should().Be("offsets:1:1500");
    }

    [Fact]
    public void Adding_abatement_sends_the_unit_and_project()
    {
        IRenderedComponent<AdminRun> page = Render();
        page.Find("#abatement-unit").Change("1");
        page.Find("#abatement-option").Change("0");
        page.FindAll("button").First(button => button.TextContent.Contains("Add abatement for a unit", StringComparison.Ordinal)).Click();

        Session.Actions.Should().ContainSingle().Which.Should().Be("abatement:1:0");
    }

    [Fact]
    public void An_end_of_year_modification_sends_the_year_and_the_change()
    {
        IRenderedComponent<AdminRun> page = Render();
        page.Find("#shock-unit").Change("1");
        page.Find("#shock-year").Change("1");
        page.Find("#shock-tonnes").Change("40000");
        page.FindAll("button").First(button => button.TextContent.Trim() == "Apply").Click();

        Session.Actions.Should().ContainSingle().Which.Should().Be("shock:1:1:40000");
    }

    [Fact]
    public void Turning_a_units_auto_trade_on_asks_the_session_for_it()
    {
        IRenderedComponent<AdminRun> page = Render();
        page.FindAll("button").First(button => button.TextContent.Trim() == "On").Click();

        Session.Actions.Should().ContainSingle().Which.Should().Be("autotrade:1:True");
    }

    [Fact]
    public void Saving_the_registration_settings_sends_them()
    {
        Session.Draft = new ScenarioDraft { Name = "Draft", Sectors = [], Companies = [] };

        IRenderedComponent<AdminSetup> page = RenderComponent<AdminSetup>();
        page.Find("#admin-save-registration").Click();

        Session.Actions.Should().ContainSingle().Which.Should().Be("registration:True:pin-123");
    }

    [Fact]
    public void The_forms_open_on_a_choice_and_their_labels_name_their_controls()
    {
        IRenderedComponent<AdminRun> page = Render();

        page.Find("#fine-unit").GetAttribute("value").Should().Be("1", "the fine form opens on the first unit");
        page.Find("#offset-unit").GetAttribute("value").Should().Be("1", "the offset form opens on the first unit");
        page.Find("label[for='fine-unit']").Should().NotBeNull("a label has to name the control it belongs to");
        page.Find("label[for='offset-unit']").Should().NotBeNull();
    }

    [Fact]
    public void A_fine_without_a_description_is_refused_where_the_form_is()
    {
        IRenderedComponent<AdminRun> page = Render();
        page.Find("#fine-description").Change(string.Empty);
        page.FindAll("button").First(button => button.TextContent.Contains("Fine a unit", StringComparison.Ordinal)).Click();

        page.Markup.Should().Contain(Localizer(Services)["AdminFineNeedsDescription"].Value);
        Session.Actions.Should().BeEmpty("a form that is not filled in is not sent");
    }

    [Fact]
    public void A_fine_that_goes_through_says_what_it_did_beside_the_button()
    {
        IRenderedComponent<AdminRun> page = Render();
        page.Find("#fine-amount").Change("5000");
        page.Find("#fine-description").Change("late report");
        page.FindAll("button").First(button => button.TextContent.Contains("Fine a unit", StringComparison.Ordinal)).Click();

        page.Find(".notice-status").TextContent.Should().Contain("Delta Unit 1").And.Contain("5,000");
        Session.Actions.Should().ContainSingle().Which.Should().Be("fine:1:5000:late report");
    }

    [Fact]
    public void The_messaging_control_reads_out_the_state_and_what_the_button_will_do()
    {
        IRenderedComponent<AdminRun> page = Render();

        page.Find("#admin-messaging-state").TextContent.Trim().Should().Be(Localizer(Services)["AdminOn"].Value);
        page.Find("#admin-messaging-toggle").TextContent.Should().Contain(Localizer(Services)["AdminSwitchOff"].Value);

        Snapshot = AdminViewFixture.Sample(SimulationId) with
        {
            Run = AdminViewFixture.Detail(SimulationId, "Running") with { MessagingEnabled = false },
        };

        IRenderedComponent<AdminRun> off = Render();

        off.Find("#admin-messaging-state").TextContent.Trim().Should().Be(Localizer(Services)["AdminOff"].Value);
        off.Find("#admin-messaging-toggle").TextContent.Should().Contain(Localizer(Services)["AdminSwitchOn"].Value);
    }

    [Fact]
    public void The_setup_screen_sets_the_exercise_ai_difficulty()
    {
        Session.Draft = new ScenarioDraft { Name = "Draft", Sectors = [], Companies = [] };

        IRenderedComponent<AdminSetup> page = RenderComponent<AdminSetup>();
        page.Find("#setup-ai-difficulty").GetAttribute("value").Should().Be("Normal", "a scenario that never names it plays at Normal");
        page.Find("#setup-ai-difficulty").Change("Hard");

        Session.Draft.Parameters.AiDifficulty.Should().Be("Hard");
    }

    [Fact]
    public void The_setup_screen_sets_manual_surrender_and_unlimited_banking_and_the_scenario_carries_them()
    {
        Session.Draft = ScenarioDraft.Parse(File.ReadAllText(Path.Combine("scenarios", "vietnam-2024.json")));

        IRenderedComponent<AdminSetup> page = RenderComponent<AdminSetup>();
        page.Find("#setup-automatic-surrender").HasAttribute("checked").Should().BeTrue("a scenario that never names it surrenders automatically");
        page.Find("#setup-unlimited-banking").HasAttribute("checked").Should().BeFalse();
        page.Find("#setup-automatic-surrender").Change(false);
        page.Find("#setup-unlimited-banking").Change(true);

        Session.Draft.Parameters.AutomaticSurrender.Should().BeFalse();
        Session.Draft.Parameters.UnlimitedBanking.Should().BeTrue();

        Parameters rules = ScenarioLoader.LoadJson(Session.Draft.ToJson(), "setup-surrender").Rules;

        rules.AutomaticSurrender.Should().BeFalse();
        rules.UnlimitedBanking.Should().BeTrue();
    }

    [Fact]
    public void The_setup_screen_sets_a_sectors_own_free_share_and_the_auction_auto_resume()
    {
        Session.Draft = ScenarioDraft.Parse(File.ReadAllText(Path.Combine("scenarios", "vietnam-2024.json")));

        IRenderedComponent<AdminSetup> page = RenderComponent<AdminSetup>();
        page.Find("#setup-sector-0-free-share").GetAttribute("value").Should().BeEmpty("a sector without its own share takes the system's");
        page.Find("#setup-sector-0-free-share").Change("50");
        page.Find("#setup-auto-resume").Change("30");

        Simulation simulation = ScenarioLoader.LoadJson(Session.Draft.ToJson(), "setup-sector-share");

        simulation.Sectors[0].FreeAllocationShare.Should().Be(0.5m);
        simulation.Sectors[1].FreeAllocationShare.Should().BeNull();
        simulation.Rules.AutoResumeAfterAuction.Should().Be(TimeSpan.FromSeconds(30));

        page.Find("#setup-sector-0-free-share").Change(string.Empty);
        Session.Draft.Sectors[0].FreeAllocationShare.Should().BeNull("clearing the box hands the sector back to the system's share");
    }

    [Fact]
    public void The_setup_screen_edits_each_companys_name_capital_and_credit_limit()
    {
        Session.Draft = new ScenarioDraft
        {
            Name = "Draft",
            Sectors = [],
            Companies =
            [
                new DraftCompany { Name = "Delta Power", Capital = 1_000_000m, OverdraftLimit = 100_000m },
                new DraftCompany { Name = "Red River Power", Capital = 2_000_000m, OverdraftLimit = 200_000m },
            ],
        };

        IRenderedComponent<AdminSetup> page = RenderComponent<AdminSetup>();
        page.Find("#setup-company-1-name").Change("Song Da Power");
        page.Find("#setup-company-1-capital").Change("3,000,000");
        page.Find("#setup-company-1-credit").Change("300,000");

        DraftCompany second = Session.Draft.Companies[1];
        second.Name.Should().Be("Song Da Power");
        second.Capital.Should().Be(3_000_000m);
        second.OverdraftLimit.Should().Be(300_000m);
        Session.Draft.Companies[0].Name.Should().Be("Delta Power", "only the row that was edited changes");
    }

    [Fact]
    public void The_setup_screen_gives_every_company_the_same_capital_and_credit_limit_at_once()
    {
        Session.Draft = new ScenarioDraft
        {
            Name = "Draft",
            Sectors = [],
            Companies =
            [
                new DraftCompany { Name = "Delta Power", Capital = 1_000_000m, OverdraftLimit = 100_000m },
                new DraftCompany { Name = "Red River Power", Capital = 2_000_000m, OverdraftLimit = 200_000m },
            ],
        };

        IRenderedComponent<AdminSetup> page = RenderComponent<AdminSetup>();
        page.Find("#setup-all-capital").Change("5,000,000");
        page.Find("#setup-all-credit").Change("500,000");
        page.Find("#setup-apply-to-all").Click();

        Session.Draft.Companies.Should().OnlyContain(company => company.Capital == 5_000_000m && company.OverdraftLimit == 500_000m);
    }

    [Fact]
    public void An_announcement_goes_to_the_room_or_to_the_company_the_trainer_picks()
    {
        IRenderedComponent<AdminRun> page = Render();
        page.Find("#announce-text").Change("Auction 2 opens in five minutes");
        page.Find("#admin-announce").Click();

        page.Find("#announce-to").Change("Red River Power");
        page.Find("#announce-text").Change("Your bid was late");
        page.Find("#admin-announce").Click();

        Session.Actions.Should().Equal(
            "announce::Auction 2 opens in five minutes",
            "announce:Red River Power:Your bid was late");
    }

    [Fact]
    public void The_market_switches_say_their_state_and_flip_one_market_at_a_time()
    {
        Snapshot = AdminViewFixture.Sample(SimulationId) with
        {
            Run = AdminViewFixture.Detail(SimulationId, "Running") with { OtcAccess = false },
        };

        IRenderedComponent<AdminRun> page = Render();

        page.Find("#admin-exchange-state").TextContent.Trim().Should().Be(Localizer(Services)["AdminOn"].Value);
        page.Find("#admin-otc-state").TextContent.Trim().Should().Be(Localizer(Services)["AdminOff"].Value);

        page.Find("#admin-exchange-toggle").Click();
        page.Find("#admin-otc-toggle").Click();
        page.Find("#admin-leaderboard-toggle").Click();

        Session.Actions.Should().Equal("access:False::", "access::True:", "access:::False");
    }

    [Fact]
    public void The_end_of_year_modifications_send_the_offset_limit_banking_limit_and_penalty()
    {
        IRenderedComponent<AdminRun> page = Render();

        page.Find("#compliance-offset-limit").GetAttribute("value").Should().Be("10", "the form opens on the rules in force, in per cent");
        page.Find("#compliance-offset-limit").Change("5");
        page.Find("#compliance-banking-limit").Change("50");
        page.Find("#compliance-penalty").Change("450");
        page.Find("#admin-change-compliance").Click();

        Session.Actions.Should().ContainSingle().Which.Should().Be("compliance:0.05:0.5:450");
    }

    [Fact]
    public void A_pause_that_will_resume_itself_says_when_and_can_be_called_off()
    {
        Snapshot = AdminViewFixture.Sample(SimulationId) with
        {
            Run = AdminViewFixture.Detail(SimulationId, "PausedAfterAuction") with { TimeToAutoResume = TimeSpan.FromSeconds(20) },
        };

        IRenderedComponent<AdminRun> page = Render();

        page.Find("#admin-auto-resume").TextContent.Should().Contain("20");
        page.Find("#admin-cancel-auto-resume").Click();

        Session.Actions.Should().ContainSingle().Which.Should().Be("cancel-auto-resume");
    }

    [Fact]
    public void The_run_header_shows_the_virtual_date()
    {
        AdminRunDetail detail = AdminViewFixture.Detail(SimulationId, "Running");
        string expected = CarbonSim.Web.Components.Shared.Display.VirtualDate(
            detail.Parameters.YearLength - detail.TimeLeftInYear, detail.Parameters.YearLength);

        Render().Find("#admin-virtual-date").TextContent.Trim().Should().Be(expected);
    }

    private static IStringLocalizer<SharedStrings> Localizer(IServiceProvider services) =>
        services.GetRequiredService<IStringLocalizer<SharedStrings>>();

    private IRenderedComponent<AdminRun> Render() => RenderComponent<AdminRun>(parameters => parameters
        .AddCascadingValue(Snapshot)
        .Add(component => component.SimulationId, SimulationId));
}
