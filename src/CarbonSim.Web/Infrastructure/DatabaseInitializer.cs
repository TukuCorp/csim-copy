using CarbonSim.Data;
using Microsoft.EntityFrameworkCore;

namespace CarbonSim.Web.Infrastructure;

/// <summary>
/// Brings the application database up to date before the host serves its first request. A host that
/// keeps its data applies the migrations the data project ships; a throwaway one builds the schema
/// from the model, which is what a test run or a demo box wants.
/// </summary>
internal static class DatabaseInitializer
{
    public static async Task InitializeAsync(
        IServiceProvider services,
        CarbonSimHostOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(options);

        await using AsyncServiceScope scope = services.CreateAsyncScope();
        CarbonSimDbContext context = scope.ServiceProvider.GetRequiredService<CarbonSimDbContext>();

        if (string.Equals(options.Schema, CarbonSimHostOptions.SchemaFromModel, StringComparison.Ordinal))
        {
            await context.Database.EnsureCreatedAsync(cancellationToken).ConfigureAwait(false);
            return;
        }

        await context.Database.MigrateAsync(cancellationToken).ConfigureAwait(false);
    }
}
