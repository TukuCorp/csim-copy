using System.Globalization;
using System.Security.Claims;
using CarbonSim.Engine.Compliance;
using CarbonSim.Engine.Domain;
using CarbonSim.Engine.Scenarios;
using CarbonSim.Web.Player;
using CarbonSim.Web.Simulations;
using FluentAssertions;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;

namespace CarbonSim.Web.Tests.Simulations;

/// <summary>
/// Manual surrender as a player meets it: the Surrender and Banking screen shows what the company
/// can surrender and the penalty it is heading for, the player sets a surrender through the same
/// owner-checked path as every other verb, and the year end surrenders exactly that.
/// </summary>
public sealed class PlayerSurrenderTests : IClassFixture<SimulationTestHost>
{
    private readonly SimulationTestHost _host;

    public PlayerSurrenderTests(SimulationTestHost host) => _host = host;

    [Fact]
    public async Task A_player_sets_a_surrender_the_screen_previews_it_and_the_year_end_surrenders_it()
    {
        Guid id = Registry().Start(Build(automatic: false));
        SimulationClockService driver = _host.CreateDriver();
        SignedIn delta = await _host.SignInAsync(TestRunFactory.DeltaCompany);
        PlayerSession session = Session(delta.AccountId);
        await driver.StartAsync(id);

        SurrenderView before = (await session.ForPlayerAsync(id))!.Surrender!;

        before.Manual.Should().BeTrue();
        before.HasInstruction.Should().BeFalse();
        before.Shortfall.Should().Be(before.Obligation, "nothing is surrendered until the player says so");
        before.EstimatedPenalty.Should().BePositive();

        decimal allowances = Math.Min(before.AllowancesAvailable, before.Obligation) / 2m;
        await Registry().SetMySurrenderAsync(id, delta.Actor, allowances, 0m);

        SurrenderView after = (await session.ForPlayerAsync(id))!.Surrender!;

        after.HasInstruction.Should().BeTrue();
        after.Allowances.Should().Be(allowances);
        after.Shortfall.Should().Be(before.Obligation - allowances);
        after.EstimatedPenalty.Should().BeLessThan(before.EstimatedPenalty);

        await driver.RequestHaltAsync(id);
        await driver.EndYearAsync(id);

        Simulation simulation = Registry().Run(id).Simulation;
        Company company = simulation.Companies.Single(candidate => candidate.Name == TestRunFactory.DeltaCompany);
        CompanyCompliance settled = simulation.Compliance.For(company, 1)!;

        settled.AllowancesSurrendered.Should().Be(allowances);
        settled.PenaltyCash.Should().Be(after.EstimatedPenalty, "the penalty the screen warned about is the one charged");
    }

    [Fact]
    public async Task Under_automatic_surrender_the_player_has_nothing_to_set()
    {
        Guid id = Registry().Start(Build(automatic: true));
        SimulationClockService driver = _host.CreateDriver();
        SignedIn delta = await _host.SignInAsync(TestRunFactory.DeltaCompany);
        await driver.StartAsync(id);

        SurrenderView view = (await Session(delta.AccountId).ForPlayerAsync(id))!.Surrender!;
        Func<Task> set = () => Registry().SetMySurrenderAsync(id, delta.Actor, 1m, 0m);

        view.Manual.Should().BeFalse();
        await set.Should().ThrowAsync<InvalidOperationException>();
    }

    private SimulationRegistry Registry() => _host.Server.Services.GetRequiredService<SimulationRegistry>();

    private PlayerSession Session(int accountId) => new(
        Registry(),
        _host.Server.Services.GetRequiredService<IServiceScopeFactory>(),
        new FixedAuthenticationStateProvider(accountId));

    private static Simulation Build(bool automatic) => ScenarioLoader.LoadJson(
        TestRunFactory.ScenarioJson.Replace(
            "\"volatilityBand\":",
            $"\"automaticSurrender\": {(automatic ? "true" : "false")}, \"volatilityBand\":",
            StringComparison.Ordinal),
        "player-surrender");

    /// <summary>Signs a session in without a Blazor circuit: the account id a real cookie carries.</summary>
    private sealed class FixedAuthenticationStateProvider : AuthenticationStateProvider
    {
        private readonly ClaimsPrincipal _user;

        public FixedAuthenticationStateProvider(int accountId)
        {
            _user = new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim(ClaimTypes.NameIdentifier, accountId.ToString(CultureInfo.InvariantCulture))],
                authenticationType: "test"));
        }

        public override Task<AuthenticationState> GetAuthenticationStateAsync() =>
            Task.FromResult(new AuthenticationState(_user));
    }
}
