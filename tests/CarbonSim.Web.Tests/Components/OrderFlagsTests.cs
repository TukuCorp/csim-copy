using Bunit;
using CarbonSim.Web.Components.Pages;
using FluentAssertions;

namespace CarbonSim.Web.Tests.Components;

/// <summary>
/// The order form carries the original's two flags apart (deck s16; catalog `PartialFillsAccepted`,
/// `ImmediateOrCancel`): partial fills accepted and resting by default, immediate-or-cancel to take
/// what crosses and drop the rest, and no partial fills at all to fill in full or not at all.
/// </summary>
public sealed class OrderFlagsTests : ScreenTestContext
{
    public OrderFlagsTests()
    {
        View = PlayerViewFixture.Sample(SimulationId);
    }

    [Fact]
    public void By_default_an_order_accepts_partial_fills_and_rests()
    {
        IRenderedComponent<Exchange> page = Render();

        page.Find("#order-partial-fill").HasAttribute("checked").Should().BeTrue();
        page.Find("#order-ioc").HasAttribute("checked").Should().BeFalse();
        ClickButton(page, "PlaceOrder");

        Session.Actions.Should().ContainSingle().Which.Should().EndWith(":AllowPartial");
    }

    [Fact]
    public void Immediate_or_cancel_with_partial_fills_sends_immediate_or_cancel()
    {
        IRenderedComponent<Exchange> page = Render();

        page.Find("#order-ioc").Change(true);
        ClickButton(page, "PlaceOrder");

        Session.Actions.Should().ContainSingle().Which.Should().EndWith(":ImmediateOrCancel");
    }

    [Fact]
    public void Without_partial_fills_the_order_is_fill_or_kill()
    {
        IRenderedComponent<Exchange> page = Render();

        page.Find("#order-partial-fill").Change(false);
        ClickButton(page, "PlaceOrder");

        Session.Actions.Should().ContainSingle().Which.Should().EndWith(":FillOrKill");
    }

    private void ClickButton(IRenderedComponent<Exchange> page, string key)
    {
        string label = Localizer(Services)[key].Value;

        page.FindAll("button").First(button => button.TextContent.Contains(label, StringComparison.Ordinal)).Click();
    }

    private IRenderedComponent<Exchange> Render() =>
        RenderComponent<Exchange>(parameters => parameters
            .AddCascadingValue(Snapshot)
            .Add(component => component.SimulationId, SimulationId));
}
