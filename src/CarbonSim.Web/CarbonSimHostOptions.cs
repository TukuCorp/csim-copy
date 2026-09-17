namespace CarbonSim.Web;

/// <summary>
/// The host's own settings, bound from the "CarbonSim" configuration section and read once at
/// start-up: where the database and the scenario live, which PIN gates registration, and how a
/// password-reset code reaches the person who asked for it. Nothing here is a secret in the source
/// tree; a deployment supplies the PIN through its own configuration.
/// </summary>
public sealed class CarbonSimHostOptions
{
    /// <summary>The configuration section these settings are bound from.</summary>
    public const string SectionName = "CarbonSim";

    /// <summary>Writes every message to the log at warning level because no transport is configured.</summary>
    public const string LogEmailSender = "Log";

    /// <summary>Keeps sent messages in memory; the development host and the tests use this.</summary>
    public const string InMemoryEmailSender = "InMemory";

    /// <summary>Applies the data project's migrations; what a host that keeps its data does.</summary>
    public const string MigrateSchema = "Migrate";

    /// <summary>Builds the schema from the model and keeps no migration history.</summary>
    public const string SchemaFromModel = "FromModel";

    /// <summary>The application database. SQLite, by default a file next to the host.</summary>
    public string ConnectionString { get; set; } = "Data Source=carbonsim.sqlite";

    /// <summary>
    /// The administrator access PIN a visitor must quote to register. Empty closes registration,
    /// so a deployment that forgets to configure a PIN cannot be registered against.
    /// </summary>
    public string RegistrationPin { get; set; } = string.Empty;

    /// <summary>The scenario whose human companies can be claimed, absolute or relative to the content root.</summary>
    public string ScenarioFile { get; set; } = "../../scenarios/vietnam-2024.json";

    /// <summary>
    /// How the host brings the database schema up: <see cref="MigrateSchema"/> (the default) applies
    /// the migrations the data project ships, while <see cref="SchemaFromModel"/> builds the schema
    /// from the model and keeps no history. A throwaway host - an integration test run, a demo box -
    /// uses the second, because it drops its database anyway and must not depend on the migrations
    /// being regenerated in step with the model.
    /// </summary>
    public string Schema { get; set; } = MigrateSchema;

    /// <summary>
    /// How reset codes are delivered: <see cref="LogEmailSender"/> or
    /// <see cref="InMemoryEmailSender"/>. Any other value stops the host at start-up rather than
    /// quietly dropping mail.
    /// </summary>
    public string EmailSender { get; set; } = LogEmailSender;

    /// <summary>How long an emailed reset code stays usable, in minutes.</summary>
    public int ResetCodeLifetimeMinutes { get; set; } = 15;

    /// <summary>How many wrong codes an open reset accepts before the host closes it.</summary>
    public int ResetCodeMaxAttempts { get; set; } = 5;
}
