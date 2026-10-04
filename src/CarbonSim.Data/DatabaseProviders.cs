namespace CarbonSim.Data;

/// <summary>
/// The two databases the host can keep its data in. SQLite is the dev and single-box training
/// store; PostgreSQL is the server option, chosen in configuration alone. The engine never sees
/// either name: it is persistence-neutral by design, and everything provider-shaped lives here.
/// </summary>
public static class DatabaseProviders
{
    public const string Sqlite = "Sqlite";

    public const string Postgres = "Postgres";

    /// <summary>Whether a configured provider name is one this host knows how to open.</summary>
    public static bool IsKnown(string provider) =>
        string.Equals(provider, Sqlite, StringComparison.OrdinalIgnoreCase)
        || string.Equals(provider, Postgres, StringComparison.OrdinalIgnoreCase);

    public static bool IsPostgres(string provider) =>
        string.Equals(provider, Postgres, StringComparison.OrdinalIgnoreCase);
}
