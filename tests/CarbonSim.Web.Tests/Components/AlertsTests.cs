using Bunit;
using CarbonSim.Web.Components.Layout;
using CarbonSim.Web.Components.Shared;
using CarbonSim.Web.Player;
using FluentAssertions;

namespace CarbonSim.Web.Tests.Components;

/// <summary>
/// The original raises an alert over whatever screen is open when an auction ends or an offer
/// arrives, so a player on the exchange page still learns what the auction did. These pin that the
/// alert comes once, for something new, and goes when dismissed.
/// </summary>
public sealed class AlertsTests : ScreenTestContext
{
    public AlertsTests()
    {
        View = PlayerViewFixture.Sample(SimulationId);
    }

    [Fact]
    public void An_auction_that_clears_while_the_player_is_elsewhere_raises_an_alert_with_the_result()
    {
        IRenderedComponent<Alerts> alerts = RenderComponent<Alerts>(parameters => parameters.Add(component => component.View, View));

        alerts.FindAll(".alert").Should().BeEmpty("what had cleared before the screen opened is not news");

        AuctionView second = View.Auctions[0] with { Sequence = 2, MyBids = [new MyBidView(1, 120m, 30_000m, 30_000m, 3_300_000m)] };
        PlayerView later = View with { Auctions = [View.Auctions[0], second] };
        alerts.SetParametersAndRender(parameters => parameters.Add(component => component.View, later));

        string text = alerts.Find(".alert").TextContent;
        text.Should().Contain("30,000").And.Contain(Display.Money(3_300_000m));

        alerts.Find(".alert button").Click();
        alerts.FindAll(".alert").Should().BeEmpty("a dismissed alert stays dismissed");
    }

    [Fact]
    public void A_new_offer_raises_an_alert_that_links_to_the_otc_market()
    {
        PlayerView before = View with { OffersToAnswer = [] };
        IRenderedComponent<Alerts> alerts = RenderComponent<Alerts>(parameters => parameters.Add(component => component.View, before));

        OtcOfferView offer = new(41, "Song Xanh", "Phu My 1", "AI Power 1", "Red River Power", ProductView.Allowance(1), 100m, 1_000m, 100_000m, "Pending", true, false);
        alerts.SetParametersAndRender(parameters => parameters.Add(component => component.View, before with { OffersToAnswer = [offer] }));

        alerts.Find(".alert a").GetAttribute("href").Should().EndWith("/otc");
    }
}
