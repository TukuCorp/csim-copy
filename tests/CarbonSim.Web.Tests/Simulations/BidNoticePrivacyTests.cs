using System.Globalization;
using System.Security.Claims;
using CarbonSim.Engine.Scenarios;
using CarbonSim.Web.Player;
using CarbonSim.Web.Simulations;
using FluentAssertions;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;

namespace CarbonSim.Web.Tests.Simulations;

/// <summary>
/// Auction bids are sealed: what one company bid, and for how much, is the owner's business and the
/// trainer's. A notice that names a released bid therefore has an audience, and this checks it at the
/// host level, with two players signed in for two companies.
/// </summary>
public sealed class BidNoticePrivacyTests : IClassFixture<SimulationTestHost>
{
    private readonly SimulationTestHost _host;

    public BidNoticePrivacyTests(SimulationTestHost host)
    {
        _host = host;
    }

    [Fact]
    public async Task A_released_bid_notice_reaches_only_the_company_that_made_it()
    {
        Guid id = StartRun(401);
        SimulationClockService driver = _host.CreateDriver();
        SimulationRegistry runs = Registry();
        SignedIn delta = await _host.SignInAsync(TestRunFactory.DeltaCompany);
        SignedIn redRiver = await _host.SignInAsync(TestRunFactory.RedRiverCompany);

        await driver.StartAsync(id);

        // Delta bids inside the collar, then the trainer raises the floor above it.
        _host.FakeTime.Advance(TimeSpan.FromMinutes(1));
        await driver.TickAsync(id);
        await runs.BidAtAuctionAsync(id, delta.Actor, 1, 1, 120m, 10_000m);
        await driver.ChangeRulesAsync(id, 140m, null);

        PlayerView owner = await Session(delta.AccountId).ForPlayerAsync(id) ?? throw new InvalidOperationException("no view");
        PlayerView other = await Session(redRiver.AccountId).ForPlayerAsync(id) ?? throw new InvalidOperationException("no view");

        string notice = owner.Messages.Last().Text;

        notice.Should().Contain("10,000", "the owner is told which bid came off the book");
        notice.Should().Contain("120", "and at what price it was resting");

        other.Messages.Should().NotContain(
            line => line.Text.Contains("10,000", StringComparison.Ordinal),
            "a sealed bid is the owner's business, not the room's");
        other.Messages.Should().NotContain(line => line.Text.Contains("120.00", StringComparison.Ordinal));

        // The room's line says how many came off, and nothing about whose or at what price.
        string line = owner.Messages.First(message => message.Text.Contains("Rules changed", StringComparison.OrdinalIgnoreCase)).Text;

        line.Should().Contain("1 auction bids were released");
        line.Should().NotContain("10,000");
        line.Should().NotContain("120.00");
    }

    private SimulationRegistry Registry() => _host.Server.Services.GetRequiredService<SimulationRegistry>();

    private PlayerSession Session(int accountId) => new(
        Registry(),
        _host.Server.Services.GetRequiredService<IServiceScopeFactory>(),
        new FixedAuthenticationStateProvider(accountId));

    private Guid StartRun(int seed)
    {
        string json = TestRunFactory.ScenarioJson.Replace("seed\": 7", "seed\": " + seed.ToString(System.Globalization.CultureInfo.InvariantCulture), StringComparison.Ordinal);

        return Registry().Start(ScenarioLoader.LoadJson(json, "bid-notice-privacy-test"));
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
