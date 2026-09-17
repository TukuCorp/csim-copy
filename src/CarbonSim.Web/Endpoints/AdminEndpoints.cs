using CarbonSim.Data.Accounts;
using CarbonSim.Web.Contracts;

namespace CarbonSim.Web.Endpoints;

/// <summary>The administrator surface of the host: what only the person running the exercise may see.</summary>
internal static class AdminEndpoints
{
    public static void MapAdminEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        endpoints
            .MapGet("/api/admin/accounts", ListAccountsAsync)
            .RequireAuthorization(HostAuthorization.AdministratorPolicy);
    }

    private static async Task<IResult> ListAccountsAsync(IAccountStore store, CancellationToken cancellationToken)
    {
        IReadOnlyList<PlayerAccount> accounts = await store.ListAsync(cancellationToken).ConfigureAwait(false);

        return Results.Ok(accounts.Select(AdminAccountResponse.From).ToArray());
    }
}
