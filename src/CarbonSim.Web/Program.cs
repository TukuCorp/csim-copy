// CarbonSim host. It owns the accounts people log in with, the company each of them plays, the
// player screens, and the way a guest turns into a player; the simulation itself lives in
// CarbonSim.Engine and the database in CarbonSim.Data.
using System.Globalization;
using CarbonSim.Data;
using CarbonSim.Data.Accounts;
using CarbonSim.Web;
using CarbonSim.Web.Accounts;
using CarbonSim.Web.Admin;
using CarbonSim.Web.Components;
using CarbonSim.Web.Components.Shared;
using CarbonSim.Web.Endpoints;
using CarbonSim.Web.Infrastructure;
using CarbonSim.Web.Player;
using CarbonSim.Web.Simulations;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Localization;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

CarbonSimHostOptions hostOptions = builder.Configuration
    .GetSection(CarbonSimHostOptions.SectionName)
    .Get<CarbonSimHostOptions>() ?? new CarbonSimHostOptions();

if (hostOptions.ResetCodeMaxAttempts < 1)
{
    throw new InvalidOperationException(
        $"'{CarbonSimHostOptions.SectionName}:{nameof(CarbonSimHostOptions.ResetCodeMaxAttempts)}' must be at least 1, " +
        "otherwise no password reset could ever be confirmed.");
}

if (hostOptions.Schema is not (CarbonSimHostOptions.MigrateSchema or CarbonSimHostOptions.SchemaFromModel))
{
    throw new InvalidOperationException(
        $"'{CarbonSimHostOptions.SectionName}:{nameof(CarbonSimHostOptions.Schema)}' must be " +
        $"'{CarbonSimHostOptions.MigrateSchema}' or '{CarbonSimHostOptions.SchemaFromModel}' (was '{hostOptions.Schema}').");
}

if (!CurrencyDisplay.IsKnown(hostOptions.DisplayCurrency))
{
    throw new InvalidOperationException(
        $"'{CarbonSimHostOptions.SectionName}:{nameof(CarbonSimHostOptions.DisplayCurrency)}' must be " +
        $"'{CurrencyDisplay.Usd}' or '{CurrencyDisplay.Vnd}' (was '{hostOptions.DisplayCurrency}').");
}

if (hostOptions.VndPerUsd <= 0m)
{
    throw new InvalidOperationException(
        $"'{CarbonSimHostOptions.SectionName}:{nameof(CarbonSimHostOptions.VndPerUsd)}' must be greater than zero " +
        $"(was {hostOptions.VndPerUsd.ToString(CultureInfo.InvariantCulture)}).");
}

if (!CarbonSim.Data.DatabaseProviders.IsKnown(hostOptions.Provider))
{
    throw new InvalidOperationException(
        $"'{CarbonSimHostOptions.SectionName}:{nameof(CarbonSimHostOptions.Provider)}' must be " +
        $"'{CarbonSim.Data.DatabaseProviders.Sqlite}' or '{CarbonSim.Data.DatabaseProviders.Postgres}' " +
        $"(was '{hostOptions.Provider}').");
}

// The shipped migration set is SQLite's; a PostgreSQL deployment builds its schema from the model
// instead (see docs/deployment.md), so the two settings cannot be combined by accident.
if (CarbonSim.Data.DatabaseProviders.IsPostgres(hostOptions.Provider)
    && string.Equals(hostOptions.Schema, CarbonSimHostOptions.MigrateSchema, StringComparison.Ordinal))
{
    throw new InvalidOperationException(
        $"'{CarbonSimHostOptions.SectionName}:{nameof(CarbonSimHostOptions.Schema)}' must be " +
        $"'{CarbonSimHostOptions.SchemaFromModel}' for the {CarbonSim.Data.DatabaseProviders.Postgres} provider: " +
        "the shipped migrations are written for SQLite. See docs/deployment.md.");
}

// Every screen writes money through Display, so the chosen currency is set once here.
Display.Currency = new CurrencyDisplay(hostOptions.DisplayCurrency, hostOptions.VndPerUsd);

builder.Services.AddSingleton(hostOptions);
builder.Services.AddHttpContextAccessor();
builder.Services.AddCarbonSimData(hostOptions.ConnectionString, hostOptions.Provider);

// Only the password hasher comes from Identity: the host has one user type (the account row) and
// the engine owns the notion of a player, so no second user store is brought in beside them.
builder.Services.AddSingleton<IPasswordHasher<PlayerAccount>, PasswordHasher<PlayerAccount>>();

builder.Services.AddSingleton<ScenarioCompanyCatalog>();
builder.Services.AddScoped<ICompanyRoster, ScenarioCompanyRoster>();
builder.Services.AddSingleton(provider => new RegistrationPolicy(provider.GetRequiredService<CarbonSimHostOptions>()));
builder.Services.AddScoped<PlayerAccountService>();
AddEmailSender(builder.Services, hostOptions);
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton(
    new CarbonSim.Web.Simulations.SimulationDriverOptions { PollInterval = TimeSpan.FromSeconds(1) });
builder.Services.AddSingleton<CarbonSim.Web.Simulations.SimulationRegistry>();
builder.Services.AddSingleton<CarbonSim.Web.Simulations.AccountCompanyResolver>();

// One driver instance, so the hosted loop and anything else that ticks a run share it.
builder.Services.AddSingleton<CarbonSim.Web.Simulations.SimulationClockService>();
builder.Services.AddHostedService(provider =>
    provider.GetRequiredService<CarbonSim.Web.Simulations.SimulationClockService>());

