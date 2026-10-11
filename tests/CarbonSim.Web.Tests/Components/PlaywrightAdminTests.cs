using FluentAssertions;
using Microsoft.Playwright;

namespace CarbonSim.Web.Tests.Components;

/// <summary>
/// The trainer's walk: an administrator signs in, builds an exercise, starts the year, halts
/// trading, closes the year and opens the next, while a player's page watches the same run change
/// under them. It is the only end-to-end proof that the console reaches players through the
/// server rather than only on its own screen.
/// </summary>
[Collection(PlaywrightCollection.Name)]
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
        await admin.WaitUntilInteractiveAsync();
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
        await player.WaitUntilInteractiveAsync();
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

    [PlaywrightFact]
    public async Task The_seeded_exercise_waits_for_its_first_year_and_then_runs()
    {
        IBrowser browser = _host.Browser ?? throw new InvalidOperationException("Chromium was not launched.");
        IBrowserContext context = await browser.NewContextAsync();
        IPage admin = await context.NewPageAsync();

        // The public waiting screen reports the exercise as it stands before anybody drives it:
        // waiting to start, on year one - not a run whose clock has already run out of time.
        using (HttpClient guest = new() { BaseAddress = new Uri(_host.BaseUrl) })
        {
            string waiting = await guest.GetStringAsync("/");

            waiting.Should().Contain(PlaywrightHostFixture.SeededRunName);
            waiting.Should().Contain("Human companies");
            waiting.Should().Contain("Waiting to start");
            waiting.Should().NotContain("Trading halted");
            // Another walk in this class may already have started an exercise of its own, and then
            // the heading says the simulation is under way; either way this is not a halt.
            waiting.Should().ContainAny("Waiting for the simulation to start", "Simulation under way");
        }

        await _host.AdminSignInAsync(admin);
        await _host.OpenSeededRunAsync(admin);

        await ExpectMetricAsync(admin, "State", "Waiting to start");
        await ExpectMetricAsync(admin, "Year", "1 / 3");
        await ExpectMetricAsync(admin, "Auction", "1 / 4");
        await ExpectMetricAsync(admin, "Time left in the year", "20m 00s");

        // Starting the year is the trainer's control, and the countdowns move from there.
        await admin.ClickAsync("#admin-begin-year");
        await admin.WaitForSelectorAsync("#admin-pause");

        await ExpectMetricAsync(admin, "State", "Running");
        await ExpectMetricChangedAsync(admin, "Time left in the year", "20m 00s");

        await context.CloseAsync();
    }

    /// <summary>Waits for one metric on the console's clock card to read exactly this.</summary>
    private static Task ExpectMetricAsync(IPage page, string label, string value) =>
        page.WaitForFunctionAsync(
            "([label, value]) => [...document.querySelectorAll('dl.metrics > div')].some("
                + "row => row.querySelector('dt')?.textContent.trim() === label"
                + " && row.querySelector('dd')?.textContent.trim() === value)",
            new[] { label, value },
            new PageWaitForFunctionOptions { Timeout = 30000 });

    /// <summary>Waits for one metric to move off the value it held.</summary>
    private static Task ExpectMetricChangedAsync(IPage page, string label, string was) =>
        page.WaitForFunctionAsync(
            "([label, was]) => [...document.querySelectorAll('dl.metrics > div')].some("
                + "row => row.querySelector('dt')?.textContent.trim() === label"
                + " && row.querySelector('dd')?.textContent.trim() !== was)",
            new[] { label, was },
            new PageWaitForFunctionOptions { Timeout = 30000 });

}
