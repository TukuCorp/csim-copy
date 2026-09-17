using System.Net.Http.Json;
using System.Text.RegularExpressions;
using CarbonSim.Data.Accounts;
using CarbonSim.Web.Accounts;
using CarbonSim.Web.Contracts;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace CarbonSim.Web.Tests;

/// <summary>
/// The calls these tests make and the peeks they take at the host: everything that would otherwise
/// be repeated in every test class.
/// </summary>
internal static class HostQueries
{
    public const string Password = "mekong-delta-2024";

    public const string NewPassword = "red-river-2025";

    private static readonly Regex CodePattern = new(@"\b(\d{6})\b", RegexOptions.CultureInvariant);

    /// <summary>Every account the host holds, read through the store rather than the API.</summary>
    public static async Task<IReadOnlyList<PlayerAccount>> ListAccountsAsync(this CarbonSimWebHost host)
    {
        ArgumentNullException.ThrowIfNull(host);

        using IServiceScope scope = host.Services.CreateScope();
        IAccountStore store = scope.ServiceProvider.GetRequiredService<IAccountStore>();

        return await store.ListAsync();
    }

    public static async Task<PlayerAccount?> FindAccountAsync(this CarbonSimWebHost host, string email)
    {
        ArgumentNullException.ThrowIfNull(host);

        using IServiceScope scope = host.Services.CreateScope();
        IAccountStore store = scope.ServiceProvider.GetRequiredService<IAccountStore>();

        return await store.FindAsync(email);
    }

    /// <summary>
    /// Creates the one account no request may create. An administrator is granted the role by the
    /// host, so a test seeds one the way a deployment would.
    /// </summary>
    public static async Task<PlayerAccount> SeedAdministratorAsync(this CarbonSimWebHost host, string email)
    {
        ArgumentNullException.ThrowIfNull(host);

        using IServiceScope scope = host.Services.CreateScope();
        IAccountStore store = scope.ServiceProvider.GetRequiredService<IAccountStore>();
        IPasswordHasher<PlayerAccount> hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher<PlayerAccount>>();

        string passwordHash = hasher.HashPassword(new PlayerAccount(), Password);

        return await store.CreateAsync(email, "Exercise Administrator", passwordHash, AccountRole.Administrator)
            ?? throw new InvalidOperationException($"The administrator account '{email}' could not be created.");
    }

    public static Task<HttpResponseMessage> RegisterAsync(
        this HttpClient client,
        string email,
        string company,
        string registrationPin,
        string password = Password,
        string displayName = "Test Player")
    {
        ArgumentNullException.ThrowIfNull(client);

        return client.PostAsJsonAsync(
            "/api/accounts/register",
            new RegisterAccountRequest
            {
                Email = email,
                DisplayName = displayName,
                Password = password,
                Company = company,
                RegistrationPin = registrationPin,
            });
    }

    public static Task<HttpResponseMessage> LoginAsync(
        this HttpClient client,
        string email,
        string password = Password)
    {
        ArgumentNullException.ThrowIfNull(client);

        return client.PostAsJsonAsync("/api/accounts/login", new LoginRequest { Email = email, Password = password });
    }

    public static Task<HttpResponseMessage> RequestPasswordResetAsync(this HttpClient client, string email)
    {
        ArgumentNullException.ThrowIfNull(client);

        return client.PostAsJsonAsync("/api/accounts/password-reset/request", new PasswordResetRequest { Email = email });
    }

    public static Task<HttpResponseMessage> ConfirmPasswordResetAsync(
        this HttpClient client,
        string email,
        string code,
        string newPassword = NewPassword)
    {
        ArgumentNullException.ThrowIfNull(client);

        return client.PostAsJsonAsync(
            "/api/accounts/password-reset/confirm",
            new ConfirmPasswordResetRequest { Email = email, Code = code, NewPassword = newPassword });
    }

    /// <summary>The code out of the newest message sent to this address.</summary>
    public static string ResetCodeFor(this InMemoryEmailSender sender, string email)
    {
        ArgumentNullException.ThrowIfNull(sender);

        EmailMessage message = sender
            .SentMessages
            .LastOrDefault(candidate => string.Equals(candidate.To, email, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException($"No message was sent to '{email}'.");

        Match match = CodePattern.Match(message.Body);

        return match.Success
            ? match.Groups[1].Value
            : throw new InvalidOperationException($"The message sent to '{email}' carries no six-digit code.");
    }

    /// <summary>Whether the response handed the browser an authentication cookie.</summary>
    public static bool SetsAuthenticationCookie(this HttpResponseMessage response)
    {
        ArgumentNullException.ThrowIfNull(response);

        return response
            .Headers
            .TryGetValues("Set-Cookie", out IEnumerable<string>? cookies)
            && cookies.Any(cookie => cookie.StartsWith("carbonsim.auth=", StringComparison.Ordinal));
    }
}
