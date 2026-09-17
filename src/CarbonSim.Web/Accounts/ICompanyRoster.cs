namespace CarbonSim.Web.Accounts;

/// <summary>
/// The companies a visitor may claim at registration. The host asks this, not the scenario file
/// directly, so registration does not care where the roster comes from.
/// </summary>
public interface ICompanyRoster
{
    /// <summary>The companies waiting for a player, in scenario order.</summary>
    Task<IReadOnlyList<string>> ListUnclaimedAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Whether a company exists, is owned by a human player and has not been claimed yet. The
    /// answer can go stale within one registration; the account table's unique index is the
    /// authority when two visitors claim the same company at once.
    /// </summary>
    Task<bool> IsClaimableAsync(string companyName, CancellationToken cancellationToken = default);
}
