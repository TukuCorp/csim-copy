using CarbonSim.Data.Accounts;
using CarbonSim.Data.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CarbonSim.Data;

/// <summary>Wiring the application database into a host.</summary>
public static class DataServiceCollectionExtensions
{
    /// <summary>
    /// Registers the database and the account store over it. SQLite is the development and
    /// single-box training store; the connection string is the only thing that decides where the
    /// data lives, so a PostgreSQL provider can be swapped in here without touching anything else.
    /// </summary>
    public static IServiceCollection AddCarbonSimData(this IServiceCollection services, string connectionString)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        services.AddDbContext<CarbonSimDbContext>(options => options.UseSqlite(connectionString));
        services.AddScoped<IAccountStore, EfAccountStore>();
        services.AddScoped<ISimulationRepository, EfSimulationRepository>();

        return services;
    }
}
