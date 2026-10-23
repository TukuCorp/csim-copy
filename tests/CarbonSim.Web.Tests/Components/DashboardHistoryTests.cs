using AngleSharp.Dom;
using Bunit;
using CarbonSim.Web.Components.Pages;
using FluentAssertions;

namespace CarbonSim.Web.Tests.Components;

/// <summary>
/// The dashboard's histories are the player's own: one auction row per bid the company placed,
/// with what it bid, what the lot cleared at, what it won and what it paid (the original's Year,
/// Auc #, Vintage, You Bid, Bid Vol, Cleared At, You Won, You Paid), and its own trades with the
/// side it was on and what they cost.
/// </summary>
public sealed class DashboardHistoryTests : ScreenTestContext
{
    public DashboardHistoryTests()
    {
        View = PlayerViewFixture.Sample(SimulationId);
    }

    [Fact]
    public void My_auction_history_has_one_row_per_own_bid()
    {
        IRenderedComponent<Dashboard> page = Render();

        Cells(page, "#my-auction-history").Should().Equal("1", "1", "1", "105.00", "20,000", "100.00", "20,000", "2,000,000");
    }

    [Fact]
    public void My_trade_history_says_which_side_the_company_was_on_and_what_it_cost()
    {
        IRenderedComponent<Dashboard> page = Render();

        List<string> cells = Cells(page, "#my-trade-history");

        cells.Should().Contain(Localizer(Services)["Buy"].Value);
        cells.Should().Contain("11,000");
        cells.Should().Contain("Red River Power");
    }

    private IRenderedComponent<Dashboard> Render() =>
        RenderComponent<Dashboard>(parameters => parameters
            .AddCascadingValue(Snapshot)
            .Add(component => component.SimulationId, SimulationId));

    private static List<string> Cells(IRenderedComponent<Dashboard> page, string table) =>
        [.. page.Find(table).QuerySelectorAll("tbody td").Select(cell => Plain(cell))];

    // Money cells carry the currency code; the figures are what the test is about.
    private static string Plain(IElement cell) => cell.TextContent.Replace(" USD", string.Empty, StringComparison.Ordinal).Trim();
}
