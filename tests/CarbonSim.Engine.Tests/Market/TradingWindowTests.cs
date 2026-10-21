using CarbonSim.Engine.Clock;
using CarbonSim.Engine.Domain;
using CarbonSim.Engine.Market;
using CarbonSim.Engine.Market.Exchange;
using CarbonSim.Engine.Market.Otc;
using FluentAssertions;

namespace CarbonSim.Engine.Tests.Market;

/// <summary>
/// Trading happens in a window and the engine refuses what falls outside it: a bid only while the
/// section's auction is open, an order or an offer only while the year is running. Before this the
/// disabled button was the only guard, so a hub client or a stale page could bid after the auction
/// had closed or place an order into a halted year. Cancelling and rejecting stay allowed: those
/// take a player out of the market rather than into it.
/// </summary>
public sealed class TradingWindowTests
{
    private static readonly Product Vintage1 = Product.Allowance(1);

    private static (Simulation Simulation, SimulationClock Clock, TestTimeProvider Time) Running()
    {
        Simulation simulation = TestSimulation.Build();
        TestTimeProvider time = new();
        SimulationClock clock = new(simulation, time);

        simulation.Ledger.Grant(simulation.FindCompany(1), Vintage1, 1_000_000m);
        clock.Start();

        return (simulation, clock, time);
    }

    private static OrderRequest Buy(Simulation simulation) =>
        new(simulation.FindUnit(1), Vintage1, OrderSide.Buy, OrderKind.Limit, 100m, 105m);

    private static OrderRequest Sell(Simulation simulation) =>
        new(simulation.FindUnit(1), Vintage1, OrderSide.Sell, OrderKind.Limit, 100m, 105m);

    [Fact]
    public void A_bid_is_taken_only_while_the_sections_auction_window_is_open()
    {
        (Simulation simulation, SimulationClock clock, TestTimeProvider time) = Running();

        // The year is 20 minutes in four sections with a 3-minute window each, so section 1's window
        // runs from the start of the year to minute 3, and the rest of the section is the gap.
        clock.IsAuctionOpen.Should().BeTrue("the first window opens with the year");
        Action insideTheWindow = () => TradingWindow.RequireAuctionOpen(clock);
        insideTheWindow.Should().NotThrow();

        time.Advance(TimeSpan.FromMinutes(3));

        clock.IsAuctionOpen.Should().BeFalse("the window has closed and the section's gap has begun");
        Action afterTheWindow = () => TradingWindow.RequireAuctionOpen(clock);
        afterTheWindow.Should().Throw<TradeRefusedException>()
            .Which.Refusal.Should().Be(TradeRefusal.AuctionClosed);
    }

    [Fact]
    public void An_order_is_taken_while_the_year_runs_and_refused_once_trading_is_halted()
    {
        (Simulation simulation, SimulationClock clock, _) = Running();
        Exchange exchange = new(simulation);

        exchange.Place(Buy(simulation)).Should().NotBeNull("the year is running");

        clock.RequestHalt();
        clock.State.Should().Be(SimulationState.TradingHalted);

        Action whileHalted = () => exchange.Place(Buy(simulation));

        whileHalted.Should().Throw<TradeRefusedException>("orders are only taken while the year runs")
            .Which.Refusal.Should().Be(TradeRefusal.YearNotRunning);
    }

    [Fact]
    public void An_order_is_refused_after_the_year_has_ended_and_while_the_clock_is_paused()
    {
        (Simulation simulation, SimulationClock clock, _) = Running();
        Exchange exchange = new(simulation);

        clock.Pause();
        Action whilePaused = () => exchange.Place(Buy(simulation));
        whilePaused.Should().Throw<TradeRefusedException>().Which.Refusal.Should().Be(TradeRefusal.YearNotRunning);

        clock.Resume();
        clock.RequestHalt();
        clock.EndYear();
        clock.State.Should().Be(SimulationState.YearEnded);

        Action afterTheYear = () => exchange.Place(Buy(simulation));

        afterTheYear.Should().Throw<TradeRefusedException>().Which.Refusal.Should().Be(TradeRefusal.YearNotRunning);
    }

    [Fact]
    public void An_order_can_still_be_cancelled_while_trading_is_halted()
    {
        (Simulation simulation, SimulationClock clock, _) = Running();
        Exchange exchange = new(simulation);
        Order resting = exchange.Place(Sell(simulation));

        clock.RequestHalt();

        exchange.Cancel(simulation.FindCompany(1), resting.Id).Should().BeSameAs(resting);
    }

    [Fact]
    public void An_offer_and_its_acceptance_are_refused_outside_the_window_but_a_rejection_is_not()
    {
        (Simulation simulation, SimulationClock clock, _) = Running();
        OtcMarket otc = new(simulation);
        Unit seller = simulation.FindUnit(1);
        Unit buyer = simulation.FindUnit(4);

        OtcOffer waiting = otc.Send(seller, buyer, Vintage1, price: 105m, volume: 200m);

        clock.RequestHalt();

        Action whileHalted = () => otc.Send(seller, buyer, Vintage1, price: 105m, volume: 200m);
        Action accepting = () => otc.Accept(buyer, waiting.Id);

        whileHalted.Should().Throw<TradeRefusedException>("an offer is only made while the year runs")
            .Which.Refusal.Should().Be(TradeRefusal.YearNotRunning);
        accepting.Should().Throw<TradeRefusedException>("an offer is only accepted while the year runs")
            .Which.Refusal.Should().Be(TradeRefusal.YearNotRunning);
        otc.Reject(buyer, waiting.Id).State.Should().Be(OtcOfferState.Rejected, "turning an offer down is always allowed");
    }
}
