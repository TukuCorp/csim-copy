using CarbonSim.Data.Accounts;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CarbonSim.Web.Admin;

/// <summary>
/// Gives the host an administrator to sign in with. A player account is created by a player
/// claiming a company, but the administrator role is never claimable, so a deployment that names
/// an address and password in its configuration gets one account of that role at boot. Nothing
/// else touches an existing account, and a host that names no administrator simply has none.
/// </summary>
public sealed class AdminAccountSeeder : IHostedService
{
    private readonly IServiceScopeFactory _scopes;
    private readonly CarbonSimHostOptions _options;
    private readonly ILogger<AdminAccountSeeder> _log;

    public AdminAccountSeeder(
        IServiceScopeFactory scopes,
        CarbonSimHostOptions options,
        ILogger<AdminAccountSeeder> log)
    {
        _scopes = scopes ?? throw new ArgumentNullException(nameof(scopes));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _log = log ?? throw new ArgumentNullException(nameof(log));
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_options.AdminEmail) || string.IsNullOrWhiteSpace(_options.AdminPassword))
        {
            return;
        }

        using IServiceScope scope = _scopes.CreateScope();
        IAccountStore store = scope.ServiceProvider.GetRequiredService<IAccountStore>();
        PlayerAccount? existing = await store.FindAsync(_options.AdminEmail, cancellationToken).ConfigureAwait(false);

        if (existing is not null)
        {
            if (existing.Role != AccountRole.Administrator)
            {
                _log.LogWarning(
                    "The configured administrator address '{Email}' already belongs to a {Role} account; no administrator was created.",
                    _options.AdminEmail,
                    existing.Role);
            }

            return;
        }

        IPasswordHasher<PlayerAccount> hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher<PlayerAccount>>();
        string passwordHash = hasher.HashPassword(new PlayerAccount(), _options.AdminPassword);

        PlayerAccount? created = await store
            .CreateAsync(_options.AdminEmail, "Exercise administrator", passwordHash, AccountRole.Administrator, null, cancellationToken)
            .ConfigureAwait(false);

        if (created is null)
        {
            _log.LogWarning("The administrator account '{Email}' could not be created.", _options.AdminEmail);
            return;
        }

        _log.LogInformation("Created the administrator account {Email}.", created.Email);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
