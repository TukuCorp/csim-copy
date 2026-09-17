using CarbonSim.Data.Accounts;

namespace CarbonSim.Web.Contracts;

/// <summary>One account as its owner sees it. The password hash never leaves the host.</summary>
/// <param name="Id">Row identifier of the account.</param>
/// <param name="Email">The address the account signs in with, normalised.</param>
/// <param name="DisplayName">The name the account is shown under.</param>
/// <param name="Company">The human company it plays, or null while it has not claimed one.</param>
/// <param name="Role">"Player" or "Administrator"; the host grants it, a caller cannot ask for it.</param>
public sealed record AccountResponse(int Id, string Email, string DisplayName, string? Company, string Role)
{
    public static AccountResponse From(PlayerAccount account)
    {
        ArgumentNullException.ThrowIfNull(account);

        return new AccountResponse(account.Id, account.Email, account.DisplayName, account.CompanyName, account.Role.ToString());
    }
}
