using CarbonSim.Engine.Domain;
using CarbonSim.Engine.Market;
using CarbonSim.Web.Simulations;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace CarbonSim.Web.Tests.Simulations;

/// <summary>
/// What the host refuses: a bid outside its section's auction window, and an order into a year that
/// is not running. The disabled button used to be the only guard, so a stale page or a hub client
/// could act out of window and the auction or the journal would take it.
/// </summary>
public sealed class TradingWindowHostTests : IClassFixture<SimulationTestHost>
{
    private readonly SimulationTestHost _host;

    public TradingWindowHostTests(SimulationTestHost host)
    {
        _host = host;
    }

    [Fact]
    public async Task A_bid_outside_the_auction_window_never_reaches_the_auction()
    {
        (Guid id, SignedIn delta, SimulationClockService driver, Simulation _) = await StartedAsync();

        // Past the first section's window, which opens with the year and closes at 2:15.
        _host.FakeTime.Advance(TimeSpan.FromMinutes(3));
        await driver.TickAsync(id);
        Runs().Run(id).Clock.IsAuctionOpen.Should().BeFalse();

        Func<Task> bid = () => Runs().BidAtAuctionAsync(id, delta.Actor, 1, 1, 120m, 1_000m);

        await bid.Should().ThrowAsync<TradeRefusedException>()
            .Where(exception => exception.Refusal == TradeRefusal.AuctionClosed);
        Runs().Run(id).Auctions.ForSection(1, 1).Bids.Should().BeEmpty("the bid never reached the auction");
    }

    [Fact]
    public async Task An_order_into_a_halted_year_is_refused_and_no_order_appears()
    {
        (Guid id, SignedIn delta, SimulationClockService driver, Simulation simulation) = await StartedAsync();

        simulation.Ledger.Grant(simulation.FindCompany(1), Product.Allowance(1), 1_000m);
        await Runs().PlaceOrderAsync(id, delta.Actor, 1, "Vintage 1", "Sell", "Limit", 100m, 110m);

        await driver.RequestHaltAsync(id);
        Runs().Run(id).Simulation.State.Should().Be(SimulationState.TradingHalted);

        Func<Task> order = () => Runs().PlaceOrderAsync(id, delta.Actor, 1, "Vintage 1", "Sell", "Limit", 100m, 105m);

        await order.Should().ThrowAsync<TradeRefusedException>()
            .Where(exception => exception.Refusal == TradeRefusal.YearNotRunning);
        Runs().Run(id).Exchange.Book(Product.Allowance(1)).OpenOrders.Should()
            .BeEmpty("the halt cancelled the order placed while the year was running, and the refused one never arrived");
    }

    private SimulationRegistry Runs() => _host.Server.Services.GetRequiredService<SimulationRegistry>();

    /// <summary>A run the host is driving with its first year under way, and a player signed in for it.</summary>
    private async Task<(Guid Id, SignedIn Delta, SimulationClockService Driver, Simulation Simulation)> StartedAsync()
    {
        Simulation simulation = TestRunFactory.Build();
        Guid id = Runs().Start(simulation);
        SignedIn delta = await _host.SignInAsync(TestRunFactory.DeltaCompany);
        SimulationClockService driver = _host.CreateDriver();

        await driver.StartAsync(id);

        return (id, delta, driver, simulation);
    }
}
