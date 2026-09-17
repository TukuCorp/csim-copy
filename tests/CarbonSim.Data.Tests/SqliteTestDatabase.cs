using CarbonSim.Data;
using Microsoft.EntityFrameworkCore;

namespace CarbonSim.Data.Tests;

/// <summary>
/// A throwaway SQLite database on disk, created by running the real migrations, so the tests
/// exercise the same schema a deployment gets. Disposing deletes the file.
/// </summary>
internal sealed class SqliteTestDatabase : IDisposable
{
    private readonly string _path;

    public SqliteTestDatabase()
    {
        _path = Path.Combine(Path.GetTempPath(), $"carbonsim-test-{Guid.NewGuid():N}.sqlite");
        Options = new DbContextOptionsBuilder<CarbonSimDbContext>()
            .UseSqlite($"Data Source={_path}")
            .Options;

        using CarbonSimDbContext context = CreateContext();
        context.Database.Migrate();
    }

    public DbContextOptions<CarbonSimDbContext> Options { get; }

    /// <summary>A context on the same database file; callers dispose it.</summary>
    public CarbonSimDbContext CreateContext() => new(Options);

    public void Dispose()
    {
        // The pools keep the file handle open on Windows, so clear them before deleting.
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();

        if (File.Exists(_path))
        {
            File.Delete(_path);
        }
    }
}
