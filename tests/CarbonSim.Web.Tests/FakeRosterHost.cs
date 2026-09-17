using CarbonSim.Web.Accounts;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CarbonSim.Web.Tests;

/// <summary>
/// A roster a test controls. It offers a fixed list of companies and answers from it, but it
/// remembers nothing about who has claimed what: the account table's unique index is left as the
/// only thing that can refuse a second claim, which is exactly the behaviour that has to hold when
/// two visitors register at the same moment.
/// </summary>
internal sealed class FakeCompanyRoster(params string[] companies) : ICompanyRoster
{
    private readonly IReadOnlyList<string> _companies = companies;

    public Task<IReadOnlyList<string>> ListUnclaimedAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_companies);
    }

    public Task<bool> IsClaimableAsync(string companyName, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_companies.Contains(companyName, StringComparer.Ordinal));
    }
}

/// <summary>
/// The host most test classes use. Its companies come from a list rather than from the scenario on
/// disk, so a test that is not about the scenario file does not depend on it; every test claims a
/// company of its own, because one account owns a company for good.
/// </summary>
public class FakeRosterHost : CarbonSimWebHost
{
    public const string PowerCompany = "Delta Power";

    public const string CementCompany = "Mekong Cement";

    public const string SteelCompany = "Hai Phong Steel";

    public const string GridCompany = "Red River Grid";

    public const string PortCompany = "Cam Ranh Port";

    public const string PaperCompany = "Binh Duong Paper";

    protected override void ConfigureHostServices(IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.RemoveAll<ICompanyRoster>();
        services.AddScoped<ICompanyRoster>(_ => new FakeCompanyRoster(
            PowerCompany,
            CementCompany,
            SteelCompany,
            GridCompany,
            PortCompany,
            PaperCompany));
    }
}
