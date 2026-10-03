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
}
