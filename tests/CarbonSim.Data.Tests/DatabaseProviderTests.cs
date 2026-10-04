using CarbonSim.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CarbonSim.Data.Tests;

/// <summary>
/// CLAUDE.md promises PostgreSQL is switchable in configuration. Without a PostgreSQL server in
/// the gate, what can be proven is that the registration picks the right provider from that
/// configuration and that the model scripts as PostgreSQL SQL, so a deployment's schema comes from
/// the model rather than from the SQLite migration set. What is not exercised: a live PostgreSQL
/// connection, and therefore the actual application of the schema to a server.
/// </summary>
public sealed class DatabaseProviderTests
{
    private const string PostgresConnection =
        "Host=localhost;Database=carbonsim;Username=carbonsim;Password=secret";

    [Fact]
    public void The_registration_picks_sqlite_by_default()
    {
        using CarbonSimDbContext context = RegisteredContext(DatabaseProviders.Sqlite, "Data Source=carbonsim-test.sqlite");

        context.Database.ProviderName.Should().Be("Microsoft.EntityFrameworkCore.Sqlite");
    }

    [Fact]
    public void The_registration_switches_to_npgsql_for_postgres()
    {
        using CarbonSimDbContext context = RegisteredContext(DatabaseProviders.Postgres, PostgresConnection);

        context.Database.ProviderName.Should().Be("Npgsql.EntityFrameworkCore.PostgreSQL");
    }

    [Fact]
    public void An_unknown_provider_is_refused_when_the_database_is_registered()
    {
        ServiceCollection services = new();

        Action act = () => services.AddCarbonSimData("Data Source=x", "Oracle");

        act.Should().Throw<ArgumentException>().WithMessage("*Unknown database provider*");
    }

    [Fact]
    public void The_model_scripts_as_postgres_sql_without_a_server()
    {
        DbContextOptions<CarbonSimDbContext> options = new DbContextOptionsBuilder<CarbonSimDbContext>()
            .UseNpgsql(PostgresConnection)
            .Options;
        using CarbonSimDbContext context = new(options);

        string script = context.Database.GenerateCreateScript();

        script.Should().Contain("CREATE TABLE");
        script.Should().Contain("uuid", "a Guid key must be a Postgres uuid, not SQLite TEXT");
        script.Should().Contain("numeric", "a decimal column must be a Postgres numeric, not SQLite TEXT");
    }

    private static CarbonSimDbContext RegisteredContext(string provider, string connectionString)
    {
        ServiceCollection services = new();
        services.AddCarbonSimData(connectionString, provider);
        ServiceProvider root = services.BuildServiceProvider();

        return root.CreateScope().ServiceProvider.GetRequiredService<CarbonSimDbContext>();
    }
}
