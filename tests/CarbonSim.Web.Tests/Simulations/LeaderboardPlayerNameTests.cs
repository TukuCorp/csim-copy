using System.Security.Claims;
using CarbonSim.Data.Accounts;
using CarbonSim.Web.Accounts;
using CarbonSim.Web.Admin;
using CarbonSim.Web.Player;
using CarbonSim.Web.Simulations;
using FluentAssertions;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace CarbonSim.Web.Tests.Simulations;

/// <summary>
/// The leaderboard names who plays each unit ("Unit name (Player)" in the original), so a trainer
/// can tell the room's people apart. The run itself only knows that a company is played by a
/// human; the name is the account's, so the host has to bring it to both leaderboards.
/// </summary>
public sealed class LeaderboardPlayerNameTests : IClassFixture<SimulationTestHost>
{
    private readonly SimulationTestHost _host;

    public LeaderboardPlayerNameTests(SimulationTestHost host) => _host = host;

    [Fact]
    public async Task Both_leaderboards_name_the_player_who_registered_for_a_company()
    {
        Guid id = Registry().Start(TestRunFactory.Build());

        using (HttpClient client = _host.CreateClient())
        {
            using HttpResponseMessage registered = await client.RegisterAsync(
                "alpha-leaderboard@example.com",
                TestRunFactory.DeltaCompany,
                _host.RegistrationPin,
                displayName: "Alpha");
            registered.IsSuccessStatusCode.Should().BeTrue();
        }

        SignedIn redRiver = await _host.SignInAsync(TestRunFactory.RedRiverCompany);

        AdminRunDetail detail = (await Console().SnapshotAsync(id)).Run
            ?? throw new InvalidOperationException("no console run");
        PlayerView view = await Player(redRiver.AccountId).ForPlayerAsync(id)
            ?? throw new InvalidOperationException("no player view");

        detail.Leaderboard.Single(row => row.Company == TestRunFactory.DeltaCompany).Player.Should().Be("Alpha");
        view.Leaderboard.Single(row => row.Company == TestRunFactory.DeltaCompany).Player.Should().Be("Alpha");
    }

    private SimulationRegistry Registry() => _host.Server.Services.GetRequiredService<SimulationRegistry>();

    private PlayerSession Player(int accountId) => new(
        Registry(),
        _host.Server.Services.GetRequiredService<IServiceScopeFactory>(),
        new FixedAuthenticationStateProvider(accountId, AccountRole.Player));

    private AdminSession Console()
    {
        IServiceProvider services = _host.Server.Services;

        return new AdminSession(
            services.GetRequiredService<SimulationRegistry>(),
            _host.CreateDriver(),
            services.GetRequiredService<IServiceScopeFactory>(),
            new FixedAuthenticationStateProvider(1, AccountRole.Administrator),
            services.GetRequiredService<RegistrationPolicy>(),
            services.GetRequiredService<CarbonSimHostOptions>(),
            services.GetRequiredService<IHostEnvironment>());
    }

    private sealed class FixedAuthenticationStateProvider(int accountId, AccountRole role) : AuthenticationStateProvider
    {
        public override Task<AuthenticationState> GetAuthenticationStateAsync() =>
            Task.FromResult(new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity(
                [
                    new Claim(ClaimTypes.NameIdentifier, accountId.ToString(System.Globalization.CultureInfo.InvariantCulture)),
                    new Claim(ClaimTypes.Role, role.ToString()),
                ],
                "test"))));
    }
}
