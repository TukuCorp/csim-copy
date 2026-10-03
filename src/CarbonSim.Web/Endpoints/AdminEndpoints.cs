using System.Text;
using CarbonSim.Data.Accounts;
using CarbonSim.Web.Admin;
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

        endpoints
            .MapGet("/api/admin/runs", ListRunsAsync)
            .RequireAuthorization(HostAuthorization.AdministratorPolicy);

        // One report per run as a file a spreadsheet can open. The report name is part of the
        // path, so a link is something a trainer can paste into a browser or a worksheet.
        endpoints
            .MapGet("/api/admin/runs/{simulationId:guid}/reports/{report}.csv", ReportAsync)
            .RequireAuthorization(HostAuthorization.AdministratorPolicy);
    }

    private static async Task<IResult> ListAccountsAsync(IAccountStore store, CancellationToken cancellationToken)
    {
        IReadOnlyList<PlayerAccount> accounts = await store.ListAsync(cancellationToken).ConfigureAwait(false);

        return Results.Ok(accounts.Select(AdminAccountResponse.From).ToArray());
    }

    private static async Task<IResult> ListRunsAsync(IAdminSession admin, CancellationToken cancellationToken)
    {
        AdminSnapshot snapshot = await admin.SnapshotAsync(null, cancellationToken).ConfigureAwait(false);

        return snapshot.IsAdministrator
            ? Results.Ok(snapshot.Runs)
            : Results.Forbid();
    }

    private static async Task<IResult> ReportAsync(
        Guid simulationId,
        string report,
        IAdminSession admin,
        CancellationToken cancellationToken)
    {
        string? csv = await admin.CsvAsync(simulationId, report, cancellationToken).ConfigureAwait(false);

        if (csv is null)
        {
            return Results.NotFound(new ErrorResponse($"There is no '{report}' report for simulation {simulationId}."));
        }

        return Results.File(Encoding.UTF8.GetBytes(csv), "text/csv", $"{report}.csv");
    }
}
