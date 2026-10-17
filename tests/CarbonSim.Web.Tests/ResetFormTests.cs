using System.Net;
using System.Text.RegularExpressions;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;

namespace CarbonSim.Web.Tests;

/// <summary>
/// A tester who forgot their password resets it from the sign-in page: ask for a code, read it
/// from the message, and choose a new password. These drive the page's own forms over HTTP, with
/// the antiforgery token the page hands out, against the real host and its account store.
/// </summary>
public sealed partial class ResetFormTests : IClassFixture<FakeRosterHost>
{
    private const string ChosenPassword = "a-brand-new-password";

    private readonly FakeRosterHost _host;

    public ResetFormTests(FakeRosterHost host)
    {
        _host = host;
    }

    [Fact]
    public async Task The_sign_in_page_resets_a_password_with_the_emailed_code()
    {
        const string email = "forgot-it@example.com";
        using HttpClient client = Client();
        (await client.RegisterAsync(email, FakeRosterHost.PortCompany, _host.RegistrationPin)).StatusCode.Should().Be(HttpStatusCode.Created);

        string token = await TokenAsync(client);
        HttpResponseMessage requested = await PostFormAsync(client, "/api/accounts/reset-request-form", token, ("email", email));
        requested.StatusCode.Should().Be(HttpStatusCode.Redirect);
        requested.Headers.Location!.OriginalString.Should().StartWith("/sign-in?status=");

        string code = _host.Emails.ResetCodeFor(email);
        HttpResponseMessage confirmed = await PostFormAsync(
            client,
            "/api/accounts/reset-confirm-form",
            token,
            ("email", email),
            ("code", code),
            ("newPassword", ChosenPassword));

        confirmed.StatusCode.Should().Be(HttpStatusCode.Redirect);
        confirmed.Headers.Location!.OriginalString.Should().StartWith("/sign-in?status=");
        (await client.LoginAsync(email, ChosenPassword)).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task A_wrong_code_is_refused_on_the_page_and_the_old_password_still_works()
    {
        const string email = "wrong-code@example.com";
        using HttpClient client = Client();
        (await client.RegisterAsync(email, FakeRosterHost.PaperCompany, _host.RegistrationPin)).StatusCode.Should().Be(HttpStatusCode.Created);

        string token = await TokenAsync(client);
        await PostFormAsync(client, "/api/accounts/reset-request-form", token, ("email", email));
        string code = _host.Emails.ResetCodeFor(email);
        string wrong = code == "000000" ? "111111" : "000000";

        HttpResponseMessage refused = await PostFormAsync(
            client,
            "/api/accounts/reset-confirm-form",
            token,
            ("email", email),
            ("code", wrong),
            ("newPassword", ChosenPassword));

        refused.Headers.Location!.OriginalString.Should().StartWith("/sign-in?error=");
        (await client.LoginAsync(email)).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task The_page_says_what_happened_after_a_reset_step()
    {
        using HttpClient client = Client();

        string page = await client.GetStringAsync("/sign-in?status=Check+your+email");

        page.Should().Contain("id=\"reset-request-form\"");
        page.Should().Contain("id=\"reset-confirm-form\"");
        page.Should().Contain("notice-status").And.Contain("Check your email");
    }

    private HttpClient Client() => _host.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    /// <summary>The antiforgery token the sign-in page embeds; the client keeps the matching cookie.</summary>
    private static async Task<string> TokenAsync(HttpClient client)
    {
        string page = await client.GetStringAsync("/sign-in");
        Match match = TokenPattern().Match(page);

        match.Success.Should().BeTrue("the sign-in page carries an antiforgery token");

        return WebUtility.HtmlDecode(match.Groups[1].Value);
    }

    private static Task<HttpResponseMessage> PostFormAsync(HttpClient client, string path, string token, params (string Name, string Value)[] fields)
    {
        List<KeyValuePair<string, string>> form = [new("__RequestVerificationToken", token)];
        form.AddRange(fields.Select(field => new KeyValuePair<string, string>(field.Name, field.Value)));

        return client.PostAsync(path, new FormUrlEncodedContent(form));
    }

    [GeneratedRegex("name=\"__RequestVerificationToken\" value=\"([^\"]+)\"")]
    private static partial Regex TokenPattern();
}
