using Bunit;
using CarbonSim.Web.Components.Pages;
using CarbonSim.Web.Components.Shared;
using FluentAssertions;

namespace CarbonSim.Web.Tests.Components;

/// <summary>
/// A room shown in dong has to read and type every price in dong. The engine keeps its internal
/// unit, so a price a player types is converted back before it reaches the session, and every price
/// on the screen carries its currency code. At 25,000 dong to the unit, 2,600,000 dong is 104.
/// </summary>
public sealed class DongPriceEntryTests : ScreenTestContext
{
    private readonly IDisposable _currency = Display.UseCurrency(new CurrencyDisplay(CurrencyDisplay.Vnd, 25_000m));
    private readonly CultureScope _culture = new("vi-VN");

    public DongPriceEntryTests()
    {
        View = PlayerViewFixture.Sample(SimulationId);
    }

    [Fact]
    public void An_order_price_typed_in_dong_reaches_the_session_in_the_internal_unit()
    {
        IRenderedComponent<Exchange> page = Render<Exchange>();

        page.Find("#order-price").Change("2.600.000");
        ClickButton(page, "PlaceOrder");

        Session.Actions.Should().ContainSingle().Which.Should().Be("order:1:Vintage 1:Buy:Limit:1000:104::AllowPartial");
    }

    [Fact]
    public void An_offer_price_typed_in_dong_reaches_the_session_in_the_internal_unit()
    {
        IRenderedComponent<Otc> page = Render<Otc>();

        page.Find("#offer-volume").Change("1.500");
        page.Find("#offer-price").Change("3.000.000");
        ClickButton(page, "SendOffer");

        Session.Actions.Should().ContainSingle().Which.Should().Be("otc-offer:1:99:Vintage 1:120:1500");
    }

    [Fact]
    public void The_auction_slider_runs_in_dong_and_bids_in_the_internal_unit()
    {
        IRenderedComponent<Auction> page = Render<Auction>();

        page.Find("#bid-price").GetAttribute("min").Should().Be("2500000");
        page.Find("#bid-price").Change("2600000");
        page.Find("#bid-volume").Change("1.000");
        ClickButton(page, "PlaceBid");

        Session.Actions.Should().ContainSingle().Which.Should().Be("bid:1:1:104:1000");
    }

    [Fact]
    public void Price_boxes_name_the_currency_they_expect()
    {
        Render<Exchange>().Find("#order-price").ParentElement!.TextContent.Should().Contain("VND");
        Render<Otc>().Find("#offer-price").ParentElement!.TextContent.Should().Contain("VND");
    }

    [Fact]
    public void Market_prices_are_shown_in_dong_with_their_code()
    {
        string markup = Render<Exchange>().Markup;

        // The fixture's auction prices run 100 to 120 in the internal unit.
        markup.Should().Contain("2.500.000 VND").And.Contain("3.000.000 VND");
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _culture.Dispose();
            _currency.Dispose();
        }

        base.Dispose(disposing);
    }

    private IRenderedComponent<TComponent> Render<TComponent>()
        where TComponent : Microsoft.AspNetCore.Components.IComponent =>
        RenderComponent<TComponent>(
            ComponentParameter.CreateCascadingValue(null, Snapshot),
            ComponentParameter.CreateParameter("SimulationId", SimulationId));

    private void ClickButton<TComponent>(IRenderedComponent<TComponent> page, string key)
        where TComponent : Microsoft.AspNetCore.Components.IComponent
    {
        string label = Localizer(Services)[key].Value;

        page.FindAll("button").First(button => button.TextContent.Contains(label, StringComparison.Ordinal)).Click();
    }
}
