using CarbonSim.Engine.Clock;
using CarbonSim.Engine.Domain;
using CarbonSim.Engine.Market;
using CarbonSim.Engine.Market.Exchange;
using CarbonSim.Engine.Run;
using FluentAssertions;

namespace CarbonSim.Engine.Tests.Run;

/// <summary>
/// The run module's own behaviour: the order one advance works in, and what closing a year does to an
/// auction that is still open. These are the engine-level tests the host's console tests were standing
/// in for, and they are what stops the host and the test loops drifting apart again.
/// </summary>
public sealed class SimulationRunTests
{
    /// <summary>A run under way, with the hand-driven clock it advances on.</summary>
    private static (SimulationRun Run, TestTimeProvider Time) Started()
    {
        Simulation simulation = TestSimulation.Build();
        TestTimeProvider time = new();
        SimulationRun run = SimulationRun.Create(simulation, time);

        run.Start();

        return (run, time);
    }

    [Fact]
    public void One_advance_clears_the_auction_that_closed_and_says_so_once()
    {
        (SimulationRun run, TestTimeProvider time) = Started();
        Auction auction = run.Auctions.ForSection(1, 1);

        // A 20-minute year in four sections with a 3-minute window each: section one's window runs from
        // minute two to minute five.
        run.Clock.IsAuctionOpen.Should().BeFalse("the window opens near the end of the section");

        time.Advance(TimeSpan.FromMinutes(5));
        RunResult closing = run.Advance();

        auction.IsCleared.Should().BeTrue("the tick that closed the section cleared its auction");
        closing.Notices.Should().Contain(
            notice => notice.Kind == ClockEventKind.AuctionClosed && notice.Cleared != null,
            "the close rides with the tick that made it");
        auction.Results.Should().ContainSingle("a lot clears once");

        // And no later tick clears or announces it again.
        RunResult later = run.Advance();

        later.Notices.Should().NotContain(notice => notice.Kind == ClockEventKind.AuctionClosed);
        auction.Results.Should().ContainSingle();
    }

    [Fact]
    public void Closing_the_year_mid_auction_clears_it_first_and_honours_the_bids()
    {
        (SimulationRun run, TestTimeProvider time) = Started();

        // Into the first section's auction window, and bid into it.
        time.Advance(TimeSpan.FromMinutes(3));
        run.Advance();
        run.Clock.IsAuctionOpen.Should().BeTrue();
        run.Bid(run.Simulation.FindUnit(1), 1, 150m, 100_000m);

        // The trainer halts and closes the year while that window is still open.
        run.RequestHalt();
        RunResult closed = run.EndYear();

        Auction auction = run.Auctions.ForSection(1, 1);

        auction.IsCleared.Should().BeTrue("a year closed early honours the bids the room already made");
        auction.Results.Should().ContainSingle().Which.VolumeSold.Should().Be(100_000m);
        closed.Notices.Should().Contain(
            notice => notice.Kind == ClockEventKind.AuctionClosed && notice.Cleared != null,
            "the close goes out like any other, so the room sees the result");
        run.Simulation.Compliance.Results.Should().NotBeEmpty("the year was reconciled as it closed");
        run.Simulation.State.Should().Be(SimulationState.YearEnded);
    }

    [Fact]
    public void A_bid_outside_the_window_is_refused_and_leaves_the_auction_alone()
    {
        (SimulationRun run, _) = Started();

        Action beforeTheWindow = () => run.Bid(run.Simulation.FindUnit(1), 1, 150m, 1_000m);

        beforeTheWindow.Should().Throw<TradeRefusedException>()
            .Which.Refusal.Should().Be(TradeRefusal.AuctionClosed);
        run.Auctions.ForSection(1, 1).Bids.Should().BeEmpty();
    }

    [Fact]
    public void A_bid_the_raised_floor_cannot_hold_is_released_with_its_money_back()
    {
        (SimulationRun run, TestTimeProvider time) = Started();
        Company bidder = run.Simulation.FindCompany(1);

        time.Advance(TimeSpan.FromMinutes(3));
        run.Advance();
        run.Bid(run.Simulation.FindUnit(1), 1, 120m, 10_000m);

        run.Simulation.Cash.Escrowed(bidder).Should().Be(1_200_000m, "a bid sets its price aside");

        RunResult changed = run.ChangeCollar(140m, null);

        changed.Collar.Should().NotBeNull();
        changed.Collar!.BidsReleased.Should().ContainSingle("a floor of 140 cannot hold a bid at 120");
        run.Auctions.ForSection(1, 1).Bids.Should().BeEmpty("the bid came off the book");
        run.Simulation.Cash.Escrowed(bidder).Should().Be(0m, "the money went back to the company");

        // And the section then closes over an empty book, which used to fail on escrow.
        time.Advance(TimeSpan.FromMinutes(2));
        Action closing = () => run.Advance();

        closing.Should().NotThrow("nothing is left to clear");
        run.Auctions.ForSection(1, 1).IsCleared.Should().BeTrue();
    }

