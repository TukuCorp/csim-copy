using CarbonSim.Engine.Domain;
using CarbonSim.Engine.Market;
using CarbonSim.Web.Simulations;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace CarbonSim.Web.Tests.Simulations;

/// <summary>
/// The regression the Phase 2 review found: a real signed-in account reaches a run as the
/// company it claimed at registration. Nothing here uses a "player-N" convention - the caller's
/// identity is the account id the hub carries, and the company comes from the account row.
/// </summary>
public sealed class ActorResolutionTests : IClassFixture<SimulationTestHost>
{
    private readonly SimulationTestHost _host;

    public ActorResolutionTests(SimulationTestHost host)
    {
        _host = host;
    }

    [Fact]
    public async Task A_signed_in_player_acts_for_its_own_company()
    {
        Simulation simulation = TestRunFactory.Build();
        SimulationRegistry runs = _host.Server.Services.GetRequiredService<SimulationRegistry>();
        Guid id = runs.Start(simulation);
        SignedIn delta = await _host.SignInAsync(TestRunFactory.DeltaCompany);

        await _host.CreateDriver().StartAsync(id);

        Unit seller = simulation.FindUnit(1);
        simulation.Ledger.Grant(seller.Company, Product.Allowance(1), 1_000m);

        long order = await runs.PlaceOrderAsync(id, delta.Actor, 1, "Vintage 1", "Sell", "Limit", 100m, 110m);

        order.Should().BeGreaterThan(0);
        runs.Run(id).Exchange.Book(Product.Allowance(1)).OpenOrders.Should().ContainSingle(sell => sell.Id == order);
    }

    [Fact]
    public async Task A_signed_in_player_cannot_act_for_another_company()
    {
        Simulation simulation = TestRunFactory.Build();
        SimulationRegistry runs = _host.Server.Services.GetRequiredService<SimulationRegistry>();
        Guid id = runs.Start(simulation);
        SignedIn delta = await _host.SignInAsync(TestRunFactory.DeltaCompany);

        await _host.CreateDriver().StartAsync(id);

        Func<Task> bid = () => runs.BidAtAuctionAsync(id, delta.Actor, 2, 1, 140m, 1_000m);

        await bid.Should().ThrowAsync<UnauthorizedAccessException>();
    }
}
