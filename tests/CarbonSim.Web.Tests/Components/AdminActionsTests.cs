using Bunit;
using CarbonSim.Web.Admin;
using CarbonSim.Web.Components.Admin;
using FluentAssertions;

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
        page.Find("#offset-company").Change("1");
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

    private IRenderedComponent<AdminRun> Render() => RenderComponent<AdminRun>(parameters => parameters
        .AddCascadingValue(Snapshot)
        .Add(component => component.SimulationId, SimulationId));
}
