using FluentAssertions;
using Microsoft.Playwright;

namespace CarbonSim.Web.Tests.Components;

/// <summary>
/// The trainer's walk: an administrator signs in, builds an exercise, starts the year, halts
/// trading, closes the year and opens the next, while a player's page watches the same run change
/// under them. It is the only end-to-end proof that the console reaches players through the
/// server rather than only on its own screen.
/// </summary>
public sealed class PlaywrightAdminTests : IClassFixture<PlaywrightHostFixture>
{
    private readonly PlaywrightHostFixture _host;

    public PlaywrightAdminTests(PlaywrightHostFixture host)
    {
        _host = host;
    }

    [PlaywrightFact]
    public async Task An_administrator_drives_a_year_and_a_player_sees_it()
    {
        IBrowser browser = _host.Browser ?? throw new InvalidOperationException("Chromium was not launched.");

        // Separate contexts: the admin's cookie must not sign the player in as the admin.
        IBrowserContext adminContext = await browser.NewContextAsync();
        IBrowserContext playerContext = await browser.NewContextAsync();
        IPage admin = await adminContext.NewPageAsync();
        IPage player = await playerContext.NewPageAsync();

        await _host.AdminSignInAsync(admin);

        // Build a fresh exercise from the configured scenario, then start its first year.
        await admin.GotoAsync(_host.BaseUrl + "/admin/setup");
        await admin.WaitForSelectorAsync("#admin-create-run");
        await admin.ClickAsync("#admin-create-run");
        await admin.WaitForURLAsync("**/admin/run/**");
        await admin.WaitForSelectorAsync("#admin-begin-year");

        string runId = new Uri(admin.Url).AbsolutePath.Trim('/').Split('/')[2];

        await admin.ClickAsync("#admin-begin-year");
        await admin.WaitForSelectorAsync("#admin-pause");

        // The player claims a company and opens the same run.
        await _host.SignInAsync(player, "playwright-admin-player@example.com");
        await player.GotoAsync($"{_host.BaseUrl}/run/{runId}/dashboard");
        await ExpectCurrentYearAsync(player, "Year 1");

        await admin.ClickAsync("#admin-halt");
        await admin.WaitForSelectorAsync("#admin-end-year");
        await player.WaitForFunctionAsync("() => document.body.innerText.includes('Trading halted')");

        await admin.ClickAsync("#admin-end-year");
        await admin.WaitForSelectorAsync("#admin-begin-next-year");
        await player.WaitForFunctionAsync("() => document.body.innerText.includes('Year ended')");

        await admin.ClickAsync("#admin-begin-next-year");
        await admin.WaitForSelectorAsync("#admin-pause");
        await ExpectCurrentYearAsync(player, "Year 2");

        await adminContext.CloseAsync();
        await playerContext.CloseAsync();
    }

    private static Task ExpectCurrentYearAsync(IPage page, string expected)
    {
        expected.Should().NotBeNullOrWhiteSpace();

        return page.WaitForFunctionAsync(
            "expected => document.querySelector('.segments .segment-current')?.textContent.trim() === expected",
            expected);
    }
}
