using Bunit;
using CarbonSim.Web.Components.Pages;
using CarbonSim.Web.Player;
using FluentAssertions;

namespace CarbonSim.Web.Tests.Components;

/// <summary>
/// Two details of the original's screens: the auction history says how much of each auction was
/// filled and how many bids it drew once it has cleared (catalog `TotalAwarded`, `TotalBids`), and the
/// OTC product picker says how much of each product the company has on hand
/// (`AvailableAllowancesOnHand_tCO2e`, `AvailableOffsetsOnHand_tCO2e`).
/// </summary>
public sealed class SmallScreenGapsTests : ScreenTestContext
{
    [Fact]
    public void A_cleared_auction_shows_the_share_filled_and_the_bid_count()
    {
        PlayerView sample = PlayerViewFixture.Sample(SimulationId);
        View = sample with
        {
            Auctions = [.. sample.Auctions.Select(auction => auction.IsCleared
                ? auction with { VolumeSold = auction.OfferedVolume * 0.8m, BidCount = 12 }
                : auction)],
        };

        IRenderedComponent<Auction> page = RenderComponent<Auction>(parameters => parameters
            .AddCascadingValue(Snapshot)
            .Add(component => component.SimulationId, SimulationId));

        string history = page.Find("#auction-history").TextContent;

        history.Should().Contain("80.0%");
        history.Should().Contain("12");
    }

    [Fact]
    public void The_otc_picker_says_how_much_of_each_product_is_on_hand()
    {
        View = PlayerViewFixture.Sample(SimulationId);
        HoldingView vintage1 = View.Holdings.Single(holding => holding.Product.Key == "Vintage 1");

        IRenderedComponent<Otc> page = RenderComponent<Otc>(parameters => parameters
            .AddCascadingValue(Snapshot)
            .Add(component => component.SimulationId, SimulationId));

        page.FindAll("#offer-product option").Should().Contain(option =>
            option.TextContent.Contains(CarbonSim.Web.Components.Shared.Display.Tonnes(vintage1.Available), StringComparison.Ordinal));
    }
}
