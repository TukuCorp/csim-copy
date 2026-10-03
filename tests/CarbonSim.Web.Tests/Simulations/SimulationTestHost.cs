using System.Globalization;
using System.Net.Http.Json;
using CarbonSim.Data.Persistence;
using CarbonSim.Web.Accounts;
using CarbonSim.Web.Contracts;
using CarbonSim.Web.Simulations;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CarbonSim.Web.Tests.Simulations;

/// <summary>A player signed in for one company: the account id the hub carries as the caller's
/// identity, and the cookie a SignalR connection presents to prove it.</summary>
public sealed record SignedIn(int AccountId, string Cookie)
{
    public string Actor => AccountId.ToString(CultureInfo.InvariantCulture);
}

/// <summary>The host the simulation tests boot: its wall clock is a fake the test advances by
/// hand, and the background driver is manual-only so only the test's own ticks move a run.
/// The shared background loop never fires mid-assertion.</summary>
public class SimulationTestHost : CarbonSimWebHost
{
    private readonly Dictionary<string, SignedIn> _players = new(StringComparer.Ordinal);
    private int _nextPlayer = 1;

    public FakeTimeProvider FakeTime { get; } = new();

    /// <summary>How many times a run has been written to the repository, for the save-once test.</summary>
    public SaveCounter Saves => Server.Services.GetRequiredService<SaveCounter>();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        base.ConfigureWebHost(builder);

        builder.ConfigureTestServices(services =>
        {
            // The fake replaces System time after Program's own registration; the run starts
            // after the host boots, so its clock always captures this instance.
            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(FakeTime);

            services.RemoveAll<SimulationDriverOptions>();
            services.AddSingleton(new SimulationDriverOptions { PollInterval = TimeSpan.Zero });

            // Count saves so a test can prove a year end is written once rather than on every tick.
            services.RemoveAll<ISimulationRepository>();
            services.AddSingleton<SaveCounter>();
            services.AddScoped<EfSimulationRepository>();
            services.AddScoped<ISimulationRepository>(provider => new CountingSimulationRepository(
                provider.GetRequiredService<EfSimulationRepository>(),
                provider.GetRequiredService<SaveCounter>()));
        });
    }

    protected override void ConfigureHostServices(IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        // Registration has to know the run's companies, and the account each player claims is
        // what the registry later maps a hub identity through, so the roster is the two companies
        // of TestRunFactory rather than whatever scenario file the host would otherwise read.
        services.RemoveAll<ICompanyRoster>();
        services.AddScoped<ICompanyRoster>(_ => new FakeCompanyRoster(
            TestRunFactory.DeltaCompany,
            TestRunFactory.RedRiverCompany));
    }

    /// <summary>
    /// Registers an account for a company the first time and signs it in, so a test reaches a run
    /// as a real player rather than through a convention. Later tests in the same class reuse the
    /// account, because one account owns a company for good.
    /// </summary>
    public async Task<SignedIn> SignInAsync(string company)
    {
        if (_players.TryGetValue(company, out SignedIn? existing))
        {
            return existing;
        }

        string email = $"sim-player-{_nextPlayer++:D2}@example.com";

        using HttpClient client = CreateClient();
        using HttpResponseMessage registered = await client.RegisterAsync(email, company, RegistrationPin).ConfigureAwait(false);

        if (!registered.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"Registering '{company}' failed: {(int)registered.StatusCode} {await registered.Content.ReadAsStringAsync().ConfigureAwait(false)}");
        }

        using HttpResponseMessage loggedIn = await client.LoginAsync(email).ConfigureAwait(false);

        if (!loggedIn.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"Signing in '{email}' failed: {(int)loggedIn.StatusCode}");
        }

        AccountResponse account = await loggedIn.Content.ReadFromJsonAsync<AccountResponse>().ConfigureAwait(false)
            ?? throw new InvalidOperationException("The host returned no account on sign-in.");

        string cookie = loggedIn.Headers
            .GetValues("Set-Cookie")
            .First(value => value.StartsWith("carbonsim.auth=", StringComparison.Ordinal))
            .Split(';')[0];

        SignedIn signedIn = new(account.Id, cookie);
        _players[company] = signedIn;

        return signedIn;
    }

    /// <summary>A SignalR connection to this host that presents a signed-in player's cookie.</summary>
    public IHubConnectionBuilder CreateHubBuilder(string cookie)
    {
        return new HubConnectionBuilder()
            .WithUrl(
                Server.BaseAddress + "hubs/simulation",
                options =>
                {
                    // Long polling keeps every request on the message handler that adds the
                    // cookie; a WebSocket upgrade would bypass it.
                    options.Transports = HttpTransportType.LongPolling;
                    options.HttpMessageHandlerFactory = _ => new CookieMessageHandler(Server.CreateHandler(), cookie);
                });
    }

    /// <summary>Drives a run by hand; the shared background loop never moves a test's run.</summary>
    public SimulationClockService CreateDriver()
    {
        return new SimulationClockService(
            Server.Services,
            new SimulationDriverOptions { PollInterval = TimeSpan.Zero },
            Server.Services.GetRequiredService<Microsoft.Extensions.Logging.ILogger<SimulationClockService>>());
    }

    /// <summary>Adds one signed-in player's cookie to every request the connection makes.</summary>
    private sealed class CookieMessageHandler(HttpMessageHandler inner, string cookie) : DelegatingHandler(inner)
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            request.Headers.TryAddWithoutValidation("Cookie", cookie);

            return base.SendAsync(request, cancellationToken);
        }
    }
}
