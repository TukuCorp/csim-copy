using System.Globalization;
using System.Reflection;
using System.Security.Claims;
using CarbonSim.Data.Accounts;
using CarbonSim.Engine.Domain;
using CarbonSim.Engine.Reporting;
using CarbonSim.Engine.Scenarios;
using CarbonSim.Web;
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
/// The system totals a screen reads must be the report's own figures for the window it is about:
/// this year alone, and every year that has run. Both the player's System Info page and the
/// console read the same figures, so this pins the one builder they share before it moves.
/// </summary>
public sealed class SystemTotalsMappingTests : IClassFixture<SimulationTestHost>
{
    private const int YearMinutes = 20;

    private readonly SimulationTestHost _host;

    public SystemTotalsMappingTests(SimulationTestHost host) => _host = host;

    [Fact]
    public async Task Both_sessions_show_the_report_totals_for_this_year_and_to_date()
    {
        Guid id = StartRun(401);
        SimulationRegistry runs = Registry();
        HostedRun run = runs.Run(id);
        SignedIn delta = await _host.SignInAsync(TestRunFactory.DeltaCompany);
        SimulationClockService driver = _host.CreateDriver();

        // Play a whole first year so "this year" and "to date" are two different windows.
        await driver.StartAsync(id);
        _host.FakeTime.Advance(TimeSpan.FromMinutes(YearMinutes));
        await driver.TickAsync(id);
        await driver.EndYearAsync(id);
        await driver.BeginNextYearAsync(id);

        Simulation simulation = run.Simulation;
        int year = Math.Max(1, simulation.CurrentYear);
        SystemReport report = SystemReport.For(simulation, year);

        PlayerView view = await Player(delta.AccountId).ForPlayerAsync(id)
            ?? throw new InvalidOperationException("no player view");
        AdminRunDetail detail = (await Console(1).SnapshotAsync(id)).Run
            ?? throw new InvalidOperationException("no console run");

        Mirrors(report.ThisYear, view.System.ThisYear);
        Mirrors(report.ToDate, view.System.ToDate);
        Mirrors(report.ThisYear, detail.ThisYear);
        Mirrors(report.ToDate, detail.ToDate);
    }

    /// <summary>Every figure the totals publish has to be the report's, not re-derived by a screen.</summary>
    private static void Mirrors(SystemTotals report, object shown)
    {
        foreach (PropertyInfo property in typeof(SystemTotals).GetProperties())
        {
            object? fromShown = shown.GetType().GetProperty(property.Name)?.GetValue(shown);

            fromShown.Should().Be(property.GetValue(report), $"{property.Name} comes from the report");
        }
    }

    private SimulationRegistry Registry() => _host.Server.Services.GetRequiredService<SimulationRegistry>();

    private PlayerSession Player(int accountId) => new(
        Registry(),
        _host.Server.Services.GetRequiredService<IServiceScopeFactory>(),
        new FixedAuthenticationStateProvider(accountId, AccountRole.Player));

    private AdminSession Console(int accountId)
    {
        IServiceProvider services = _host.Server.Services;

        return new AdminSession(
            services.GetRequiredService<SimulationRegistry>(),
            _host.CreateDriver(),
            services.GetRequiredService<IServiceScopeFactory>(),
            new FixedAuthenticationStateProvider(accountId, AccountRole.Administrator),
            services.GetRequiredService<RegistrationPolicy>(),
            services.GetRequiredService<CarbonSimHostOptions>(),
            services.GetRequiredService<IHostEnvironment>());
    }

    private Guid StartRun(int seed)
    {
        string json = TestRunFactory.ScenarioJson.Replace("\"seed\": 7", $"\"seed\": {seed}", StringComparison.Ordinal);

        return Registry().Start(ScenarioLoader.LoadJson(json, "system-totals-test"));
    }

    private sealed class FixedAuthenticationStateProvider : AuthenticationStateProvider
    {
        private readonly ClaimsPrincipal _user;

        public FixedAuthenticationStateProvider(int accountId, AccountRole role)
        {
            _user = new ClaimsPrincipal(new ClaimsIdentity(
                [
                    new Claim(ClaimTypes.NameIdentifier, accountId.ToString(CultureInfo.InvariantCulture)),
                    new Claim(ClaimTypes.Role, role.ToString()),
                ],
                authenticationType: "test"));
        }

        public override Task<AuthenticationState> GetAuthenticationStateAsync() =>
            Task.FromResult(new AuthenticationState(_user));
    }
}

