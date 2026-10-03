using CarbonSim.Engine.Domain;
using CarbonSim.Engine.Market;
using CarbonSim.Web.Simulations;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace CarbonSim.Web.Tests.Simulations;

/// <summary>A player action reaches the engine only through the company the caller signed in for.</summary>
public sealed class RegistryGuardTests : IClassFixture<SimulationTestHost>
{
    private readonly SimulationTestHost _host;

    public RegistryGuardTests(SimulationTestHost host)
    {
        _host = host;
    }

    [Fact]
    public async Task A_bid_from_another_company_is_refused()
    {
        Simulation simulation = TestRunFactory.Build();
        SimulationRegistry runs = _host.Server.Services.GetRequiredService<SimulationRegistry>();
        Guid id = runs.Start(simulation);
        SignedIn redRiver = await _host.SignInAsync(TestRunFactory.RedRiverCompany);

        await _host.CreateDriver().StartAsync(id);

        Func<Task> bid = () => runs.BidAtAuctionAsync(id, redRiver.Actor, 1, 1, 140m, 1_000m);

        await bid.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task An_order_from_another_company_is_refused()
    {
        Simulation simulation = TestRunFactory.Build();
        SimulationRegistry runs = _host.Server.Services.GetRequiredService<SimulationRegistry>();
        Guid id = runs.Start(simulation);
        SignedIn redRiver = await _host.SignInAsync(TestRunFactory.RedRiverCompany);

        await _host.CreateDriver().StartAsync(id);

        // The seller needs something to sell: grant it vintage-1 allowances directly.
        Unit seller = simulation.FindUnit(1);
        simulation.Ledger.Grant(seller.Company, Product.Allowance(1), 1_000m);

        Func<Task> sell = () => runs.PlaceOrderAsync(id, redRiver.Actor, 1, "Vintage 1", "Sell", "Limit", 100m, 110m);

        await sell.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task An_offer_answer_from_a_stranger_is_refused()
    {
        Simulation simulation = TestRunFactory.Build();
        SimulationRegistry runs = _host.Server.Services.GetRequiredService<SimulationRegistry>();
        Guid id = runs.Start(simulation);
        SignedIn delta = await _host.SignInAsync(TestRunFactory.DeltaCompany);

        await _host.CreateDriver().StartAsync(id);

        Unit seller = simulation.FindUnit(1);
        simulation.Ledger.Grant(seller.Company, Product.Allowance(1), 1_000m);
        long offer = await runs.SendOtcOfferAsync(id, delta.Actor, 1, 2, "Vintage 1", 110m, 100m);

        // Unit 1's company tries to answer its own offer: only the named buyer unit may.
        Func<Task> answer = () => runs.AnswerOtcOfferAsync(id, delta.Actor, 1, offer, true);

        await answer.Should().ThrowAsync<InvalidOperationException>();
        offer.Should().Be(1);
    }

    [Fact]
    public async Task An_identity_with_no_company_cannot_act()
    {
        Simulation simulation = TestRunFactory.Build();
        SimulationRegistry runs = _host.Server.Services.GetRequiredService<SimulationRegistry>();
        Guid id = runs.Start(simulation);

        await _host.CreateDriver().StartAsync(id);

        Func<Task> bid = () => runs.BidAtAuctionAsync(id, "9999", 1, 1, 140m, 1_000m);

        await bid.Should().ThrowAsync<UnauthorizedAccessException>();
    }
}