if (hostOptions.StartDemoSimulation)
{
    // Development only: a run is on the shelf the moment the host boots, so `dotnet run` shows a
    // live game. The shipped configuration leaves this off.
    builder.Services.AddHostedService<CarbonSim.Web.Simulations.DemoSimulationSeeder>();
}

builder.Services.AddSignalR();

// The player screens. They read the run on the server through PlayerSession and refresh from the
// registry, rather than opening a browser SignalR connection of their own.
builder.Services.AddRazorComponents().AddInteractiveServerComponents();

if (builder.Environment.IsDevelopment())
{
    // A Blazor circuit that fails in the browser says nothing useful otherwise; in development the
    // real exception is worth having in the console.
    builder.Services.Configure<Microsoft.AspNetCore.Components.Server.CircuitOptions>(
        options => options.DetailedErrors = true);
}
builder.Services.AddScoped<PlayerSession>();
builder.Services.AddScoped<IPlayerSession>(provider => provider.GetRequiredService<PlayerSession>());
builder.Services.AddScoped<PlayerContext>();
builder.Services.AddScoped<ICharts, EChartsInterop>();
builder.Services.AddSingleton(new PlayerViewOptions { RefreshInterval = TimeSpan.FromSeconds(1) });

// The trainer's console. It reads and drives the same runs the player screens watch, through the
// same server-side driver, and every control is refused unless the account holds the admin role.
builder.Services.AddScoped<AdminSession>();
builder.Services.AddScoped<IAdminSession>(provider => provider.GetRequiredService<AdminSession>());
builder.Services.AddScoped<AdminContext>();
builder.Services.AddSingleton(new AdminContextOptions { RefreshInterval = TimeSpan.FromSeconds(1) });
builder.Services.AddHostedService<AdminAccountSeeder>();

builder.Services.AddLocalization();
builder.Services.AddAntiforgery();
builder.Services.Configure<RequestLocalizationOptions>(options =>
{
    options.DefaultRequestCulture = new RequestCulture(new CultureInfo(CarbonSimCultures.Default));
    options.SupportedCultures = [.. CarbonSimCultures.Supported.Select(name => new CultureInfo(name))];
    options.SupportedUICultures = [.. CarbonSimCultures.Supported.Select(name => new CultureInfo(name))];
    options.RequestCultureProviders =
    [
        new CookieRequestCultureProvider { CookieName = CarbonSimCultures.CookieName },
        new QueryStringRequestCultureProvider(),
        new AcceptLanguageHeaderRequestCultureProvider(),
    ];
});

builder.Services
    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Name = "carbonsim.auth";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.SlidingExpiration = true;

        // The player screens arrive in this phase; a browser that is turned away is sent to the
        // sign-in form, and the JSON endpoints and the hub answer with a status code instead.
        options.LoginPath = "/sign-in";
        options.AccessDeniedPath = "/access-denied";

        options.Events.OnRedirectToLogin = context => TurnAway(context, StatusCodes.Status401Unauthorized);
        options.Events.OnRedirectToAccessDenied = context => TurnAway(context, StatusCodes.Status403Forbidden);
    });

builder.Services
    .AddAuthorizationBuilder()
    .AddPolicy(
        HostAuthorization.AdministratorPolicy,
        policy => policy.RequireRole(nameof(AccountRole.Administrator)));

WebApplication app = builder.Build();

await DatabaseInitializer.InitializeAsync(app.Services, hostOptions).ConfigureAwait(false);

app.UseRequestLocalization();
app.UseStaticFiles();

app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

app.MapAccountEndpoints();
app.MapSignInEndpoints();
app.MapCultureEndpoints();
app.MapAdminEndpoints();
app.MapHub<CarbonSim.Web.Simulations.SimulationHub>("/hubs/simulation").RequireAuthorization();
app.MapRazorComponents<App>().AddInteractiveServerRenderMode();

app.Run();

// The host's own endpoints are JSON under /api and the SignalR hub under /hubs, so they answer
// with a status code rather than a redirect a caller cannot use. SignalR in particular needs a 401:
// it cannot follow a redirect to an HTML page.
static Task TurnAway(RedirectContext<CookieAuthenticationOptions> context, int statusCode)
{
    if (context.Request.Path.StartsWithSegments("/api") || context.Request.Path.StartsWithSegments("/hubs"))
    {
        context.Response.StatusCode = statusCode;
        return Task.CompletedTask;
    }

    context.Response.Redirect(context.RedirectUri);
    return Task.CompletedTask;
}

// Chooses where password-reset codes go. An unrecognised name stops the host: a host that quietly
// dropped mail would leave players locked out with nothing in the log to explain it.
static void AddEmailSender(IServiceCollection services, CarbonSimHostOptions options)
{
    switch (options.EmailSender)
    {
        case CarbonSimHostOptions.InMemoryEmailSender:
            services.AddSingleton<InMemoryEmailSender>();
            services.AddSingleton<IEmailSender>(provider => provider.GetRequiredService<InMemoryEmailSender>());
            break;
        case CarbonSimHostOptions.LogEmailSender:
            services.AddSingleton<IEmailSender, LoggingEmailSender>();
            break;
        default:
            throw new InvalidOperationException(
                $"'{CarbonSimHostOptions.SectionName}:{nameof(CarbonSimHostOptions.EmailSender)}' must be " +
                $"'{CarbonSimHostOptions.LogEmailSender}' or '{CarbonSimHostOptions.InMemoryEmailSender}' " +
                $"(was '{options.EmailSender}').");
    }
}

/// <summary>Lets <c>WebApplicationFactory&lt;Program&gt;</c> boot this host from a test project.</summary>
public partial class Program
{
}
