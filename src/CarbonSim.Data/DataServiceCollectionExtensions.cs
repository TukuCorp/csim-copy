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
    /// single-box training store; PostgreSQL is the server option. The <paramref name="provider"/>
    /// is the only thing that decides which, so a deployment switches store in configuration and
    /// nothing above this method changes.
    /// </summary>
    public static IServiceCollection AddCarbonSimData(
        this IServiceCollection services,
        string connectionString,
        string provider = DatabaseProviders.Sqlite)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        ArgumentException.ThrowIfNullOrWhiteSpace(provider);

        if (!DatabaseProviders.IsKnown(provider))
        {
            throw new ArgumentException(
                $"Unknown database provider '{provider}'; use '{DatabaseProviders.Sqlite}' or '{DatabaseProviders.Postgres}'.",
                nameof(provider));
        }

        services.AddDbContext<CarbonSimDbContext>(options =>
        {
            if (DatabaseProviders.IsPostgres(provider))
            {
                options.UseNpgsql(connectionString);
            }
            else
            {
                options.UseSqlite(connectionString);
            }
        });

        services.AddScoped<IAccountStore, EfAccountStore>();
        services.AddScoped<ISimulationRepository, EfSimulationRepository>();

        return services;
    }
}
