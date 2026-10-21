using System.Globalization;
using CarbonSim.Data.Accounts;
using Microsoft.Extensions.DependencyInjection;

namespace CarbonSim.Web.Simulations;

/// <summary>
/// Answers the one question the registry needs before it lets anybody act: which company does this
/// caller play? A signed-in player reaches the hub with the identity written into the
/// authentication cookie, which is the account row's id, and the account records the human company
/// it claimed at registration - the same claiming model the roster uses. Anonymous connections,
/// administrators and accounts that never claimed a company all resolve to null, so none of them
/// can act for a company.
/// </summary>
public sealed class AccountCompanyResolver
{
    private readonly IServiceScopeFactory _scopes;
    private IReadOnlyDictionary<string, string>? _playersByCompany;

    public AccountCompanyResolver(IServiceScopeFactory scopes)
    {
        _scopes = scopes ?? throw new ArgumentNullException(nameof(scopes));
    }

    /// <summary>The company the actor owns, or null when the actor plays no company.</summary>
    public async Task<string?> CompanyForAsync(string actor, CancellationToken cancellationToken = default)
    {
        if (!int.TryParse(actor, NumberStyles.Integer, CultureInfo.InvariantCulture, out int accountId))
        {
            return null;
        }

        using IServiceScope scope = _scopes.CreateScope();
        IAccountStore store = scope.ServiceProvider.GetRequiredService<IAccountStore>();
        PlayerAccount? account = await store.FindByIdAsync(accountId, cancellationToken).ConfigureAwait(false);

        return account?.CompanyName;
    }

    /// <summary>
    /// Who plays each claimed company, by the name their account is shown under: the leaderboard's
    /// "unit (player)". Read once and kept until a registration changes it, because every screen
    /// asks every second and the answer only moves when somebody registers.
    /// </summary>
    public async Task<IReadOnlyDictionary<string, string>> PlayersByCompanyAsync(CancellationToken cancellationToken = default)
    {
        if (_playersByCompany is { } known)
        {
            return known;
        }

        using IServiceScope scope = _scopes.CreateScope();
        IAccountStore store = scope.ServiceProvider.GetRequiredService<IAccountStore>();
        IReadOnlyList<PlayerAccount> accounts = await store.ListAsync(cancellationToken).ConfigureAwait(false);
        Dictionary<string, string> players = new(StringComparer.Ordinal);

        foreach (PlayerAccount account in accounts)
        {
            if (account.CompanyName is { Length: > 0 } company)
            {
                players[company] = account.DisplayName;
            }
        }

        _playersByCompany = players;

        return players;
    }

    /// <summary>Drops the names read so far; a registration calls this once it has claimed a company.</summary>
    public void Forget() => _playersByCompany = null;
}
