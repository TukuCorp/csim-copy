using Bunit;
using CarbonSim.Web.Components.Pages;
using CarbonSim.Web.Player;
using FluentAssertions;

namespace CarbonSim.Web.Tests.Components;

/// <summary>
/// The opportunities table reads over the simulation by default and over the project's lifetime on
/// request (FAQ #3-#4). An option that cannot operate before the run ends shows no cost per tonne.
/// </summary>
public sealed class AbatementHorizonTests : ScreenTestContext
{
    public AbatementHorizonTests()
    {
        PlayerView sample = PlayerViewFixture.Sample(SimulationId);
        UnitView unit = sample.Units[0];

        View = sample with
        {
            Units =
            [
                unit with
                {
                    Opportunities =
                    [
                        unit.Opportunities[0] with { SimulationTonnes = 107_110m, SimulationCostPerTonne = 15.03m, SimulationRoi = -0.87m },
                        unit.Opportunities[1] with { SimulationTonnes = 0m, SimulationCostPerTonne = null, SimulationRoi = -1m },
                    ],
                },
            ],
        };
    }

    [Fact]
    public void The_table_reads_over_the_simulation_by_default()
    {
        IRenderedComponent<Abatement> page = Render();
        List<string> costs = Column(page, "abatement-cost");

        costs.Should().Equal("15.03", "-");
        Column(page, "abatement-roi").Should().Equal("-87.0%", "-100.0%");
        Column(page, "abatement-simulation-tonnes").Should().Equal("107,110", "0");
        page.Markup.Should().Contain(Localizer(Services)["NOTE_OnceAnEmmissionReductionHasBeenImplemented"].Value);
    }

    [Fact]
    public void Switching_to_the_project_lifetime_shows_the_lifetime_figures()
    {
        IRenderedComponent<Abatement> page = Render();

        page.Find("#abatement-view-lifetime").Click();

        Column(page, "abatement-cost").Should().Equal("20.00", "7.00");
        Column(page, "abatement-roi").Should().Equal("-40.0%", "40.0%");
        page.FindAll("span.abatement-simulation-tonnes").Should().BeEmpty();
    }

    private IRenderedComponent<Abatement> Render() =>
        RenderComponent<Abatement>(parameters => parameters
            .AddCascadingValue(Snapshot)
            .Add(component => component.SimulationId, SimulationId));

    private static List<string> Column(IRenderedComponent<Abatement> page, string marker) =>
        [.. page.FindAll($"span.{marker}").Select(cell => cell.TextContent.Trim())];
}
