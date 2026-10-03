using Bunit;
using CarbonSim.Web.Components.Pages;
using FluentAssertions;

namespace CarbonSim.Web.Tests.Components;

/// <summary>
/// A player typing into a bid or an order is writing a number the way their culture writes it: a
/// comma between the whole part and the fraction in Vietnamese, a dot as the thousands separator.
/// The tests run the same form under both cultures, because the bug they guard against is the one
/// that only shows up when the screen is not English.
/// </summary>
public sealed class NumberEntryTests : ScreenTestContext
{
    public NumberEntryTests()
    {
        View = PlayerViewFixture.Sample(SimulationId);
    }

    [Fact]
    public void A_volume_written_the_Vietnamese_way_binds_as_a_thousand_tonnes()
    {
        using CultureScope culture = new("vi-VN");

        IRenderedComponent<Auction> page = RenderAuction();

        page.Find("#bid-volume").GetAttribute("type").Should().Be("text", "the browser's own number input only accepts a dot");
        page.Find("#bid-volume").Change("1.000");
        ClickButton(page, "PlaceBid");

        Session.Actions.Should().ContainSingle().Which.Should().Be("bid:1:1:100:1000");
    }

    [Fact]
    public void The_bid_is_added_up_in_the_readers_culture()
    {
        using CultureScope culture = new("vi-VN");

        IRenderedComponent<Auction> page = RenderAuction();

        page.Find("#bid-volume").Change("1.000");

        // 100 a tonne times a thousand tonnes, grouped the Vietnamese way.
        page.Find("#bid-capital-cost").TextContent.Should().Contain("100.000");
    }

    [Fact]
    public void A_price_with_a_Vietnamese_decimal_comma_keeps_its_fraction()
    {
        using CultureScope culture = new("vi-VN");

        IRenderedComponent<Exchange> page = RenderComponent<Exchange>(parameters => parameters
            .AddCascadingValue(Snapshot)
            .Add(component => component.SimulationId, SimulationId));

        page.Find("#order-price").Change("150,5");
        ClickButton(page, "PlaceOrder");

        Session.Actions.Should().ContainSingle().Which.Should().Be("order:1:Vintage 1:Buy:Limit:1000:150.5::AllowPartial");
    }

    [Fact]
    public void The_same_volume_in_English_is_still_a_thousand_tonnes()
    {
        using CultureScope culture = new("en-US");

        IRenderedComponent<Auction> page = RenderAuction();

        page.Find("#bid-volume").Change("1,000");
        ClickButton(page, "PlaceBid");

        Session.Actions.Should().ContainSingle().Which.Should().Be("bid:1:1:100:1000");
    }

    [Fact]
    public void An_offer_written_the_Vietnamese_way_reaches_the_session()
    {
        using CultureScope culture = new("vi-VN");

        IRenderedComponent<Otc> page = RenderComponent<Otc>(parameters => parameters
            .AddCascadingValue(Snapshot)
            .Add(component => component.SimulationId, SimulationId));

        page.Find("#offer-volume").Change("1.500");
        page.Find("#offer-price").Change("120,25");
        ClickButton(page, "SendOffer");

        Session.Actions.Should().ContainSingle().Which.Should().Be("otc-offer:1:99:Vintage 1:120.25:1500");
    }

    private IRenderedComponent<Auction> RenderAuction() =>
        RenderComponent<Auction>(parameters => parameters
            .AddCascadingValue(Snapshot)
            .Add(component => component.SimulationId, SimulationId));

    /// <summary>Finds the button by the label the screens actually show in this test's culture.</summary>
    private void ClickButton<TComponent>(IRenderedComponent<TComponent> page, string key)
        where TComponent : Microsoft.AspNetCore.Components.IComponent
    {
        string label = Localizer(Services)[key].Value;

        page.FindAll("button").First(button => button.TextContent.Contains(label, StringComparison.Ordinal)).Click();
    }
}
