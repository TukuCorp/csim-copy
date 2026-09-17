using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace CarbonSim.Data;

/// <summary>
/// Lets `dotnet ef migrations` build the context without starting the web host. The connection
/// string here is only used to work out the provider; migrations do not touch a database file.
/// </summary>
internal sealed class CarbonSimDbContextFactory : IDesignTimeDbContextFactory<CarbonSimDbContext>
{
    public CarbonSimDbContext CreateDbContext(string[] args)
    {
        DbContextOptionsBuilder<CarbonSimDbContext> builder = new();
        builder.UseSqlite("Data Source=carbonsim-design.sqlite");

        return new CarbonSimDbContext(builder.Options);
    }
}
