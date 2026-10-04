using System.Text.Json;
using FluentAssertions;
using Microsoft.Playwright;

namespace CarbonSim.Web.Tests.Components;

/// <summary>
/// The Phase 6 accessibility gate. axe-core (vendored under Compliance/, so no CDN and no network)
/// is injected into each real screen and run against the WCAG 2.0 and 2.1 A/AA rules, which covers
/// the label, contrast, landmark and name/role/value basics. It complements the structural audit -
/// keyboard reachability, live regions and the charts' text alternatives - with a machine check on
/// every screen a player or trainer opens.
/// </summary>
public sealed class PlaywrightAccessibilityTests : IClassFixture<PlaywrightHostFixture>
{
    private static readonly string AxePath = Path.Combine(AppContext.BaseDirectory, "Compliance", "axe.min.js");

    private static readonly (string Screen, string Heading)[] PlayerScreens =
    [
        ("dashboard", "Dashboard"),
        ("abatement", "Abatement"),
        ("auction", "Allowance auction"),
        ("exchange", "Exchange market"),
        ("otc", "OTC market"),
        ("system", "System info"),
        ("leaderboard", "Leaderboard"),
        ("company", "Company management"),
        ("units", "Unit information"),
        ("surrender", "Surrender and banking"),
        ("messages", "Messages"),
    ];

    private readonly PlaywrightHostFixture _host;

    public PlaywrightAccessibilityTests(PlaywrightHostFixture host)
    {
        _host = host;
    }

    [PlaywrightFact]
    public async Task The_player_screens_have_no_axe_violations()
    {
        IBrowser browser = RequireBrowser();
        IBrowserContext context = await browser.NewContextAsync();
        IPage page = await context.NewPageAsync();

        await _host.SignInAsync(page, "axe-player@example.com");
        string runId = RunId(page);
        List<string> problems = [];

        foreach ((string screen, string heading) in PlayerScreens)
        {
            await page.GotoAsync(_host.BaseUrl + "/run/" + runId + "/" + screen);
            await ExpectHeadingAsync(page, heading);
            problems.AddRange(await ViolationsAsync(page, "player/" + screen));
        }

        await context.CloseAsync();

        problems.Should().BeEmpty("every player screen must pass the WCAG 2.1 AA axe rules:" + Environment.NewLine + Describe(problems));
    }

    [PlaywrightFact]
    public async Task The_admin_screens_have_no_axe_violations()
    {
        IBrowser browser = RequireBrowser();
        IBrowserContext context = await browser.NewContextAsync();
        IPage page = await context.NewPageAsync();

        await _host.AdminSignInAsync(page);
        List<string> problems = [.. await ViolationsAsync(page, "admin/runs")];

        await page.GotoAsync(_host.BaseUrl + "/admin/setup");
        await page.WaitForSelectorAsync("#admin-create-run");
        problems.AddRange(await ViolationsAsync(page, "admin/setup"));

        await page.ClickAsync("#admin-create-run");
        await page.WaitForURLAsync("**/admin/run/**");
        await page.WaitForSelectorAsync("#admin-begin-year");
        string runId = new Uri(page.Url).AbsolutePath.Trim('/').Split('/')[2];

        foreach (string screen in new[] { "run", "reports", "surrender" })
        {
            string path = screen == "run" ? "/admin/run/" + runId : "/admin/run/" + runId + "/" + screen;
            await page.GotoAsync(_host.BaseUrl + path);
            await page.WaitForSelectorAsync("main h1");
            problems.AddRange(await ViolationsAsync(page, "admin/" + screen));
        }

        await context.CloseAsync();

        problems.Should().BeEmpty("every admin screen must pass the WCAG 2.1 AA axe rules:" + Environment.NewLine + Describe(problems));
    }

    [PlaywrightFact]
    public async Task The_admin_screens_do_not_scroll_sideways_at_phone_width()
    {
        IBrowser browser = RequireBrowser();
        IBrowserContext context = await browser.NewContextAsync(new BrowserNewContextOptions
        {
            ViewportSize = new ViewportSize { Width = 390, Height = 844 },
        });
        IPage page = await context.NewPageAsync();

        await _host.AdminSignInAsync(page);
        (await PageOverflowAsync(page)).Should().BeLessThanOrEqualTo(1, "the admin run list must fit a phone");

        await page.GotoAsync(_host.BaseUrl + "/admin/setup");
        await page.WaitForSelectorAsync("#admin-create-run");
        (await PageOverflowAsync(page)).Should().BeLessThanOrEqualTo(1, "the setup screen must fit a phone");

        await page.ClickAsync("#admin-create-run");
        await page.WaitForURLAsync("**/admin/run/**");
        await page.WaitForSelectorAsync("#admin-begin-year");
        string runId = new Uri(page.Url).AbsolutePath.Trim('/').Split('/')[2];

        foreach (string screen in new[] { "run", "reports", "surrender" })
        {
            string path = screen == "run" ? "/admin/run/" + runId : "/admin/run/" + runId + "/" + screen;
            await page.GotoAsync(_host.BaseUrl + path);
            await page.WaitForSelectorAsync("main h1");
            (await PageOverflowAsync(page)).Should().BeLessThanOrEqualTo(1, "admin/" + screen + " must fit a phone");
        }

        await context.CloseAsync();
    }

    private static string Describe(IReadOnlyList<string> problems) =>
        problems.Count == 0 ? "no violations" : string.Join(Environment.NewLine, problems);

    private static async Task<double> PageOverflowAsync(IPage page) =>
        await page.EvaluateAsync<double>("() => document.documentElement.scrollWidth - document.documentElement.clientWidth");

    /// <summary>Runs axe over the current page and returns one line per violating node.</summary>
    private static async Task<IReadOnlyList<string>> ViolationsAsync(IPage page, string screen)
    {
        await page.AddScriptTagAsync(new PageAddScriptTagOptions { Path = AxePath });

        string json = await page.EvaluateAsync<string>(
            """
            async () => {
              const results = await axe.run(document, {
                runOnly: { type: 'tag', values: ['wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa'] }
              });
              return JSON.stringify(results.violations.map(v => ({
                id: v.id,
                impact: v.impact,
                help: v.help,
                nodes: v.nodes.slice(0, 4).map(n => n.target.join(' '))
              })));
            }
            """);

        using JsonDocument document = JsonDocument.Parse(json);
        List<string> problems = [];

        foreach (JsonElement violation in document.RootElement.EnumerateArray())
        {
            string id = violation.GetProperty("id").GetString() ?? "?";
            string impact = violation.GetProperty("impact").GetString() ?? "?";
            string help = violation.GetProperty("help").GetString() ?? "?";
            IEnumerable<string> nodes = violation.GetProperty("nodes").EnumerateArray().Select(node => node.GetString() ?? "?");

            problems.Add(screen + ": " + id + " (" + impact + ") " + help + " -> " + string.Join("; ", nodes));
        }

        return problems;
    }

    private IBrowser RequireBrowser() =>
        _host.Browser ?? throw new InvalidOperationException("Chromium was not launched.");

    private static string RunId(IPage page)
    {
        string[] parts = new Uri(page.Url).AbsolutePath.Trim('/').Split('/');

        return parts[1];
    }

    private static Task ExpectHeadingAsync(IPage page, string heading)
    {
        return page.WaitForFunctionAsync(
            "expected => document.querySelector('h1') && document.querySelector('h1').textContent.trim() === expected",
            heading);
    }
}
