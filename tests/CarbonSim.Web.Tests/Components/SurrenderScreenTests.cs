using Bunit;
using CarbonSim.Web.Components.Pages;
using CarbonSim.Web.Player;
using FluentAssertions;

namespace CarbonSim.Web.Tests.Components;

/// <summary>
/// The Surrender and Banking screen reads the engine's surrender plan: what the company can
/// surrender this year, the position that leaves, and the penalty the year end would charge. Under
/// manual surrender it carries the form that sets the surrender.
/// </summary>
public sealed class SurrenderScreenTests : ScreenTestContext
{
    private static readonly SurrenderView Automatic = new(
        Manual: false,
        HasInstruction: false,
        Obligation: 4_000_000m,
        AllowancesAvailable: 3_000_000m,
        OffsetsAvailable: 400_000m,
        OffsetLimit: 400_000m,
        Allowances: 3_000_000m,
        Offsets: 400_000m,
        ResultingPosition: -600_000m,
        Shortfall: 600_000m,
        EstimatedPenalty: 180_000_000m,
        UnlimitedBanking: false);

    public SurrenderScreenTests()
    {
        View = PlayerViewFixture.Sample(SimulationId) with { Surrender = Automatic };
    }

    [Fact]
    public void The_screen_shows_what_is_available_and_the_penalty_the_year_end_would_charge()
    {
        IRenderedComponent<SurrenderAndBanking> page = Render();

        page.Find("#surrender-allowances-available").TextContent.Should().Contain("3,000,000");
        page.Find("#surrender-offsets-available").TextContent.Should().Contain("400,000");
        page.Find("#surrender-estimated-penalty").TextContent.Should().Contain("180,000,000");
        page.Markup.Should().Contain(Localizer(Services)["AutomaticSurrender"].Value);
        page.FindAll("#set-surrender").Should().BeEmpty("the year end surrenders for the company");
    }

    [Fact]
    public void Under_manual_surrender_the_form_sets_the_volumes_the_player_types()
    {
        View = View with { Surrender = Automatic with { Manual = true, Allowances = 0m, Offsets = 0m, Shortfall = 4_000_000m } };

        IRenderedComponent<SurrenderAndBanking> page = Render();

        page.Markup.Should().Contain(Localizer(Services)["ManualSurrenderNote"].Value);
        page.Find("#surrender-allowances").Change("2,500,000");
        page.Find("#surrender-offsets").Change("100,000");
        page.Find("#set-surrender").Click();

        Session.Actions.Should().ContainSingle().Which.Should().Be("surrender:2500000:100000");
    }

    [Theory]
    [InlineData(-600_000, "Short 600,000")]
    [InlineData(250_000, "Long 250,000")]
    [InlineData(0, "Long 0")]
    public void The_position_reads_short_or_long_the_way_every_other_screen_does(int resulting, string expected)
    {
        View = View with { Surrender = Automatic with { ResultingPosition = resulting } };

        Render().Find("#surrender-plan").TextContent.Should().Contain(expected);
    }

    [Fact]
    public void A_settled_year_shows_no_plan()
    {
        View = View with { Surrender = null };

        IRenderedComponent<SurrenderAndBanking> page = Render();

        page.FindAll("#surrender-estimated-penalty").Should().BeEmpty();
    }

    private IRenderedComponent<SurrenderAndBanking> Render() =>
        RenderComponent<SurrenderAndBanking>(parameters => parameters
            .AddCascadingValue(Snapshot)
            .Add(component => component.SimulationId, SimulationId));
}
