using FluentAssertions;
using Microsoft.Playwright;

namespace CarbonSim.Web.Tests.Components;

/// <summary>
/// The acceptance walk: signs a player in through the browser and steps through the screens in the
/// order the demo video uses, checking each one arrived. It is the only test that exercises the
/// whole thing together: the Blazor circuit, the stylesheet, the live run, and a real Chromium.
/// </summary>
public sealed class PlaywrightScreenWalkTests : IClassFixture<PlaywrightHostFixture>
{
    private readonly PlaywrightHostFixture _host;

    public PlaywrightScreenWalkTests(PlaywrightHostFixture host)
    {
        _host = host;
    }

    [PlaywrightFact]
    public async Task A_player_walks_the_screens_in_the_demo_order()
    {
        IBrowser browser = RequireBrowser();
        IPage page = await browser.NewPageAsync();

        await _host.SignInAsync(page, "playwright-walk@example.com");

        await page.WaitUntilInteractiveAsync();
        await ExpectHeadingAsync(page, "Dashboard");

        foreach ((string Screen, string Heading) step in Walk)
        {
            await page.ClickAsync($"nav a[href$='/{step.Screen}']");
            await ExpectHeadingAsync(page, step.Heading);
        }

        await page.CloseAsync();
    }

    [PlaywrightFact]
    public async Task No_screen_scrolls_sideways_at_phone_width()
    {
        IBrowser browser = RequireBrowser();
        IPage page = await browser.NewPageAsync(new BrowserNewPageOptions
        {
            ViewportSize = new ViewportSize { Width = 390, Height = 844 },
        });

        await _host.SignInAsync(page, "playwright-phone@example.com");

        foreach ((string Screen, string Heading) step in Walk)
        {
            await page.GotoAsync(_host.BaseUrl + $"/run/{RunId(page)}/{step.Screen}");
            await ExpectHeadingAsync(page, step.Heading);

            double overflow = await page.EvaluateAsync<double>(
                "() => document.documentElement.scrollWidth - document.documentElement.clientWidth");

            overflow.Should().BeLessThanOrEqualTo(1, $"{step.Screen} must not scroll the page sideways on a phone");
        }

        await page.CloseAsync();
    }

    private static readonly (string Screen, string Heading)[] Walk =
    [
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
