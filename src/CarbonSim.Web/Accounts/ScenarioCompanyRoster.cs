using CarbonSim.Data.Accounts;

namespace CarbonSim.Web.Accounts;

/// <summary>
/// The roster of the scenario the host is configured to play: the companies a human owns, minus
/// the ones an account already claimed, so a deployment needs no second list of who plays what.
/// </summary>
public sealed class ScenarioCompanyRoster(IAccountStore store, ScenarioCompanyCatalog catalog) : ICompanyRoster
{
    private readonly IAccountStore _store = store ?? throw new ArgumentNullException(nameof(store));
    private readonly ScenarioCompanyCatalog _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));

    public async Task<IReadOnlyList<string>> ListUnclaimedAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyList<PlayerAccount> accounts = await _store.ListAsync(cancellationToken).ConfigureAwait(false);
        HashSet<string> claimed = new(
            accounts.Select(account => account.CompanyName).OfType<string>(),
            StringComparer.Ordinal);

        return [.. _catalog.HumanCompanies.Where(company => !claimed.Contains(company))];
    }

    public async Task<bool> IsClaimableAsync(string companyName, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(companyName);

        string company = companyName.Trim();

        if (!_catalog.HumanCompanies.Contains(company, StringComparer.Ordinal))
        {
            return false;
        }

        return !await _store.IsCompanyTakenAsync(company, cancellationToken).ConfigureAwait(false);
    }
}