    [Fact]
    public void A_bid_above_a_cut_ceiling_is_released_the_same_way()
    {
        (SimulationRun run, TestTimeProvider time) = Started();
        Company bidder = run.Simulation.FindCompany(1);

        time.Advance(TimeSpan.FromMinutes(3));
        run.Advance();
        run.Bid(run.Simulation.FindUnit(1), 1, 280m, 1_000m);

        RunResult changed = run.ChangeCollar(null, 250m);

        changed.Collar!.BidsReleased.Should().ContainSingle("a ceiling of 250 cannot hold a bid at 280");
        run.Auctions.ForSection(1, 1).Bids.Should().BeEmpty();
        run.Simulation.Cash.Escrowed(bidder).Should().Be(0m);
    }

    [Fact]
    public void A_bid_inside_the_new_collar_stays_where_it_is()
    {
        (SimulationRun run, TestTimeProvider time) = Started();

        time.Advance(TimeSpan.FromMinutes(3));
        run.Advance();
        run.Bid(run.Simulation.FindUnit(1), 1, 150m, 1_000m);

        RunResult changed = run.ChangeCollar(140m, null);

        changed.Collar!.BidsReleased.Should().BeEmpty("a bid at 150 is inside a 140..300 collar");
        run.Auctions.ForSection(1, 1).Bids.Should().ContainSingle();
    }

    [Fact]
    public void An_auction_holding_a_bid_outside_the_collar_refuses_to_clear_and_says_why()
    {
        (SimulationRun run, TestTimeProvider time) = Started();

        time.Advance(TimeSpan.FromMinutes(3));
        run.Advance();
        run.Bid(run.Simulation.FindUnit(1), 1, 120m, 1_000m);

        // Move the collar behind the run's back, which is the state the release exists to prevent. The
        // rule is internal, so only the module moves a collar; a test may still force the violation.
        run.Simulation.Rules.AuctionFloorPrice = 140m;

        Action clearing = () => run.Auctions.ForSection(1, 1).Clear();

        clearing.Should().Throw<InvalidOperationException>()
            .WithMessage("*outside the collar*", "settling it would fail deep in the cash ledger instead");
    }

    [Fact]
    public void Closing_the_year_early_clears_every_auction_left_in_it()
    {
        (SimulationRun run, _) = Started();

        // Halted in the first section, before any window opened: none of the year's auctions ran.
        run.RequestHalt();
        run.EndYear();

        run.Auctions.Auctions.Where(auction => auction.Year == 1)
            .Should().OnlyContain(auction => auction.IsCleared, "an auction left open would keep its lots out of the reserve");
    }

    [Fact]
    public void A_floor_of_zero_is_refused_before_anything_moves()
    {
        (SimulationRun run, _) = Started();
        decimal floor = run.Simulation.Rules.AuctionFloorPrice;
        int revision = run.Simulation.RulesRevision;

        Action toZero = () => run.ChangeCollar(0m, null);

        toZero.Should().Throw<ArgumentOutOfRangeException>("a book cannot be anchored on a price of zero");
        run.Simulation.Rules.AuctionFloorPrice.Should().Be(floor);
        run.Simulation.RulesRevision.Should().Be(revision);
    }

    [Fact]
    public void A_collar_change_counts_only_the_orders_it_released()
    {
        (SimulationRun run, _) = Started();
        Product vintage1 = Product.Allowance(1);
        run.Simulation.Ledger.Grant(run.Simulation.FindCompany(1), vintage1, 1_000m);
        run.PlaceOrder(new OrderRequest(run.Simulation.FindUnit(1), vintage1, OrderSide.Sell, OrderKind.Limit, 100m, 95m));

        run.ChangeCollar(105m, null).Collar!.OrdersReleased.Should().Be(1, "a floor of 105 cannot hold an offer at 95");

        run.ChangeCollar(null, 280m).Collar!.OrdersReleased.Should().Be(0, "nothing was left outside the second change");
    }

    [Fact]
    public void A_bid_placed_after_a_release_gets_an_id_of_its_own()
    {
        (SimulationRun run, TestTimeProvider time) = Started();
        time.Advance(TimeSpan.FromMinutes(3));
        run.Advance();
        Unit unit = run.Simulation.FindUnit(1);
        run.Bid(unit, 1, 120m, 1_000m);
        run.Bid(unit, 1, 150m, 1_000m);

        run.ChangeCollar(140m, null);
        run.Bid(unit, 1, 160m, 1_000m);

        run.Auctions.ForSection(1, 1).Bids.Select(bid => bid.Id).Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public void Moving_only_the_ceiling_leaves_the_market_where_it_was_trading()
    {
        (SimulationRun run, _) = Started();
        Product vintage1 = Product.Allowance(1);
        run.Simulation.Ledger.Grant(run.Simulation.FindCompany(1), vintage1, 1_000m);
        run.PlaceOrder(new OrderRequest(run.Simulation.FindUnit(1), vintage1, OrderSide.Sell, OrderKind.Limit, 10m, 108m));
        run.PlaceOrder(new OrderRequest(run.Simulation.FindUnit(4), vintage1, OrderSide.Buy, OrderKind.Limit, 10m, 108m));

        // Trading at 108, the band is 97.2..118.8, so an offer at 115 can rest.
        Order offer = run.PlaceOrder(new OrderRequest(run.Simulation.FindUnit(1), vintage1, OrderSide.Sell, OrderKind.Limit, 10m, 115m)).Order!;

        run.ChangeCollar(null, 280m);

        run.Exchange.Book(vintage1).LastTradePrice.Should().Be(108m, "the floor did not move, so neither does the market");
        offer.Status.Should().Be(OrderStatus.Open);
    }
}
