// CarbonSim host. It owns the accounts people log in with, the company each of them plays and
// the way a guest turns into a player; the simulation itself lives in CarbonSim.Engine and the
// database in CarbonSim.Data.
using CarbonSim.Data;
using CarbonSim.Data.Accounts;
using CarbonSim.Web;
using CarbonSim.Web.Accounts;
using CarbonSim.Web.Endpoints;
using CarbonSim.Web.Infrastructure;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;

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

builder.Services.AddSingleton(hostOptions);
builder.Services.AddCarbonSimData(hostOptions.ConnectionString);

// Only the password hasher comes from Identity: the host has one user type (the account row) and
// the engine owns the notion of a player, so no second user store is brought in beside them.
builder.Services.AddSingleton<IPasswordHasher<PlayerAccount>, PasswordHasher<PlayerAccount>>();

builder.Services.AddSingleton<ScenarioCompanyCatalog>();
builder.Services.AddScoped<ICompanyRoster, ScenarioCompanyRoster>();
builder.Services.AddScoped<PlayerAccountService>();
AddEmailSender(builder.Services, hostOptions);

builder.Services
    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Name = "carbonsim.auth";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.SlidingExpiration = true;

        // The Blazor screens arrive in the next phase; until they do, a browser that is turned away
        // is sent to these paths and the JSON endpoints answer with a status code instead.
        options.LoginPath = "/login";
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

app.UseAuthentication();
app.UseAuthorization();

app.MapAccountEndpoints();
app.MapAdminEndpoints();

app.Run();

// The host's own endpoints are JSON under /api, so they answer with a status code rather than a
// redirect to a login page nobody has built yet.
static Task TurnAway(RedirectContext<CookieAuthenticationOptions> context, int statusCode)
{
    if (context.Request.Path.StartsWithSegments("/api"))
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
