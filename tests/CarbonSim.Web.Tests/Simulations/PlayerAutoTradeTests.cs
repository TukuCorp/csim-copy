using CarbonSim.Engine.Domain;
using CarbonSim.Engine.Scenarios;
using CarbonSim.Web.Simulations;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace CarbonSim.Web.Tests.Simulations;

/// <summary>
/// "Can human players put their facility on AI? Yes, when enabled by the sim-administrator"
/// (FAQ #7; the spec's "AI trade assistance"). A player switches AutoTrade on their own units only,
/// and only in an exercise where the trainer has allowed it.
/// </summary>
public sealed class PlayerAutoTradeTests : IClassFixture<SimulationTestHost>
{
    private readonly SimulationTestHost _host;

    public PlayerAutoTradeTests(SimulationTestHost host) => _host = host;

    [Fact]
    public async Task A_player_puts_their_own_unit_on_autotrade_when_the_trainer_allows_it()
    {
        Simulation simulation = Build(allowed: true);
        Guid id = Registry().Start(simulation);
        SignedIn delta = await _host.SignInAsync(TestRunFactory.DeltaCompany);

        await Registry().SetMyAutoTradeAsync(id, delta.Actor, 1, true);

        simulation.FindUnit(1).AutoTrade.Should().BeTrue();
        Registry().Run(id).Bots!.Bots.Should().Contain(bot => bot.Company.Name == TestRunFactory.DeltaCompany, "the unit is now run by a bot");

        Func<Task> someoneElses = () => Registry().SetMyAutoTradeAsync(id, delta.Actor, 2, true);
        await someoneElses.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task Without_the_trainers_permission_the_switch_is_refused()
    {
        Simulation simulation = Build(allowed: false);
        Guid id = Registry().Start(simulation);
        SignedIn delta = await _host.SignInAsync(TestRunFactory.DeltaCompany);

        Func<Task> switchOn = () => Registry().SetMyAutoTradeAsync(id, delta.Actor, 1, true);

        await switchOn.Should().ThrowAsync<InvalidOperationException>();
        simulation.FindUnit(1).AutoTrade.Should().BeFalse();
    }

    private SimulationRegistry Registry() => _host.Server.Services.GetRequiredService<SimulationRegistry>();

    private static Simulation Build(bool allowed) => ScenarioLoader.LoadJson(
        TestRunFactory.ScenarioJson.Replace(
            "\"volatilityBand\":",
            $"\"aiTradeAssistance\": {(allowed ? "true" : "false")}, \"volatilityBand\":",
            StringComparison.Ordinal),
        "player-autotrade");
}
