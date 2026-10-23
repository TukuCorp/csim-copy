using Bunit;
using CarbonSim.Web.Components.Layout;
using CarbonSim.Web.Components.Pages;
using CarbonSim.Web.Player;
using FluentAssertions;

namespace CarbonSim.Web.Tests.Components;

/// <summary>
/// The exchange summary has a row per product, offsets included, so the offset discount can be read
/// off the screen, and each row carries the product's OTC last trade beside the exchange's.
/// </summary>
public sealed class ExchangeSummaryTests : ScreenTestContext
{
    public ExchangeSummaryTests()
    {
        PlayerView sample = PlayerViewFixture.Sample(SimulationId);
        ProductBookView vintage1 = sample.Books[0];

        View = sample with
        {
            Books =
            [
                vintage1 with { OtcLastTrade = 113m, MarketLastTrade = 113m },
                .. sample.Books.Skip(1),
                vintage1 with
                {
                    Product = ProductView.Offset,
                    Label = ProductView.Offset.Key,
                    BestBid = 90m,
                    BestOffer = 94m,
                    LastTrade = 92m,
                    OtcLastTrade = null,
                    MarketLastTrade = 92m,
                    MyOrders = [],
                    RecentTrades = [],
                },
            ],
        };
    }

    [Fact]
    public void Every_product_has_a_row_with_its_exchange_and_otc_prices()
    {
        IRenderedComponent<Exchange> page = RenderComponent<Exchange>(parameters => parameters
            .AddCascadingValue(Snapshot)
            .Add(component => component.SimulationId, SimulationId));

        List<List<string>> rows =
        [
            .. page.Find("#market-summary").QuerySelectorAll("tbody tr")
                .Select(row => row.QuerySelectorAll("td").Select(cell => cell.TextContent.Replace(" USD", string.Empty, StringComparison.Ordinal).Trim()).ToList()),
        ];

        rows.Should().HaveCount(3);
        rows[0].Should().Equal(Localizer(Services)["Vintage"].Value + " 1", "108.00", "110.00", "112.00", "113.00");
        rows[2].Should().Equal(Localizer(Services)["Offset"].Value, "90.00", "92.00", "94.00", "-");
    }

    [Fact]
    public void The_top_bar_last_trade_is_the_latest_deal_in_either_market()
    {
        IRenderedComponent<TopBar> bar = RenderComponent<TopBar>(parameters => parameters.AddCascadingValue(Snapshot));

        bar.Markup.Should().Contain("113.00");
    }
}
