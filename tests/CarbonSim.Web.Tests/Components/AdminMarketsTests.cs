using Bunit;
using CarbonSim.Web.Admin;
using CarbonSim.Web.Components.Admin;
using CarbonSim.Web.Player;
using FluentAssertions;

namespace CarbonSim.Web.Tests.Components;

/// <summary>
/// The console's live views (catalog `LiveAllowanceAuctionUpdates`, `ExchangeMarketLiveUpdates`): the
/// open auction's lots with their bids, and every exchange book with this year's trading.
/// </summary>
public sealed class AdminMarketsTests : ScreenTestContext
{
    [Fact]
    public void The_live_page_shows_the_auction_lots_and_every_book()
    {
        AdminRunDetail detail = AdminViewFixture.Detail(SimulationId, "Running") with
        {
            LiveAuction = new AdminLiveAuctionView(1, 2, Open: true, Cleared: false,
            [
                new AdminLiveLotView(1, false, 250_000m, Bids: 7, Bidders: 4, BidVolume: 310_000m, HighestBid: 131m, ClearingPrice: null),
                new AdminLiveLotView(2, true, 50_000m, Bids: 0, Bidders: 0, BidVolume: 0m, HighestBid: null, ClearingPrice: null),
            ]),
            LiveBooks =
            [
                new AdminLiveBookView(ProductView.Allowance(1), 118m, 121m, 120m, 12, 40_000m, 25_000m, 33, 180_000m),
                new AdminLiveBookView(ProductView.Offset, 101m, null, null, 3, 9_000m, 0m, 0, 0m),
            ],
        };
        AdminSnapshot snapshot = AdminViewFixture.Sample(SimulationId) with { Run = detail };

        IRenderedComponent<AdminMarkets> page = RenderComponent<AdminMarkets>(parameters => parameters
            .AddCascadingValue(snapshot)
            .Add(component => component.SimulationId, SimulationId));

        List<string> lot = Row(page, "#live-auction tbody tr", 0);
        lot.Should().Contain("7").And.Contain("4").And.Contain("310,000");
        page.Find("#live-auction").TextContent.Should().Contain("131.00");

        List<string> offsets = Row(page, "#live-books tbody tr", 1);
        offsets[0].Should().Be(Localizer(Services)["Offset"].Value);
        offsets.Should().Contain("-", "a book that has not traded has no last price");
    }

    [Fact]
    public void Before_the_first_year_there_is_no_auction_to_watch()
    {
        AdminSnapshot snapshot = AdminViewFixture.Sample(SimulationId) with
        {
            Run = AdminViewFixture.Detail(SimulationId, "Pending") with { LiveAuction = null },
        };

        IRenderedComponent<AdminMarkets> page = RenderComponent<AdminMarkets>(parameters => parameters
            .AddCascadingValue(snapshot)
            .Add(component => component.SimulationId, SimulationId));

        page.FindAll("#live-auction tbody tr").Should().BeEmpty();
    }

    [Fact]
    public void The_reports_chart_the_average_price_year_by_year()
    {
        // The original's "Historical Average Carbon Price" report is a chart as well as a table.
        AdminSnapshot snapshot = AdminViewFixture.Sample(SimulationId) with
        {
            Run = AdminViewFixture.Detail(SimulationId, "YearEnded") with
            {
                Prices =
                [
                    new AdminPriceView(1, 100_000m, 126.5m, 10_000m, 110m, 125m, 120m, 130m, 40),
                    new AdminPriceView(2, 90_000m, 231m, 0m, 0m, null, null, null, 30),
                ],
            },
        };

        RenderComponent<AdminReports>(parameters => parameters
            .AddCascadingValue(snapshot)
            .Add(component => component.SimulationId, SimulationId));

        string chart = Charts.Options.Should().ContainSingle(option => option.Contains("\"line\"", StringComparison.Ordinal)).Which;
        chart.Should().Contain("126.5").And.Contain("231");
        chart.Should().Contain("null", "a year with no offset trades has no offset average to plot");
    }

    private static List<string> Row(IRenderedComponent<AdminMarkets> page, string rows, int index) =>
        [.. page.FindAll(rows).ElementAt(index).QuerySelectorAll("td, th").Select(cell => cell.TextContent.Replace(" USD", string.Empty, StringComparison.Ordinal).Trim())];
}
