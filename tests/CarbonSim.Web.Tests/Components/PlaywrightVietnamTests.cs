using FluentAssertions;
using Microsoft.Playwright;

namespace CarbonSim.Web.Tests.Components;

/// <summary>
/// The Vietnamese acceptance walk: the language switch turns the screens Vietnamese, and a number a
/// Vietnamese player types the Vietnamese way (a dot grouping the thousands, a comma before the
/// fraction) is understood both by the bid form and by the engine behind an order.
/// </summary>
public sealed class PlaywrightVietnamTests : IClassFixture<PlaywrightHostFixture>
{
    private readonly PlaywrightHostFixture _host;

    public PlaywrightVietnamTests(PlaywrightHostFixture host)
    {
        _host = host;
    }

    [PlaywrightFact]
    public async Task A_Vietnamese_player_reads_the_screens_and_bids_in_Vietnamese_numbers()
    {
        IBrowser browser = _host.Browser ?? throw new InvalidOperationException("Chromium was not launched.");
        IPage page = await browser.NewPageAsync();

        await _host.SignInAsync(page, "playwright-vi@example.com");
        string runId = RunId(page);

        // Switch language the way the top bar does, then land on a fresh page so the circuit starts
        // in Vietnamese rather than only the server-rendered shell.
        await page.GotoAsync($"{_host.BaseUrl}/culture/set?culture=vi&redirect=/run/{runId}/dashboard");
        await ExpectHeadingAsync(page, "Bảng điều khiển");

        // Hydration must not put the English back: after a beat the heading is still Vietnamese and
        // the live figures are grouped the Vietnamese way.
        await page.WaitForTimeoutAsync(1500);
        await ExpectHeadingAsync(page, "Bảng điều khiển");
        (await page.TextContentAsync("body") ?? string.Empty)
            .Should().MatchRegex(@"\d{1,3}(\.\d{3})+", "Vietnamese figures use a dot as the thousands separator");

        // The auction screen speaks Vietnamese and understands a volume of one thousand written 1.000.
        await page.GotoAsync($"{_host.BaseUrl}/run/{runId}/auction");
        await ExpectHeadingAsync(page, "Đấu giá hạn ngạch");
        await page.FillAsync("#bid-volume", "1.000");
        await page.PressAsync("#bid-volume", "Tab");
        await page.WaitForFunctionAsync(
            "() => (document.querySelector('#bid-capital-cost')?.textContent ?? '').includes('100.000')",
            new PageWaitForFunctionOptions { Timeout = 15000 });

        // An offer written in Vietnamese numbers is accepted by the engine and rests in my offers.
        await page.GotoAsync($"{_host.BaseUrl}/run/{runId}/otc");
        await ExpectHeadingAsync(page, "Thị trường OTC");
        await page.FillAsync("#offer-volume", "1.500");
        await page.FillAsync("#offer-price", "120,25");
        await page.ClickAsync("#send-offer");
        await page.WaitForFunctionAsync(
            "() => (document.querySelector('#my-offers')?.textContent ?? '').includes('1.500')",
            new PageWaitForFunctionOptions { Timeout = 15000 });

        await page.CloseAsync();
    }

    private static string RunId(IPage page)
    {
        string[] parts = new Uri(page.Url).AbsolutePath.Trim('/').Split('/');

        return parts[1];
    }

    private static Task ExpectHeadingAsync(IPage page, string heading) =>
        page.WaitForFunctionAsync(
            "expected => document.querySelector('h1') && document.querySelector('h1').textContent.trim() === expected",
            heading);
}
