using System.Net;
using System.Net.Http.Json;
using CarbonSim.Data.Accounts;
using CarbonSim.Web.Contracts;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace CarbonSim.Web.Tests;

/// <summary>Signing in, signing out, and what an anonymous caller is told at a protected endpoint.</summary>
public sealed class LoginTests : IClassFixture<FakeRosterHost>
{
    private readonly FakeRosterHost _host;

    public LoginTests(FakeRosterHost host)
    {
        _host = host;
    }

    [Fact]
    public async Task Login_with_the_right_password_sets_the_authentication_cookie()
    {
        using HttpClient client = _host.CreateClient();
        await RegisterAsync(client, "signs-in@example.com", FakeRosterHost.PowerCompany);

        HttpResponseMessage response = await client.LoginAsync("signs-in@example.com");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.SetsAuthenticationCookie().Should().BeTrue();

        AccountResponse? account = await response.Content.ReadFromJsonAsync<AccountResponse>();
        account!.Company.Should().Be(FakeRosterHost.PowerCompany);
    }

    [Fact]
    public async Task Login_with_a_wrong_password_is_refused_and_sets_no_cookie()
    {
        using HttpClient client = _host.CreateClient();
        await RegisterAsync(client, "wrong-password@example.com", FakeRosterHost.CementCompany);

        HttpResponseMessage response = await client.LoginAsync("wrong-password@example.com", "not-the-password");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        response.SetsAuthenticationCookie().Should().BeFalse();

        // The refusal says exactly the same thing for an address nobody registered, so it cannot be
        // used to find out who has an account.
        HttpResponseMessage stranger = await client.LoginAsync("nobody@example.com");

        stranger.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await stranger.Content.ReadFromJsonAsync<ErrorResponse>())!.Message
            .Should()
            .Be((await response.Content.ReadFromJsonAsync<ErrorResponse>())!.Message);
    }

    [Fact]
    public async Task A_protected_endpoint_asks_an_anonymous_caller_to_sign_in()
    {
        using HttpClient client = _host.CreateClient();

        HttpResponseMessage response = await client.GetAsync("/api/accounts/me");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task A_signed_in_caller_sees_their_own_account()
    {
        using HttpClient client = _host.CreateClient();
        await RegisterAsync(client, "sees-me@example.com", FakeRosterHost.SteelCompany);
        (await client.LoginAsync("sees-me@example.com")).StatusCode.Should().Be(HttpStatusCode.OK);

        HttpResponseMessage response = await client.GetAsync("/api/accounts/me");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        AccountResponse? account = await response.Content.ReadFromJsonAsync<AccountResponse>();
        account!.Email.Should().Be("sees-me@example.com");
        account.Company.Should().Be(FakeRosterHost.SteelCompany);
    }

    [Fact]
    public async Task Logout_ends_the_session()
    {
        using HttpClient client = _host.CreateClient();
        await RegisterAsync(client, "signs-out@example.com", FakeRosterHost.GridCompany);
        (await client.LoginAsync("signs-out@example.com")).StatusCode.Should().Be(HttpStatusCode.OK);

        HttpResponseMessage response = await client.PostAsync("/api/accounts/logout", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await client.GetAsync("/api/accounts/me")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task A_password_hashed_with_older_parameters_is_stored_again_at_login()
    {
        string email = "older-parameters@example.com";
        IPasswordHasher<PlayerAccount> older = new PasswordHasher<PlayerAccount>(
            Options.Create(new PasswordHasherOptions { IterationCount = 1_000 }));
        string originallyStored;

        using (IServiceScope scope = _host.Services.CreateScope())
        {
            IAccountStore store = scope.ServiceProvider.GetRequiredService<IAccountStore>();

            PlayerAccount seeded = await store.CreateAsync(
                email,
                "Test Player",
                older.HashPassword(new PlayerAccount(), HostQueries.Password),
                AccountRole.Player)
                ?? throw new InvalidOperationException($"The account '{email}' could not be created.");

            originallyStored = seeded.PasswordHash;
        }

        using HttpClient client = _host.CreateClient();

        (await client.LoginAsync(email)).StatusCode.Should().Be(HttpStatusCode.OK, "the older hash still verifies");

        PlayerAccount? after = await _host.FindAccountAsync(email);
        after!.PasswordHash.Should().NotBe(originallyStored, "what the hasher can protect better now is stored again");
    }

    private async Task RegisterAsync(HttpClient client, string email, string company)
    {
        HttpResponseMessage response = await client.RegisterAsync(email, company, _host.RegistrationPin);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }
}
