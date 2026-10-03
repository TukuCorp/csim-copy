using System.Net;
using CarbonSim.Data.Accounts;
using CarbonSim.Web.Simulations;
using CarbonSim.Web.Tests.Simulations;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace CarbonSim.Web.Tests.Admin;

/// <summary>
/// The console's server surface: the exercises list and the CSV exports. They sit behind the
/// administrator policy, and the exports are answered from the same run detail the screens read.
/// </summary>
public sealed class AdminEndpointTests : IClassFixture<FakeRosterHost>, IAsyncLifetime
{
    private readonly FakeRosterHost _host;
    private Guid _runId;

    public AdminEndpointTests(FakeRosterHost host)
    {
        _host = host;
    }

    public Task InitializeAsync()
    {
        SimulationRegistry runs = _host.Server.Services.GetRequiredService<SimulationRegistry>();
        _runId = runs.Start(TestRunFactory.Build());

        return Task.CompletedTask;
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task An_administrator_can_list_the_exercises()
    {
        PlayerAccount admin = await _host.SeedAdministratorAsync("admin-runs@example.com");

        using HttpClient client = _host.CreateClient();
        (await client.LoginAsync(admin.Email)).StatusCode.Should().Be(HttpStatusCode.OK);

        HttpResponseMessage response = await client.GetAsync("/api/admin/runs");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync()).Should().Contain("Hub test run");
    }

    [Fact]
    public async Task An_administrator_can_export_a_report_as_csv()
    {
        PlayerAccount admin = await _host.SeedAdministratorAsync("admin-csv@example.com");

        using HttpClient client = _host.CreateClient();
        (await client.LoginAsync(admin.Email)).StatusCode.Should().Be(HttpStatusCode.OK);

        HttpResponseMessage response = await client.GetAsync($"/api/admin/runs/{_runId:D}/reports/system.csv");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should().Be("text/csv");

        string body = await response.Content.ReadAsStringAsync();
        body.Should().StartWith("Metric,");
        body.Should().Contain("Cap");
    }

    [Fact]
    public async Task An_unknown_report_is_not_found()
    {
        PlayerAccount admin = await _host.SeedAdministratorAsync("admin-unknown@example.com");

        using HttpClient client = _host.CreateClient();
        (await client.LoginAsync(admin.Email)).StatusCode.Should().Be(HttpStatusCode.OK);

        HttpResponseMessage response = await client.GetAsync($"/api/admin/runs/{_runId:D}/reports/nonsense.csv");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task A_player_is_turned_away_from_an_export()
    {
        using HttpClient client = _host.CreateClient();
        (await client.RegisterAsync("player-csv@example.com", FakeRosterHost.PowerCompany, _host.RegistrationPin))
            .StatusCode
            .Should()
            .Be(HttpStatusCode.Created);
        (await client.LoginAsync("player-csv@example.com")).StatusCode.Should().Be(HttpStatusCode.OK);

        HttpResponseMessage response = await client.GetAsync($"/api/admin/runs/{_runId:D}/reports/system.csv");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task An_anonymous_caller_is_asked_to_sign_in()
    {
        using HttpClient client = _host.CreateClient();

        HttpResponseMessage response = await client.GetAsync($"/api/admin/runs/{_runId:D}/reports/system.csv");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
