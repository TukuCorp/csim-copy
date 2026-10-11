using System.Globalization;
using System.Security.Claims;
using CarbonSim.Engine.Domain;
using CarbonSim.Engine.Market;
using CarbonSim.Engine.Run;
using CarbonSim.Web.Player;
using CarbonSim.Web.Simulations;
using FluentAssertions;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;

namespace CarbonSim.Web.Tests.Simulations;

/// <summary>
/// The three things an exchange order can do, so the notice a screen reports is read from the run
/// rather than guessed from the next snapshot: a market order that fills against the book, a limit
/// order that rests, and an order the market refuses. The refusal is a value the session hands
/// back, not an exception the screen has to catch.
/// </summary>
public sealed class OrderOutcomeTests : IClassFixture<SimulationTestHost>
{
    private readonly SimulationTestHost _host;

    public OrderOutcomeTests(SimulationTestHost host) => _host = host;

    [Fact]
    public async Task A_market_order_that_crosses_reports_a_complete_fill()
    {
        (Guid id, SignedIn delta, SignedIn redRiver, Simulation simulation, _) = await StartedAsync();

        simulation.Ledger.Grant(simulation.FindCompany(1), Product.Allowance(1), 1_000m);
        await Session(delta.AccountId).PlaceOrderAsync(id, 1, "Vintage 1", "Sell", "Limit", 1_000m, 110m, null, "AllowPartial");

        OrderOutcome outcome = await Session(redRiver.AccountId)
            .PlaceOrderAsync(id, 2, "Vintage 1", "Buy", "Market", 1_000m, null, null, "AllowPartial");

        outcome.Refusal.Should().BeNull();
        outcome.OrderId.Should().BeGreaterThan(0);
        outcome.Filled.Should().Be(1_000m, "the whole order crossed");
        outcome.Resting.Should().Be(0m, "nothing is left on the book");
        outcome.AveragePrice.Should().Be(110m, "it filled at the resting offer");
    }

    [Fact]
    public async Task A_limit_order_that_does_not_cross_reports_what_rests()
    {
        (Guid id, SignedIn delta, SignedIn redRiver, Simulation simulation, _) = await StartedAsync();

        simulation.Ledger.Grant(simulation.FindCompany(1), Product.Allowance(1), 1_000m);
        await Session(delta.AccountId).PlaceOrderAsync(id, 1, "Vintage 1", "Sell", "Limit", 1_000m, 110m, null, "AllowPartial");

        OrderOutcome outcome = await Session(redRiver.AccountId)
            .PlaceOrderAsync(id, 2, "Vintage 1", "Buy", "Limit", 500m, 95m, null, "AllowPartial");

        outcome.Refusal.Should().BeNull();
        outcome.OrderId.Should().BeGreaterThan(0);
        outcome.Filled.Should().Be(0m);
        outcome.Resting.Should().Be(500m, "the offer it met is dearer than its limit");
        outcome.AveragePrice.Should().Be(0m, "nothing traded");
    }

    [Fact]
    public async Task A_year_that_is_not_running_comes_back_as_a_refusal()
    {
        (Guid id, SignedIn delta, _, Simulation simulation, SimulationClockService driver) = await StartedAsync();

        simulation.Ledger.Grant(simulation.FindCompany(1), Product.Allowance(1), 1_000m);
        await driver.RequestHaltAsync(id);

        OrderOutcome outcome = await Session(delta.AccountId)
            .PlaceOrderAsync(id, 1, "Vintage 1", "Sell", "Limit", 100m, 105m, null, "AllowPartial");

        outcome.Refusal.Should().NotBeNull("the year is not running, so the market would not take it");
        outcome.Refusal!.Window.Should().Be(TradeRefusal.YearNotRunning);
        outcome.OrderId.Should().Be(0);
        outcome.Filled.Should().Be(0m);
        outcome.Resting.Should().Be(0m);
    }

    [Fact]
    public async Task An_order_outside_the_band_comes_back_as_a_refusal()
    {
        (Guid id, SignedIn delta, _, _, _) = await StartedAsync();

        OrderOutcome outcome = await Session(delta.AccountId)
            .PlaceOrderAsync(id, 1, "Vintage 1", "Buy", "Limit", 10m, 300m, null, "AllowPartial");

        outcome.Refusal.Should().NotBeNull("300 is far above the ceiling");
        outcome.Refusal!.Window.Should().BeNull("the window was open; the price was the trouble");
        outcome.Refusal.Message.Should().NotBeNullOrWhiteSpace();
        outcome.OrderId.Should().Be(0);
    }

    private SimulationRegistry Runs() => _host.Server.Services.GetRequiredService<SimulationRegistry>();

    private PlayerSession Session(int accountId) => new(
        Runs(),
        _host.Server.Services.GetRequiredService<IServiceScopeFactory>(),
        new FixedAuthenticationStateProvider(accountId));

    /// <summary>A run the host is driving with its first year under way, and both companies signed in.</summary>
    private async Task<(Guid Id, SignedIn Delta, SignedIn RedRiver, Simulation Simulation, SimulationClockService Driver)> StartedAsync()
    {
        Simulation simulation = TestRunFactory.Build();
        Guid id = Runs().Start(simulation);
        SignedIn delta = await _host.SignInAsync(TestRunFactory.DeltaCompany);
        SignedIn redRiver = await _host.SignInAsync(TestRunFactory.RedRiverCompany);
        SimulationClockService driver = _host.CreateDriver();

        await driver.StartAsync(id);

        return (id, delta, redRiver, simulation, driver);
    }

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

