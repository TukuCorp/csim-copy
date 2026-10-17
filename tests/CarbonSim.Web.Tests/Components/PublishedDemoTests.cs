using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Playwright;

namespace CarbonSim.Web.Tests.Components;

/// <summary>
/// The internal demo exactly as an operator runs it: scripts/run-demo.ps1 publishes a Release
/// build, writes the session's credentials, and starts the host from the publish folder in the Demo
/// environment. A trainer builds an exercise and closes its first year, a player registers with the
/// generated PIN, and then the host is killed and started again from the same build - the restart
/// a demo box has to survive - and the trainer loads the exercise back and opens year two.
/// </summary>
[Collection(PlaywrightCollection.Name)]
public sealed class PublishedDemoTests : IAsyncLifetime
{
    private readonly string _work = Directory.CreateTempSubdirectory("carbonsim-demo-").FullName;
    private readonly StringBuilder _output = new();
    private Process? _host;
    private IPlaywright? _playwright;
    private IBrowser? _browser;
    private string _baseUrl = string.Empty;

    private string DataDir => Path.Combine(_work, "data");

    private string PublishDir => Path.Combine(_work, "publish");

    public async Task InitializeAsync()
    {
        try
        {
            _playwright = await Playwright.CreateAsync();
            _browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = true });
        }
        catch (PlaywrightException)
        {
            // No browser installed: the attribute on the test reports the skip.
            _browser = null;
        }
    }

    public async Task DisposeAsync()
    {
        await StopHostAsync();

        if (_browser is not null)
        {
            await _browser.DisposeAsync();
        }

        _playwright?.Dispose();

        try
        {
            Directory.Delete(_work, recursive: true);
        }
        catch (IOException)
        {
            // A file the killed host still held is a tidiness problem, not a test failure.
        }
        catch (UnauthorizedAccessException)
        {
            // Likewise.
        }
    }

    [PlaywrightFact]
    public async Task The_launcher_serves_a_demo_that_survives_a_restart()
    {
        IBrowser browser = _browser ?? throw new InvalidOperationException("Chromium was not launched.");

        await StartHostAsync(publish: true);

        // The first run wrote the credentials it generated; the banner and this file agree.
        using JsonDocument credentials = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(DataDir, "credentials.local.json")));
        string adminEmail = credentials.RootElement.GetProperty("adminEmail").GetString()!;
        string adminPassword = credentials.RootElement.GetProperty("adminPassword").GetString()!;
        string pin = credentials.RootElement.GetProperty("registrationPin").GetString()!;
        _output.ToString().Should().Contain(pin, "the banner shows the PIN to hand to players");

        // The trainer builds an exercise from the scenario the publish folder carries, and plays year one to its close.
        IBrowserContext trainerContext = await browser.NewContextAsync();
        IPage trainer = await trainerContext.NewPageAsync();
        await SignInAsync(trainer, adminEmail, adminPassword);
        await trainer.GotoAsync(_baseUrl + "/admin/setup");
        await trainer.WaitUntilInteractiveAsync();
        await trainer.ClickAsync("#admin-create-run");
        await trainer.WaitForURLAsync("**/admin/run/**");
        await trainer.ClickAsync("#admin-begin-year");
        await trainer.WaitForSelectorAsync("#admin-halt");
        (await trainer.Locator("footer.stamp").TextContentAsync()).Should().Contain(BuildInfo.Version);

        // A player registers with the generated PIN and claims a company.
        IBrowserContext playerContext = await browser.NewContextAsync();
        IPage player = await playerContext.NewPageAsync();
        await RegisterAsync(player, pin);

        await trainer.ClickAsync("#admin-halt");
        await trainer.ClickAsync("#admin-end-year");
        await trainer.WaitForSelectorAsync("#admin-begin-next-year");
        await trainerContext.CloseAsync();
        await playerContext.CloseAsync();

        // The box goes down. The same build comes back on the same data.
        await StopHostAsync();
        await StartHostAsync(publish: false);

        IBrowserContext afterContext = await browser.NewContextAsync();
        IPage after = await afterContext.NewPageAsync();
        await SignInAsync(after, adminEmail, adminPassword);
        await after.GotoAsync(_baseUrl + "/admin");
        await after.WaitUntilInteractiveAsync();

        // Nothing is live after a restart; the closed year is on the shelf, and Load brings it back.
        ILocator saved = after.Locator("tr", new PageLocatorOptions { HasTextString = "Vietnam ETS Pilot 2024" });
        await saved.First.Locator("button").First.ClickAsync();
        await after.WaitForFunctionAsync("() => document.body.innerText.includes('Exercise loaded.')");
        await after.Locator("tr", new PageLocatorOptions { HasTextString = "Vietnam ETS Pilot 2024" })
            .Locator("button", new LocatorLocatorOptions { HasTextString = "Open" })
            .First.ClickAsync();
        await after.WaitForURLAsync("**/admin/run/**");
        await after.ClickAsync("#admin-begin-next-year");
        await after.WaitForSelectorAsync("#admin-halt");
        await afterContext.CloseAsync();

        // The session's log is on disk, timestamped, for whoever reads the testers' reports.
        string log = Directory.EnumerateFiles(Path.Combine(DataDir, "logs"), "host-*.log").Should().HaveCount(2).And.Subject.Last();
        (await ReadSharedAsync(log)).Should().MatchRegex(@"\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}\.\d{3} info:");
    }

    private async Task StartHostAsync(bool publish)
    {
        int port = PlaywrightHostFixture.FreePort();
        _baseUrl = $"http://127.0.0.1:{port.ToString(CultureInfo.InvariantCulture)}";

        ProcessStartInfo start = new("powershell")
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };

        foreach (string argument in new[]
        {
            "-NoProfile", "-ExecutionPolicy", "Bypass",
            "-File", Path.Combine(PlaywrightHostFixture.RepositoryRoot(), "scripts", "run-demo.ps1"),
            "-Port", port.ToString(CultureInfo.InvariantCulture),
            "-DataDir", DataDir,
            "-PublishDir", PublishDir,
        })
        {
            start.ArgumentList.Add(argument);
        }

        if (!publish)
        {
            start.ArgumentList.Add("-NoPublish");
        }

        // The launcher sets its own environment; nothing from the test run may leak into the host.
        start.Environment.Remove("ASPNETCORE_ENVIRONMENT");

        _host = Process.Start(start) ?? throw new InvalidOperationException("The launcher could not be started.");
        _host.OutputDataReceived += (_, line) => Append(line.Data);
        _host.ErrorDataReceived += (_, line) => Append(line.Data);
        _host.BeginOutputReadLine();
        _host.BeginErrorReadLine();

        using HttpClient client = new() { BaseAddress = new Uri(_baseUrl) };

        // A Release publish takes a while on the first start.
        for (int attempt = 0; attempt < 600; attempt++)
        {
            if (_host.HasExited)
            {
                throw new InvalidOperationException($"The launcher stopped before the host served: {Captured()}");
            }

            try
            {
                using HttpResponseMessage response = await client.GetAsync("/sign-in");

                if (response.IsSuccessStatusCode)
                {
                    return;
                }
            }
            catch (HttpRequestException)
            {
                // Still publishing or starting.
            }

            await Task.Delay(TimeSpan.FromMilliseconds(500));
        }

        throw new InvalidOperationException($"The demo host did not answer on {_baseUrl} in time: {Captured()}");
    }

    private async Task StopHostAsync()
    {
        if (_host is { HasExited: false })
        {
            _host.Kill(entireProcessTree: true);
            await _host.WaitForExitAsync();
        }

        _host?.Dispose();
        _host = null;
    }

    private async Task SignInAsync(IPage page, string email, string password)
    {
        await page.GotoAsync(_baseUrl + "/sign-in");
        await page.FillAsync("#sign-in-form input[name=email]", email);
        await page.FillAsync("#sign-in-form input[name=password]", password);
        await page.ClickAsync("#sign-in-form button[type=submit]");
        await page.WaitForURLAsync(_baseUrl + "/");
    }

    private async Task RegisterAsync(IPage page, string pin)
    {
        await page.GotoAsync(_baseUrl + "/sign-in");
        await page.FillAsync("#register-form input[name=email]", "demo-player@example.com");
        await page.FillAsync("#register-form input[name=displayName]", "Demo Player");
        await page.FillAsync("#register-form input[name=password]", "mekong-delta-2024");
        await page.FillAsync("#register-form input[name=registrationPin]", pin);
        string company = await page.EvalOnSelectorAsync<string>("#register-form select[name=company]", "select => select.options[0].value");
        await page.SelectOptionAsync("#register-form select[name=company]", company);
        await page.ClickAsync("#register-form button[type=submit]");
        await page.WaitForURLAsync(_baseUrl + "/");
        await page.WaitForSelectorAsync("a.button[href*='/run/']");
    }

    /// <summary>Reads a file the launcher still has open for writing.</summary>
    private static async Task<string> ReadSharedAsync(string path)
    {
        await using FileStream stream = new(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using StreamReader reader = new(stream);

        return await reader.ReadToEndAsync();
    }

    private void Append(string? line)
    {
        if (line is null)
        {
            return;
        }

        lock (_output)
        {
            _output.AppendLine(line);
        }
    }

    private string Captured()
    {
        lock (_output)
        {
            return _output.ToString();
        }
    }
}
