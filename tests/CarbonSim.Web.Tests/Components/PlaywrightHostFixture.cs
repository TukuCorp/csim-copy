using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using Microsoft.Playwright;

namespace CarbonSim.Web.Tests.Components;

/// <summary>
/// Marks a test that needs an installed Playwright browser. Where none is installed the test is
/// reported as skipped with the command that would fix it, rather than failing for the wrong reason.
/// </summary>
internal sealed class PlaywrightFactAttribute : FactAttribute
{
    public PlaywrightFactAttribute()
    {
        if (!BrowsersInstalled())
        {
            Skip = "Playwright browsers are not installed here. Install one with "
                + "`pwsh tests/CarbonSim.Web.Tests/bin/Debug/net9.0/playwright.ps1 install chromium`.";
        }
    }

    private static bool BrowsersInstalled()
    {
        string root = Environment.GetEnvironmentVariable("PLAYWRIGHT_BROWSERS_PATH") is { Length: > 0 } custom
            ? custom
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ms-playwright");

        return Directory.Exists(root) && Directory.EnumerateDirectories(root, "chromium*").Any();
    }
}

/// <summary>
/// Boots the real host on a free port against a throwaway database, with the demo run seeded, for
/// the Playwright walk. The host is a child process because a browser needs a real server; the
/// browser is launched once and shared by every test in the class.
/// </summary>
public sealed class PlaywrightHostFixture : IAsyncLifetime
{
    private const string RegistrationPin = "playwright-pin";

    /// <summary>The administrator the host bootstraps from configuration; the console's way in.</summary>
    public const string AdminEmail = "playwright-admin@example.com";

    public const string AdminPassword = "playwright-admin-2026";

    private readonly string _database = Path.Combine(
        Path.GetTempPath(),
        "carbonsim-playwright",
        $"{Guid.NewGuid():N}.sqlite");

    private Process? _app;
    private IPlaywright? _playwright;

    public string BaseUrl { get; private set; } = string.Empty;

    public IBrowser? Browser { get; private set; }

    public string RegistrationPinValue => RegistrationPin;

