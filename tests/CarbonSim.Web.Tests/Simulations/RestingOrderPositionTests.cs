using System.Globalization;
using System.Security.Claims;
using CarbonSim.Engine.Allocation;
using CarbonSim.Engine.Domain;
using CarbonSim.Engine.Market;
using CarbonSim.Web.Player;
using CarbonSim.Web.Simulations;
using FluentAssertions;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;

namespace CarbonSim.Web.Tests.Simulations;

/// <summary>
/// An allowance set aside for a resting order is still the company's, so the position a player reads
/// must not fall when it is committed. This is the test that would have caught the old position bug:
/// the screens summed only what was available and quietly wrote off everything escrowed.
/// </summary>
public sealed class RestingOrderPositionTests : IClassFixture<SimulationTestHost>
{
    private readonly SimulationTestHost _host;

    public RestingOrderPositionTests(SimulationTestHost host)
    {
        _host = host;
    }

    [Fact]
    public async Task A_resting_sell_order_still_counts_as_held()
    {
        Simulation simulation = TestRunFactory.Build();
        SimulationRegistry runs = Registry();
        Guid id = runs.Start(simulation);
        SignedIn delta = await _host.SignInAsync(TestRunFactory.DeltaCompany);
        SimulationClockService driver = _host.CreateDriver();
        PlayerSession session = Session(delta.AccountId);
        Company company = simulation.FindCompany(1);

        await driver.StartAsync(id);
        simulation.Ledger.Grant(company, Product.Allowance(1), 1_000m);

        PlayerView before = await session.ForPlayerAsync(id) ?? throw new InvalidOperationException("no view");

        // A sell order with nothing to cross it: the tonnes are escrowed, not sold.
        await runs.PlaceOrderAsync(id, delta.Actor, 1, "Vintage 1", "Sell", "Limit", 1_000m, 110m);

        PlayerView after = await session.ForPlayerAsync(id) ?? throw new InvalidOperationException("no view");
        Standing standing = new(simulation);

        simulation.Ledger.Escrowed(company, Product.Allowance(1)).Should().Be(1_000m, "the order escrowed them");
        after.Position.Held.Should().Be(before.Position.Held, "an escrowed allowance is still held");
        after.Position.Held.Should().Be(standing.Held(company, 1), "the screen reads the one module");
        after.Position.Shortfall.Should().Be(standing.Shortfall(company, 1), "and so does the shortfall");
    }

    private SimulationRegistry Registry() => _host.Server.Services.GetRequiredService<SimulationRegistry>();

    private PlayerSession Session(int accountId) => new(
        Registry(),
        _host.Server.Services.GetRequiredService<IServiceScopeFactory>(),
        new FixedAuthenticationStateProvider(accountId));

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
