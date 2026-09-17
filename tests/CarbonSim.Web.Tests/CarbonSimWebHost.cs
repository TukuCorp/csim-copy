using System.Globalization;
using CarbonSim.Web;
using CarbonSim.Web.Accounts;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;

namespace CarbonSim.Web.Tests;

/// <summary>
/// Boots the real host against a SQLite file of its own, so test classes run in parallel without
/// meeting each other's accounts. Everything the host reads from configuration - the database, the
/// registration PIN, the reset-code rules and the email sender - is supplied here, which is also
/// how these tests prove those settings are read from configuration at all.
/// </summary>
public class CarbonSimWebHost : WebApplicationFactory<Program>
{
    private readonly string _databasePath = Path.Combine(
        Path.GetTempPath(),
        "carbonsim-web-tests",
        $"{Guid.NewGuid():N}.sqlite");

    /// <summary>The PIN these tests register with; the shipped default is empty, which closes registration.</summary>
    public virtual string RegistrationPin => "test-registration-pin";

    public virtual string EmailSenderName => CarbonSimHostOptions.InMemoryEmailSender;

    public virtual int ResetCodeLifetimeMinutes => 15;

    public virtual int ResetCodeMaxAttempts => 5;

    /// <summary>Leaves the scenario path to the host's own default unless a test needs its own file.</summary>
    public virtual string? ScenarioFile => null;

    /// <summary>
    /// The test host builds its schema from the model instead of applying migrations: each class
    /// throws its database away, and a migration being regenerated beside the model must not be
    /// able to fail this suite.
    /// </summary>
    public virtual string SchemaStrategy => CarbonSimHostOptions.SchemaFromModel;

    /// <summary>The messages this host has sent; only the in-memory sender can report them.</summary>
    public InMemoryEmailSender Emails => Services.GetRequiredService<InMemoryEmailSender>();

    /// <summary>Where this host keeps its database, so a test can look at the file itself.</summary>
    public string DatabasePath => _databasePath;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        Directory.CreateDirectory(Path.GetDirectoryName(_databasePath)!);

        builder.UseSetting($"{CarbonSimHostOptions.SectionName}:ConnectionString", $"Data Source={_databasePath}");
        builder.UseSetting($"{CarbonSimHostOptions.SectionName}:RegistrationPin", RegistrationPin);
        builder.UseSetting($"{CarbonSimHostOptions.SectionName}:EmailSender", EmailSenderName);
        builder.UseSetting($"{CarbonSimHostOptions.SectionName}:Schema", SchemaStrategy);
        builder.UseSetting(
            $"{CarbonSimHostOptions.SectionName}:ResetCodeLifetimeMinutes",
            ResetCodeLifetimeMinutes.ToString(CultureInfo.InvariantCulture));
        builder.UseSetting(
            $"{CarbonSimHostOptions.SectionName}:ResetCodeMaxAttempts",
            ResetCodeMaxAttempts.ToString(CultureInfo.InvariantCulture));

        if (ScenarioFile is not null)
        {
            builder.UseSetting($"{CarbonSimHostOptions.SectionName}:ScenarioFile", ScenarioFile);
        }

        builder.ConfigureTestServices(ConfigureHostServices);
    }

    /// <summary>Lets a fixture swap a host service - the company roster, typically - for its own.</summary>
    protected virtual void ConfigureHostServices(IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (!disposing)
        {
            return;
        }

        // SQLite pools its connections, and Windows will not delete a file they still hold open.
        SqliteConnection.ClearAllPools();

        try
        {
            File.Delete(_databasePath);
        }
        catch (IOException)
        {
            // A file an abandoned connection still holds is a tidiness problem, not a test failure.
        }
    }
}
