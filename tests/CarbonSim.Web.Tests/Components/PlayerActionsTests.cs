using Bunit;
using CarbonSim.Web.Components.Pages;
using FluentAssertions;

namespace CarbonSim.Web.Tests.Components;

/// <summary>
/// What the buttons do. Every action goes through the session, which is the registry's owner-checked
/// path, so these tests assert the call a component made rather than the engine it would have moved.
/// </summary>
public sealed class PlayerActionsTests : ScreenTestContext
{
    public PlayerActionsTests()
    {
        View = PlayerViewFixture.Sample(SimulationId);
    }

    [Fact]
    public void Implement_asks_the_session_to_implement_the_chosen_project()
    {
        IRenderedComponent<Abatement> page = RenderComponent<Abatement>(parameters => parameters
            .AddCascadingValue(Snapshot)
            .Add(component => component.SimulationId, SimulationId));

        page.FindAll("button").First(button => button.TextContent.Contains("Implement", StringComparison.Ordinal)).Click();

        Session.Actions.Should().ContainSingle().Which.Should().Be("implement:1:1");
    }

    [Fact]
    public void Place_bid_sends_the_bid_the_form_shows()
    {
        IRenderedComponent<Auction> page = RenderComponent<Auction>(parameters => parameters
            .AddCascadingValue(Snapshot)
            .Add(component => component.SimulationId, SimulationId));

        page.FindAll("button").First(button => button.TextContent.Contains("Place bid", StringComparison.Ordinal)).Click();

        Session.Actions.Should().ContainSingle().Which.Should().Be("bid:1:1:100:1000");
    }

    [Fact]
    public void Place_order_sends_the_order_the_form_shows()
    {
        IRenderedComponent<Exchange> page = RenderComponent<Exchange>(parameters => parameters
            .AddCascadingValue(Snapshot)
            .Add(component => component.SimulationId, SimulationId));

        page.FindAll("button").First(button => button.TextContent.Contains("Place order", StringComparison.Ordinal)).Click();

        Session.Actions.Should().ContainSingle().Which.Should().Be("order:1:Vintage 1:Buy:Limit:1000:100::AllowPartial");
    }

    [Fact]
    public void Cancel_order_names_the_order_and_its_unit()
    {
        IRenderedComponent<Exchange> page = RenderComponent<Exchange>(parameters => parameters
            .AddCascadingValue(Snapshot)
            .Add(component => component.SimulationId, SimulationId));

        page.FindAll("button").First(button => button.TextContent.Trim() == "Cancel").Click();

        Session.Actions.Should().ContainSingle().Which.Should().Be("cancel:1:11");
    }

    [Fact]
    public void Send_offer_sends_it_to_the_named_unit()
    {
        IRenderedComponent<Otc> page = RenderComponent<Otc>(parameters => parameters
            .AddCascadingValue(Snapshot)
            .Add(component => component.SimulationId, SimulationId));

        page.FindAll("button").First(button => button.TextContent.Contains("Send offer", StringComparison.Ordinal)).Click();

        Session.Actions.Should().ContainSingle().Which.Should().Be("otc-offer:1:99:Vintage 1:100:1000");
    }

    [Fact]
    public void Accepting_an_offer_answers_it_for_the_players_own_unit()
    {
        IRenderedComponent<Otc> page = RenderComponent<Otc>(parameters => parameters
            .AddCascadingValue(Snapshot)
            .Add(component => component.SimulationId, SimulationId));

        page.FindAll("button").First(button => button.TextContent.Trim() == "Accept").Click();

        Session.Actions.Should().ContainSingle().Which.Should().Be("otc-answer:1:7:True");
    }

    [Fact]
    public void Sending_a_message_posts_what_was_typed()
    {
        IRenderedComponent<Messages> page = RenderComponent<Messages>(parameters => parameters
            .AddCascadingValue(Snapshot)
            .Add(component => component.SimulationId, SimulationId));

        page.Find("textarea").Change("hello");
        page.FindAll("button").First(button => button.TextContent.Contains("Send", StringComparison.Ordinal)).Click();

        Session.Actions.Should().ContainSingle().Which.Should().Be("message:hello");
    }
}
