using CarbonSim.Engine.Domain;
using CarbonSim.Engine.Run;
using CarbonSim.Web.Simulations;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace CarbonSim.Web.Tests.Simulations;

/// <summary>
/// Every way the host changes a run goes through the one primitive, and the primitive is what
/// invalidates the screens' run-wide cache. Each public mutating method is exercised here and the
/// run's revision checked after it: a path that changes a run without going through the primitive
/// leaves a screen reading the state from before the change, and that is the bug this catches.
/// </summary>
public sealed class MutationPrimitiveTests : IClassFixture<SimulationTestHost>
{
    private readonly SimulationTestHost _host;

    public MutationPrimitiveTests(SimulationTestHost host)
    {
        _host = host;
    }

    [Fact]
    public async Task Every_public_mutation_advances_the_run_revision()
    {
        SimulationRegistry runs = _host.Server.Services.GetRequiredService<SimulationRegistry>();
        Simulation simulation = TestRunFactory.Build();
        Guid id = runs.Start(simulation);
        SignedIn delta = await _host.SignInAsync(TestRunFactory.DeltaCompany);
        SignedIn redRiver = await _host.SignInAsync(TestRunFactory.RedRiverCompany);
        SimulationClockService driver = _host.CreateDriver();

        await AdvancedAsync(id, "start the year", () => driver.StartAsync(id));
        await AdvancedAsync(id, "chat", () => runs.PostMessageAsync(id, delta.Actor, "hello"));

        OrderOutcome order = await AdvancedAsync(id, "an order", () => runs.PlaceOrderAsync(id, delta.Actor, 1, "Vintage 1", "Buy", "Limit", 10m, 105m));
        await AdvancedAsync(id, "cancelling it", () => runs.CancelOrderAsync(id, delta.Actor, 1, order.OrderId));

        long offer = await AdvancedAsync(id, "an OTC offer", () => runs.SendOtcOfferAsync(id, delta.Actor, 1, 2, "Vintage 1", 105m, 10m));
        await AdvancedAsync(id, "turning it down", () => runs.AnswerOtcOfferAsync(id, redRiver.Actor, 2, offer, false));
        await AdvancedAsync(id, "a player's abatement", () => runs.ImplementAbatementAsync(id, delta.Actor, 1, 0));

        // Into the first section's auction window, so a bid is a legal mutation rather than a refusal.
        _host.FakeTime.Advance(TimeSpan.FromMinutes(1));
        await AdvancedAsync(id, "a tick", () => driver.TickAsync(id));
        // Bid above the floor the collar is about to move to, so the two mutations do not interact:
        // a bid left under a raised floor is a separate finding, not this test's subject.
        await AdvancedAsync(id, "a bid", () => runs.BidAtAuctionAsync(id, delta.Actor, 1, 1, 150m, 10m));

        await AdvancedAsync(id, "pausing", () => driver.PauseAsync(id));
        await AdvancedAsync(id, "resuming", () => driver.ResumeAsync(id));
        await AdvancedAsync(id, "moving the collar", () => driver.ChangeRulesAsync(id, 140m, null));
        await AdvancedAsync(id, "a fine", () => driver.IssueFineAsync(id, 1, 10m, "late report"));
        await AdvancedAsync(id, "offsets", () => driver.DisburseOffsetsAsync(id, 1, 10m));
        // The other unit: a project cannot be implemented twice on the same one, and this test is
        // about the mutation path rather than about which unit gets the project.
        await AdvancedAsync(id, "abatement for a unit", () => driver.AddAbatementAsync(id, 2, 0));
        await AdvancedAsync(id, "an emission shock", () => driver.ApplyShockAsync(id, 1, 1, 10m));
        await AdvancedAsync(id, "the AutoTrade switch", () => driver.SetAutoTradeAsync(id, 1, false));
        await AdvancedAsync(id, "the messaging switch", () => driver.SetMessagingAsync(id, false));
        await AdvancedAsync(id, "halting", () => driver.RequestHaltAsync(id));
        await AdvancedAsync(id, "ending the year", () => driver.EndYearAsync(id));
        await AdvancedAsync(id, "opening the next year", () => driver.BeginNextYearAsync(id));

        // EndSimulationAsync is the one verb left out: it needs the last year of the run to be closed,
        // and it goes through the same primitive call as every method above.
    }

    /// <summary>Runs one mutation and checks the run's revision moved, which is the cache's invalidation.</summary>
    private async Task AdvancedAsync(Guid id, string what, Func<Task> mutate)
    {
        long before = Revision(id);

        await mutate();

        Revision(id).Should().BeGreaterThan(before, $"{what} goes through the host's one mutation primitive");
    }

    /// <summary>The same check for a mutation that hands a value back.</summary>
    private async Task<T> AdvancedAsync<T>(Guid id, string what, Func<Task<T>> mutate)
    {
        long before = Revision(id);
        T result = await mutate();

        Revision(id).Should().BeGreaterThan(before, $"{what} goes through the host's one mutation primitive");

        return result;
    }

    private long Revision(Guid id) =>
        _host.Server.Services.GetRequiredService<SimulationRegistry>().Run(id).Revision;
}
