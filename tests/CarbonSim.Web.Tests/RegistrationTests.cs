using System.Net;
using System.Net.Http.Json;
using CarbonSim.Data.Accounts;
using CarbonSim.Web.Contracts;
using FluentAssertions;

namespace CarbonSim.Web.Tests;

/// <summary>Claiming a company: the PIN gates it, the roster decides what is on offer, nobody may ask for a role.</summary>
public sealed class RegistrationTests : IClassFixture<FakeRosterHost>
{
    private readonly FakeRosterHost _host;

    public RegistrationTests(FakeRosterHost host)
    {
        _host = host;
    }

    [Fact]
    public async Task Registration_with_a_wrong_pin_is_refused_and_creates_no_account()
    {
        using HttpClient client = _host.CreateClient();

        HttpResponseMessage response = await client.RegisterAsync(
            "wrong-pin@example.com",
            FakeRosterHost.PowerCompany,
            registrationPin: "not-the-administrator-pin");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        ErrorResponse? error = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        error.Should().NotBeNull();
        error!.Message.Should().Contain("PIN");

        (await _host.FindAccountAsync("wrong-pin@example.com")).Should().BeNull("a refused registration creates nothing");
    }

    [Fact]
    public async Task Registration_with_the_right_pin_claims_the_chosen_company()
    {
        using HttpClient client = _host.CreateClient();

        HttpResponseMessage response = await client.RegisterAsync(
            "player@example.com",
            FakeRosterHost.GridCompany,
            _host.RegistrationPin);

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        AccountResponse? account = await response.Content.ReadFromJsonAsync<AccountResponse>();
        account.Should().NotBeNull();
        account!.Email.Should().Be("player@example.com");
        account.Company.Should().Be(FakeRosterHost.GridCompany);
        account.Role.Should().Be(nameof(AccountRole.Player));

        PlayerAccount? stored = await _host.FindAccountAsync("player@example.com");
        stored.Should().NotBeNull();
        stored!.CompanyName.Should().Be(FakeRosterHost.GridCompany);
        stored.PasswordHash.Should().NotBe(HostQueries.Password, "the password is stored hashed, never as itself");
    }

    [Fact]
    public async Task Registration_refuses_a_company_another_account_already_claimed()
    {
        using HttpClient first = _host.CreateClient();
        (await first.RegisterAsync("first@example.com", FakeRosterHost.CementCompany, _host.RegistrationPin))
            .StatusCode
            .Should()
            .Be(HttpStatusCode.Created);

        using HttpClient second = _host.CreateClient();
        HttpResponseMessage response = await second.RegisterAsync(
            "second@example.com",
            FakeRosterHost.CementCompany,
            _host.RegistrationPin);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await _host.FindAccountAsync("second@example.com")).Should().BeNull();

        IReadOnlyList<PlayerAccount> accounts = await _host.ListAccountsAsync();
        accounts.Should().ContainSingle(account => account.CompanyName == FakeRosterHost.CementCompany);
    }

    [Fact]
    public async Task Registration_refuses_a_company_that_is_not_on_offer()
    {
        using HttpClient client = _host.CreateClient();

        HttpResponseMessage response = await client.RegisterAsync(
            "outsider@example.com",
            "Somebody Else's Company",
            _host.RegistrationPin);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await _host.FindAccountAsync("outsider@example.com")).Should().BeNull();
    }

    [Fact]
    public async Task A_registration_cannot_ask_for_the_administrator_role()
    {
        using HttpClient client = _host.CreateClient();

        // The role is not part of the contract; a caller that sends one anyway is given a player.
        HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/accounts/register",
            new
            {
                email = "would-be-admin@example.com",
                displayName = "Would Be Administrator",
                password = HostQueries.Password,
                company = FakeRosterHost.SteelCompany,
                registrationPin = _host.RegistrationPin,
                role = nameof(AccountRole.Administrator),
            });

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        AccountResponse? account = await response.Content.ReadFromJsonAsync<AccountResponse>();
        account!.Role.Should().Be(nameof(AccountRole.Player));

        PlayerAccount? stored = await _host.FindAccountAsync("would-be-admin@example.com");
        stored!.Role.Should().Be(AccountRole.Player);
    }

    [Fact]
    public async Task Registration_is_closed_when_the_host_has_no_pin_configured()
    {
        using NoPinHost host = new();
        using HttpClient client = host.CreateClient();

        HttpResponseMessage response = await client.RegisterAsync(
            "anyone@example.com",
            FakeRosterHost.PowerCompany,
            registrationPin: string.Empty);

        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        (await host.FindAccountAsync("anyone@example.com")).Should().BeNull();
    }
}

/// <summary>A host that was never given a registration PIN, which is how a deployment fails closed.</summary>
public sealed class NoPinHost : FakeRosterHost
{
    public override string RegistrationPin => string.Empty;
}