    public async Task InitializeAsync()
    {
        string root = RepositoryRoot();

        Directory.CreateDirectory(Path.GetDirectoryName(_database)!);

        int port = FreePort();
        BaseUrl = $"http://127.0.0.1:{port.ToString(CultureInfo.InvariantCulture)}";

        ProcessStartInfo start = new("dotnet")
        {
            WorkingDirectory = root,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };

        start.ArgumentList.Add("run");
        start.ArgumentList.Add("--project");
        start.ArgumentList.Add(Path.Combine(root, "src", "CarbonSim.Web"));
        start.ArgumentList.Add("--no-build");
        start.ArgumentList.Add("--no-launch-profile");
        start.ArgumentList.Add("--urls");
        start.ArgumentList.Add(BaseUrl);
        start.Environment["ASPNETCORE_ENVIRONMENT"] = "Development";
        start.Environment["CarbonSim__ConnectionString"] = $"Data Source={_database}";
        start.Environment["CarbonSim__Schema"] = "FromModel";
        start.Environment["CarbonSim__RegistrationPin"] = RegistrationPin;
        start.Environment["CarbonSim__StartDemoSimulation"] = "true";
        start.Environment["CarbonSim__EmailSender"] = "InMemory";
        start.Environment["CarbonSim__ScenarioFile"] = Path.Combine(root, "scenarios", "vietnam-2024.json");
        start.Environment["CarbonSim__AdminEmail"] = AdminEmail;
        start.Environment["CarbonSim__AdminPassword"] = AdminPassword;

        // Quiet the host: its output is read only if it fails to start, and a chatty log could fill
        // the pipe it writes into.
        start.Environment["Logging__LogLevel__Default"] = "Warning";
        start.Environment["Logging__LogLevel__Microsoft"] = "Warning";

        _app = Process.Start(start) ?? throw new InvalidOperationException("The host process could not be started.");

        await WaitForHostAsync().ConfigureAwait(false);

        try
        {
            _playwright = await Playwright.CreateAsync().ConfigureAwait(false);
            Browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = true }).ConfigureAwait(false);
        }
        catch (PlaywrightException)
        {
            // No browser installed: the attribute on each test reports the skip.
            Browser = null;
        }
    }

    public async Task DisposeAsync()
    {
        if (Browser is not null)
        {
            await Browser.DisposeAsync().ConfigureAwait(false);
        }

        _playwright?.Dispose();

        if (_app is { HasExited: false })
        {
            _app.Kill(entireProcessTree: true);
            await _app.WaitForExitAsync().ConfigureAwait(false);
        }

        _app?.Dispose();

        foreach (string file in new[] { _database, _database + "-wal", _database + "-shm" })
        {
            try
            {
                File.Delete(file);
            }
            catch (IOException)
            {
                // A file still held open is a tidiness problem, not a test failure.
            }
        }
    }

    /// <summary>Registers a player through the sign-in page and returns the open run's address.</summary>
    public async Task<string> SignInAsync(IPage page, string email)
    {
        ArgumentNullException.ThrowIfNull(page);

        await page.GotoAsync(BaseUrl + "/sign-in").ConfigureAwait(false);
        await page.WaitForSelectorAsync("#register-form").ConfigureAwait(false);

        await page.FillAsync("#register-form input[name=email]", email).ConfigureAwait(false);
        await page.FillAsync("#register-form input[name=displayName]", "Playwright Player").ConfigureAwait(false);
        await page.FillAsync("#register-form input[name=password]", "mekong-delta-2024").ConfigureAwait(false);
        await page.FillAsync("#register-form input[name=registrationPin]", RegistrationPin).ConfigureAwait(false);

        string company = await page
            .EvalOnSelectorAsync<string>("#register-form select[name=company]", "select => select.options[0].value")
            .ConfigureAwait(false);

        await page.SelectOptionAsync("#register-form select[name=company]", company).ConfigureAwait(false);
        await page.ClickAsync("#register-form button[type=submit]").ConfigureAwait(false);
        await page.WaitForURLAsync(BaseUrl + "/").ConfigureAwait(false);

        // The waiting screen lists the seeded run; open it.
        await page.WaitForSelectorAsync("a.button").ConfigureAwait(false);
        await page.ClickAsync("a.button").ConfigureAwait(false);
        await page.WaitForURLAsync("**/run/**").ConfigureAwait(false);

        return page.Url;
    }

    /// <summary>Signs the bootstrap administrator in through the sign-in form and lands on the console.</summary>
    public async Task AdminSignInAsync(IPage page)
    {
        ArgumentNullException.ThrowIfNull(page);

        await page.GotoAsync(BaseUrl + "/sign-in").ConfigureAwait(false);
        await page.WaitForSelectorAsync("#sign-in-form").ConfigureAwait(false);

        await page.FillAsync("#sign-in-form input[name=email]", AdminEmail).ConfigureAwait(false);
        await page.FillAsync("#sign-in-form input[name=password]", AdminPassword).ConfigureAwait(false);
        await page.ClickAsync("#sign-in-form button[type=submit]").ConfigureAwait(false);
        await page.WaitForURLAsync(BaseUrl + "/").ConfigureAwait(false);

        await page.GotoAsync(BaseUrl + "/admin").ConfigureAwait(false);
        await page.WaitForSelectorAsync("#admin-create-run, nav.nav", new PageWaitForSelectorOptions { Timeout = 30000 }).ConfigureAwait(false);
    }

    private async Task WaitForHostAsync()
    {
        Process app = _app ?? throw new InvalidOperationException("The host process is missing.");
        using HttpClient client = new() { BaseAddress = new Uri(BaseUrl) };

        for (int attempt = 0; attempt < 120; attempt++)
        {
            if (app.HasExited)
            {
                string output = await app.StandardOutput.ReadToEndAsync().ConfigureAwait(false);
                string errors = await app.StandardError.ReadToEndAsync().ConfigureAwait(false);

                throw new InvalidOperationException($"The host stopped before it could serve: {errors}{output}");
            }

            try
            {
                using HttpResponseMessage response = await client.GetAsync("/sign-in").ConfigureAwait(false);

                if (response.IsSuccessStatusCode)
                {
                    return;
                }
            }
            catch (HttpRequestException)
            {
                // Still starting.
            }

            await Task.Delay(TimeSpan.FromMilliseconds(500)).ConfigureAwait(false);
        }

        throw new InvalidOperationException($"The host did not answer on {BaseUrl} in time.");
    }

    /// <summary>Walks up from the test's own output folder to the folder holding the solution.</summary>
    private static string RepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "CarbonSim.sln")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName
            ?? throw new InvalidOperationException("The repository root could not be found from the test output folder.");
    }

    private static int FreePort()
    {
        TcpListener listener = new(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();

        return port;
    }
}
