using System.Globalization;
using System.Security.Claims;
using CarbonSim.Engine.Clock;
using CarbonSim.Engine.Domain;
using CarbonSim.Engine.Market;
using CarbonSim.Engine.Scenarios;
using CarbonSim.Web.Player;
using CarbonSim.Web.Simulations;
using FluentAssertions;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;

namespace CarbonSim.Web.Tests.Simulations;

/// <summary>
/// G1 at the level a player meets it: an allowance won at auction belongs to the company that won
/// it, so the position the screens read has to count it. A bid is placed through the same
/// owner-checked path the hub uses, and the driver clears the auction the way the hosted loop does.
/// </summary>
public sealed class PlayerPositionTests : IClassFixture<SimulationTestHost>
{
    private readonly SimulationTestHost _host;

    public PlayerPositionTests(SimulationTestHost host)
    {
        _host = host;
    }

    [Fact]
    public async Task The_position_counts_the_allowances_the_company_wins_at_auction()
    {
        Guid id = StartRun(301);
        SimulationClockService driver = _host.CreateDriver();
        SimulationRegistry runs = Registry();
        HostedRun run = runs.Run(id);
        SignedIn delta = await _host.SignInAsync(TestRunFactory.DeltaCompany);
        PlayerSession session = Session(delta.AccountId);
        Unit unit = run.Simulation.FindUnit(1);
        decimal price = run.Simulation.Rules.AuctionFloorPrice;

        await driver.StartAsync(id);

        PlayerView before = await session.ForPlayerAsync(id) ?? throw new InvalidOperationException("no view");

        before.Position.Held.Should().BePositive("the year's free allocation has been granted");
        before.Position.Shortfall.Should().Be(
            Math.Max(0m, before.Position.ForecastEmissions - before.Position.Held),
            "the screen reads the shortfall the way the engine states it: what is still owed, never negative");

        // Section 1's auction window opens with the 20-minute year and closes at 2:15.
        _host.FakeTime.Advance(TimeSpan.FromMinutes(1));
        await driver.TickAsync(id);
        await runs.BidAtAuctionAsync(id, delta.Actor, unit.Id, 1, price, 20_000m);
        _host.FakeTime.Advance(TimeSpan.FromMinutes(2));
        await driver.TickAsync(id);

        Auction auction = run.Auctions.ForSection(1, 1);

        auction.IsCleared.Should().BeTrue("the clock cleared the section's auction");

        decimal won = auction.Results
            .SelectMany(result => result.Awards)
            .Where(award => ReferenceEquals(award.Bid.Unit.Company, unit.Company))
            .Sum(award => award.Volume);

        won.Should().BePositive("the bid was inside the collar and the auction was under-subscribed");

        PlayerView after = await session.ForPlayerAsync(id) ?? throw new InvalidOperationException("no view");

        after.Position.Held.Should().Be(before.Position.Held + won, "what the auction awarded is the company's own");
        after.Position.Shortfall.Should().Be(
            Math.Max(0m, before.Position.Shortfall - won),
            "and it comes off what is still owed, down to zero: a company already covered cannot owe less than nothing");
        after.Position.Position.Should().Be(before.Position.Position + won, "the same win lengthens the position");
        after.Position.FreeAllocation.Should().Be(before.Position.FreeAllocation, "the grant itself has not changed");

        HoldingView vintage1 = after.Holdings.Single(holding => holding.Product.Key == "Vintage 1");
        HoldingView beforeVintage1 = before.Holdings.Single(holding => holding.Product.Key == "Vintage 1");

        vintage1.Held.Should().Be(beforeVintage1.Held + won, "the holdings table shows the bought allowances too");
    }

    private SimulationRegistry Registry() => _host.Server.Services.GetRequiredService<SimulationRegistry>();

    private PlayerSession Session(int accountId) => new(
        Registry(),
        _host.Server.Services.GetRequiredService<IServiceScopeFactory>(),
        new FixedAuthenticationStateProvider(accountId));

    private Guid StartRun(int seed)
    {
        string json = TestRunFactory.ScenarioJson.Replace("\"seed\": 7", $"\"seed\": {seed}", StringComparison.Ordinal);

        return Registry().Start(ScenarioLoader.LoadJson(json, "player-position-test"));
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
