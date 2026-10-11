using FluentAssertions;
using Microsoft.Playwright;

namespace CarbonSim.Web.Tests.Components;

/// <summary>
/// The trading window in a browser, in a host of its own: halting trading changes the seeded run for
/// every later test that shares a host, so this cannot sit beside the tests that need a live year.
/// </summary>
[Collection(PlaywrightCollection.Name)]
public sealed class PlaywrightTradingWindowTests : IClassFixture<PlaywrightHostFixture>
{
    private readonly PlaywrightHostFixture _host;

    public PlaywrightTradingWindowTests(PlaywrightHostFixture host)
    {
        _host = host;
    }

    [PlaywrightFact]
    public async Task The_trainer_halts_trading_and_a_player_order_is_refused_on_the_screen()
    {
        await _host.StartSeededYearAsync();

        IBrowser browser = _host.Browser ?? throw new InvalidOperationException("Chromium was not launched.");
        IBrowserContext playerContext = await browser.NewContextAsync();
        IBrowserContext adminContext = await browser.NewContextAsync();
        IPage player = await playerContext.NewPageAsync();
        IPage admin = await adminContext.NewPageAsync();

        await _host.SignInAsync(player, "playwright-halt-order@example.com");
        string runId = RunId(player);

        await player.GotoAsync($"{_host.BaseUrl}/run/{runId}/exchange");
        await player.WaitUntilInteractiveAsync();
        await player.FillAsync("#order-volume", "100");

        await _host.AdminSignInAsync(admin);
        await _host.OpenSeededRunAsync(admin);
        await admin.WaitUntilInteractiveAsync();
        await admin.ClickAsync("#admin-halt");
        await admin.WaitForFunctionAsync("() => document.querySelector('#admin-halt') === null");

        // The order form carries no disabled state, so the click goes through and the run refuses it.
        await player.Locator("button", new PageLocatorOptions { HasTextString = "Place order" }).First.ClickAsync();

        await player.WaitForFunctionAsync("() => document.body.innerText.includes('Trading is halted')");
        (await player.TextContentAsync("body")).Should().Contain(
            "Nothing to show yet.",
            "the refused order never reached the book, so the player has no orders");

        await playerContext.CloseAsync();
        await adminContext.CloseAsync();
    }

    private static string RunId(IPage page)
    {
        string[] parts = new Uri(page.Url).AbsolutePath.Trim('/').Split('/');

        return parts[1];
    }
}
