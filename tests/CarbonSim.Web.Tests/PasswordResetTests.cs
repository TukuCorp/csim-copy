using System.Net;
using FluentAssertions;

namespace CarbonSim.Web.Tests;

/// <summary>
/// Password recovery: an emailed code that expires, works once, and stops working after a few
/// wrong tries; and a reset request that never reveals whether an address has an account.
/// </summary>
public sealed class PasswordResetTests : IClassFixture<PasswordResetHost>
{
    private readonly PasswordResetHost _host;

    public PasswordResetTests(PasswordResetHost host)
    {
        _host = host;
    }

    [Fact]
    public async Task A_reset_request_emails_a_code_to_the_address()
    {
        using HttpClient client = _host.CreateClient();
        await RegisterAsync(client, "reset-me@example.com", FakeRosterHost.PortCompany);

        HttpResponseMessage response = await client.RequestPasswordResetAsync("reset-me@example.com");

        response.StatusCode.Should().Be(HttpStatusCode.Accepted);

        string code = _host.Emails.ResetCodeFor("reset-me@example.com");
        code.Should().HaveLength(6).And.MatchRegex("^[0-9]{6}$");
    }

    [Fact]
    public async Task A_reset_request_for_an_unknown_address_looks_the_same_and_sends_nothing()
    {
        using HttpClient client = _host.CreateClient();
        await RegisterAsync(client, "known@example.com", FakeRosterHost.PaperCompany);

        HttpResponseMessage known = await client.RequestPasswordResetAsync("known@example.com");

        _host.Emails.Clear();

        HttpResponseMessage unknown = await client.RequestPasswordResetAsync("nobody@example.com");

        unknown.StatusCode.Should().Be(known.StatusCode);
        (await unknown.Content.ReadAsStringAsync())
            .Should()
            .Be(await known.Content.ReadAsStringAsync(), "the reply must not tell a stranger which addresses exist");
        _host.Emails.SentMessages.Should().BeEmpty();
    }

    [Fact]
    public async Task The_code_the_email_carries_changes_the_password()
    {
        using HttpClient client = _host.CreateClient();
        await RegisterAsync(client, "forgets@example.com", FakeRosterHost.SteelCompany);
        await client.RequestPasswordResetAsync("forgets@example.com");
        string code = _host.Emails.ResetCodeFor("forgets@example.com");

        HttpResponseMessage response = await client.ConfirmPasswordResetAsync("forgets@example.com", code);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        using HttpClient again = _host.CreateClient();
        (await again.LoginAsync("forgets@example.com", HostQueries.NewPassword)).StatusCode
            .Should()
            .Be(HttpStatusCode.OK);
        (await again.LoginAsync("forgets@example.com", HostQueries.Password)).StatusCode
            .Should()
            .Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task A_code_can_be_used_only_once()
    {
        using HttpClient client = _host.CreateClient();
        await RegisterAsync(client, "once@example.com", FakeRosterHost.GridCompany);
        await client.RequestPasswordResetAsync("once@example.com");
        string code = _host.Emails.ResetCodeFor("once@example.com");
        (await client.ConfirmPasswordResetAsync("once@example.com", code)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        HttpResponseMessage replay = await client.ConfirmPasswordResetAsync(
            "once@example.com",
            code,
            newPassword: HostQueries.Password);

        replay.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await client.LoginAsync("once@example.com", HostQueries.Password)).StatusCode
            .Should()
            .Be(HttpStatusCode.Unauthorized, "the second use must not have changed anything");
    }

    [Fact]
    public async Task Too_many_wrong_codes_close_the_reset()
    {
        using HttpClient client = _host.CreateClient();
        await RegisterAsync(client, "fumbles@example.com", FakeRosterHost.PowerCompany);
        await client.RequestPasswordResetAsync("fumbles@example.com");
        string code = _host.Emails.ResetCodeFor("fumbles@example.com");
        string wrongCode = code == "000000" ? "000001" : "000000";

        for (int attempt = 0; attempt < _host.ResetCodeMaxAttempts; attempt++)
        {
            (await client.ConfirmPasswordResetAsync("fumbles@example.com", wrongCode)).StatusCode
                .Should()
                .Be(HttpStatusCode.BadRequest);
        }

        // Even the code that was right no longer opens anything: the reset is closed.
        (await client.ConfirmPasswordResetAsync("fumbles@example.com", code)).StatusCode
            .Should()
            .Be(HttpStatusCode.BadRequest);
        (await client.LoginAsync("fumbles@example.com", HostQueries.NewPassword)).StatusCode
            .Should()
            .Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task An_expired_code_is_refused()
    {
        using ExpiredCodeHost host = new();
        using HttpClient client = host.CreateClient();
        (await client.RegisterAsync("late@example.com", FakeRosterHost.CementCompany, host.RegistrationPin))
            .StatusCode
            .Should()
            .Be(HttpStatusCode.Created);
        await client.RequestPasswordResetAsync("late@example.com");
        string code = host.Emails.ResetCodeFor("late@example.com");

        HttpResponseMessage response = await client.ConfirmPasswordResetAsync("late@example.com", code);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await client.LoginAsync("late@example.com", HostQueries.NewPassword)).StatusCode
            .Should()
            .Be(HttpStatusCode.Unauthorized);
    }

    private async Task RegisterAsync(HttpClient client, string email, string company)
    {
        HttpResponseMessage response = await client.RegisterAsync(email, company, _host.RegistrationPin);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }
}

/// <summary>A host whose reset codes are handed out already expired.</summary>
public sealed class PasswordResetHost : FakeRosterHost
{
    public override int ResetCodeMaxAttempts => 3;
}

/// <summary>A host that gives its codes no lifetime at all, which is the shortest way to an expired one.</summary>
public sealed class ExpiredCodeHost : FakeRosterHost
{
    public override int ResetCodeLifetimeMinutes => 0;
}
