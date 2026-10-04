using System.Globalization;
using System.Security.Claims;
using CarbonSim.Web.Player;
using CarbonSim.Web.Simulations;
using FluentAssertions;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;

namespace CarbonSim.Web.Tests.Simulations;

/// <summary>
/// The load-bearing property behind the Phase 6 load test: the part of a player's view that does
/// not depend on which company is asking - the leaderboard, the system report, the top of every
/// book - is built once per change and shared, so a bigger room does not multiply that work.
/// </summary>
public sealed class SharedViewCacheTests : IClassFixture<SimulationTestHost>
{
    private readonly SimulationTestHost _host;

    public SharedViewCacheTests(SimulationTestHost host)
    {
        _host = host;
    }

    [Fact]
    public async Task Two_players_on_one_run_build_the_shared_part_of_the_view_once()
    {
        SimulationRegistry runs = _host.Server.Services.GetRequiredService<SimulationRegistry>();
        Guid id = runs.Start(TestRunFactory.Build());
        SimulationClockService driver = _host.CreateDriver();
        await driver.StartAsync(id);

        SignedIn delta = await _host.SignInAsync(TestRunFactory.DeltaCompany);
        SignedIn redRiver = await _host.SignInAsync(TestRunFactory.RedRiverCompany);
        PlayerSession deltaSession = Session(delta.AccountId);
        PlayerSession redRiverSession = Session(redRiver.AccountId);
        HostedRun run = runs.Run(id);

        PlayerView? deltaNullable = await deltaSession.ForPlayerAsync(id);
        deltaNullable.Should().NotBeNull();
        PlayerView deltaView = deltaNullable!;
        int afterDelta = run.SharedBuilds;
        PlayerView? redRiverNullable = await redRiverSession.ForPlayerAsync(id);
        redRiverNullable.Should().NotBeNull();
        PlayerView redRiverView = redRiverNullable!;
        int afterRedRiver = run.SharedBuilds;

        // The second player only pays for their own slice - one company-keyed value - because
        // everything run-wide was already built for the first.
        (afterRedRiver - afterDelta).Should().Be(1);

        // The one shared ranking is still filtered per player, so each sees the flag on themselves.
        deltaView.Leaderboard.Should().ContainSingle(row => row.IsMine).Which.Company.Should().Be(TestRunFactory.DeltaCompany);
        redRiverView.Leaderboard.Should().ContainSingle(row => row.IsMine).Which.Company.Should().Be(TestRunFactory.RedRiverCompany);

        // A tick is a change, so the cache is thrown away and the next read rebuilds the shared part.
        await driver.TickAsync(id);
        await deltaSession.ForPlayerAsync(id);
        (run.SharedBuilds - afterRedRiver).Should().BeGreaterThan(1);
    }

    private PlayerSession Session(int accountId)
    {
        return new PlayerSession(
            _host.Server.Services.GetRequiredService<SimulationRegistry>(),
            _host.Server.Services.GetRequiredService<IServiceScopeFactory>(),
            new FixedAuthenticationStateProvider(accountId));
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
