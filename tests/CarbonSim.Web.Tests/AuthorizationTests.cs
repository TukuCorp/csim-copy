using System.Net;
using CarbonSim.Data.Accounts;
using FluentAssertions;

namespace CarbonSim.Web.Tests;

/// <summary>Who may reach the administrator surface, and what happens to everyone else.</summary>
public sealed class AuthorizationTests : IClassFixture<FakeRosterHost>
{
    private readonly FakeRosterHost _host;

    public AuthorizationTests(FakeRosterHost host)
    {
        _host = host;
    }

    [Fact]
    public async Task An_administrator_only_endpoint_turns_a_player_away()
    {
        using HttpClient client = _host.CreateClient();
        (await client.RegisterAsync("mere-player@example.com", FakeRosterHost.PowerCompany, _host.RegistrationPin))
            .StatusCode
            .Should()
            .Be(HttpStatusCode.Created);
        (await client.LoginAsync("mere-player@example.com")).StatusCode.Should().Be(HttpStatusCode.OK);

        HttpResponseMessage response = await client.GetAsync("/api/admin/accounts");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task An_administrator_only_endpoint_answers_the_administrator()
    {
        PlayerAccount admin = await _host.SeedAdministratorAsync("administrator@example.com");

        using HttpClient client = _host.CreateClient();
        (await client.LoginAsync(admin.Email)).StatusCode.Should().Be(HttpStatusCode.OK);

        HttpResponseMessage response = await client.GetAsync("/api/admin/accounts");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        string body = await response.Content.ReadAsStringAsync();
        body.Should().Contain(admin.Email);
        body.Should().Contain(nameof(AccountRole.Administrator));
    }

    [Fact]
    public async Task An_administrator_only_endpoint_asks_an_anonymous_caller_to_sign_in()
    {
        using HttpClient client = _host.CreateClient();

        HttpResponseMessage response = await client.GetAsync("/api/admin/accounts");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
