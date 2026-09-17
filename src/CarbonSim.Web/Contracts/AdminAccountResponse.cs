using CarbonSim.Data.Accounts;

namespace CarbonSim.Web.Contracts;

/// <summary>One account as an administrator sees it.</summary>
public sealed record AdminAccountResponse(int Id, string Email, string DisplayName, string? Company, string Role)
{
    public static AdminAccountResponse From(PlayerAccount account)
    {
        ArgumentNullException.ThrowIfNull(account);

        return new AdminAccountResponse(
            account.Id,
            account.Email,
            account.DisplayName,
            account.CompanyName,
            account.Role.ToString());
    }
}
