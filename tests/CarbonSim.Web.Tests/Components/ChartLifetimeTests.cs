using Bunit;
using CarbonSim.Web.Components.Shared;
using FluentAssertions;

namespace CarbonSim.Web.Tests.Components;

/// <summary>
/// A chart lives as long as its page. The shell re-renders every second, so a chart that redrew on
/// every render would replay its animation each tick, and one never disposed would leave its
/// instance and a window resize handler behind on every page visit of a long session.
/// </summary>
public sealed class ChartLifetimeTests : ScreenTestContext
{
    [Fact]
    public void A_chart_draws_again_only_when_its_option_changes()
    {
        IRenderedComponent<EChart> chart = RenderComponent<EChart>(parameters => parameters
            .Add(component => component.Option, "{\"series\":[1]}")
            .Add(component => component.Label, "A chart"));

        chart.SetParametersAndRender(parameters => parameters.Add(component => component.Option, "{\"series\":[1]}"));
        Charts.Options.Should().ContainSingle("the same option was drawn already");

        chart.SetParametersAndRender(parameters => parameters.Add(component => component.Option, "{\"series\":[2]}"));
        Charts.Options.Should().HaveCount(2);
    }

    [Fact]
    public void A_chart_is_taken_down_with_its_page()
    {
        IRenderedComponent<EChart> chart = RenderComponent<EChart>(parameters => parameters
            .Add(component => component.Option, "{}")
            .Add(component => component.Label, "A chart"));

        DisposeComponents();

        Charts.Disposed.Should().Be(1);
    }
}
