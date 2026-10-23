using CarbonSim.Engine.Clock;
using CarbonSim.Engine.Domain;
using CarbonSim.Engine.Market;
using CarbonSim.Engine.Market.Exchange;
using CarbonSim.Engine.Market.Otc;
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
        // the start of the year to minute three.
        run.Clock.IsAuctionOpen.Should().BeTrue("the year's first auction opens with the year");

        time.Advance(TimeSpan.FromMinutes(3));
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
        time.Advance(TimeSpan.FromMinutes(1));
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
        (SimulationRun run, TestTimeProvider time) = Started();

        // Into the gap after section one's window, before section two's opens.
        time.Advance(TimeSpan.FromMinutes(4));
        run.Advance();

        Action betweenTheWindows = () => run.Bid(run.Simulation.FindUnit(1), 1, 150m, 1_000m);

        betweenTheWindows.Should().Throw<TradeRefusedException>()
            .Which.Refusal.Should().Be(TradeRefusal.AuctionClosed);
        run.Auctions.ForSection(1, 2).Bids.Should().BeEmpty();
    }

    [Fact]
    public void A_bid_the_raised_floor_cannot_hold_is_released_with_its_money_back()
    {
        (SimulationRun run, TestTimeProvider time) = Started();
        Company bidder = run.Simulation.FindCompany(1);

        time.Advance(TimeSpan.FromMinutes(1));
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

        time.Advance(TimeSpan.FromMinutes(1));
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

        time.Advance(TimeSpan.FromMinutes(1));
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

        time.Advance(TimeSpan.FromMinutes(1));
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

        // Halted at the very start, inside the first window with nothing bid: none of the year's auctions ran.
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
    public void A_collar_change_leaves_the_exchange_where_it_was_trading()
    {
        // The collar is set for the primary market, not the secondary one (glossary, Ceiling Price and
        // Floor Price): the exchange keeps its own band around its own last trade.
        (SimulationRun run, _) = Started();
        Product vintage1 = Product.Allowance(1);
        run.Simulation.Ledger.Grant(run.Simulation.FindCompany(1), vintage1, 1_000m);
        Order offer = run.PlaceOrder(new OrderRequest(run.Simulation.FindUnit(1), vintage1, OrderSide.Sell, OrderKind.Limit, 100m, 95m)).Order!;
        decimal anchor = run.Exchange.Book(vintage1).LastTradePrice;

        run.ChangeCollar(105m, null);

        offer.Status.Should().Be(OrderStatus.Open, "an offer under the auction floor may stand on the exchange");
        run.Exchange.Book(vintage1).LastTradePrice.Should().Be(anchor, "the band stays around the exchange's own price");
    }

    [Fact]
    public void A_bid_placed_after_a_release_gets_an_id_of_its_own()
    {
        (SimulationRun run, TestTimeProvider time) = Started();
        time.Advance(TimeSpan.FromMinutes(1));
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

    [Fact]
    public void A_halt_cancels_resting_orders_and_pending_offers_and_hands_their_allowances_back()
    {
        (SimulationRun run, _) = Started();
        Product vintage1 = Product.Allowance(1);
        Company seller = run.Simulation.FindCompany(1);
        run.Simulation.Ledger.Grant(seller, vintage1, 1_000m);
        decimal held = run.Simulation.Ledger.Available(seller, vintage1);
        Order order = run.PlaceOrder(new OrderRequest(run.Simulation.FindUnit(1), vintage1, OrderSide.Sell, OrderKind.Limit, 300m, 105m)).Order!;
        OtcOffer offer = run.SendOffer(run.Simulation.FindUnit(1), run.Simulation.FindUnit(4), vintage1, 100m, 200m).Offer!;

        run.RequestHalt();

        order.Status.Should().Be(OrderStatus.Cancelled, "a halt cancels what is resting on the exchange");
        offer.State.Should().Be(OtcOfferState.Cancelled, "and what is waiting for an answer over the counter");
        run.Simulation.Ledger.Available(seller, vintage1).Should().Be(held, "nothing stays set aside once trading has stopped");
    }

    [Fact]
    public void Closing_the_exchange_mid_year_cancels_its_resting_orders_and_refuses_new_ones_until_it_reopens()
    {
        (SimulationRun run, _) = Started();
        Product vintage1 = Product.Allowance(1);
        Company seller = run.Simulation.FindCompany(1);
        run.Simulation.Ledger.Grant(seller, vintage1, 1_000m);
        decimal held = run.Simulation.Ledger.Available(seller, vintage1);
        OrderRequest sell = new(run.Simulation.FindUnit(1), vintage1, OrderSide.Sell, OrderKind.Limit, 300m, 105m);
        Order order = run.PlaceOrder(sell).Order!;
        OtcOffer offer = run.SendOffer(run.Simulation.FindUnit(1), run.Simulation.FindUnit(4), vintage1, 100m, 200m).Offer!;
        int revision = run.Simulation.RulesRevision;

        run.SetMarketAccess(exchange: false, otc: null, leaderboard: null);

        order.Status.Should().Be(OrderStatus.Cancelled, "a market the trainer closes keeps nothing resting");
        offer.State.Should().Be(OtcOfferState.Pending, "the OTC market was not touched");
        run.Simulation.Rules.OtcAccess.Should().BeTrue();
        run.Simulation.RulesRevision.Should().BeGreaterThan(revision, "the bots re-read the rules on their next pass");
        run.Invoking(r => r.PlaceOrder(sell)).Should().Throw<TradeRefusedException>()
            .Which.Refusal.Should().Be(TradeRefusal.MarketClosed);

        run.SetMarketAccess(exchange: true, otc: false, leaderboard: false);

        offer.State.Should().Be(OtcOfferState.Cancelled, "closing OTC cancels the offers waiting for an answer");
        run.Simulation.Ledger.Available(seller, vintage1).Should().Be(held, "nothing stays set aside in a closed market");
        run.Simulation.Rules.LeaderboardVisible.Should().BeFalse();
        run.PlaceOrder(sell).Order!.Status.Should().Be(OrderStatus.Open, "a reopened exchange takes orders again");
    }

    [Fact]
    public void The_year_running_out_cancels_resting_orders_so_the_year_end_can_surrender_them()
    {
        (SimulationRun run, TestTimeProvider time) = Started();
        Product vintage1 = Product.Allowance(1);
        Company seller = run.Simulation.FindCompany(1);
        decimal held = run.Simulation.Ledger.Available(seller, vintage1);
        Order order = run.PlaceOrder(new OrderRequest(run.Simulation.FindUnit(1), vintage1, OrderSide.Sell, OrderKind.Limit, held, 105m)).Order!;

        time.Advance(run.Simulation.Rules.YearLength);
        run.Advance();

        run.Simulation.State.Should().Be(SimulationState.TradingHalted);
        order.Status.Should().Be(OrderStatus.Cancelled);
        run.Simulation.Ledger.Available(seller, vintage1).Should().Be(held, "the year end reconciles on what the company holds");
    }

    [Fact]
    public void A_bid_can_be_cancelled_while_its_auction_is_open_and_its_money_comes_back()
    {
        // The original lists each pending bid with a "Cancel bid" control while the auction runs.
        (SimulationRun run, TestTimeProvider time) = Started();
        Company bidder = run.Simulation.FindCompany(1);
        run.Bid(run.Simulation.FindUnit(1), 1, 120m, 10_000m);
        run.Bid(run.Simulation.FindUnit(1), 1, 130m, 5_000m);
        int first = run.Auctions.ForSection(1, 1).Bids[0].Id;

        run.CancelBid(bidder, first);

        run.Auctions.ForSection(1, 1).Bids.Should().ContainSingle().Which.Price.Should().Be(130m);
        run.Simulation.Cash.Escrowed(bidder).Should().Be(650_000m, "only the bid left standing keeps its money aside");

        int second = run.Auctions.ForSection(1, 1).Bids[0].Id;
        Action someoneElses = () => run.CancelBid(run.Simulation.FindCompany(2), second);
        someoneElses.Should().Throw<InvalidOperationException>("a company cancels only its own bids");

        time.Advance(TimeSpan.FromMinutes(4));
        run.Advance();
        Action afterTheClose = () => run.CancelBid(bidder, second);
        afterTheClose.Should().Throw<TradeRefusedException>().Which.Refusal.Should().Be(TradeRefusal.AuctionClosed);
    }
}
