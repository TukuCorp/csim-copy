using System.Globalization;
using System.Text.RegularExpressions;
using FluentAssertions;
using Microsoft.Playwright;

namespace CarbonSim.Web.Tests.Components;

/// <summary>
/// QA's second pass, in a browser: the language switch labels each language in its own and keeps
/// the page it was pressed on, the waiting screen knows an exercise is under way, and the exchange
/// screen reads as the three markets it summarises.
/// </summary>
[Collection(PlaywrightCollection.Name)]
public sealed class PlaywrightFeedbackTests : IClassFixture<PlaywrightHostFixture>
{
    private readonly PlaywrightHostFixture _host;

    public PlaywrightFeedbackTests(PlaywrightHostFixture host)
    {
        _host = host;
    }

    [PlaywrightFact]
    public async Task The_language_switch_keeps_the_page_and_names_each_language_in_its_own()
    {
        await _host.StartSeededYearAsync();

        IBrowser browser = _host.Browser ?? throw new InvalidOperationException("Chromium was not launched.");
        // A context of its own: the language cookie a switch writes must not reach another test.
        IBrowserContext context = await browser.NewContextAsync();
        IPage page = await context.NewPageAsync();

        await _host.SignInAsync(page, "playwright-feedback-vi@example.com");
        string runId = RunId(page);

        await page.GotoAsync($"{_host.BaseUrl}/run/{runId}/exchange");
        await page.WaitUntilInteractiveAsync();

        // Both names are written in their own language, whatever the screen is set to.
        (await page.TextContentAsync(".lang a[lang='en']")).Should().Contain("English");
        (await page.TextContentAsync(".lang a[lang='vi']")).Should().Contain("Tiếng Việt");

        // And they are readable on the navy bar rather than the link blue falling through from the
        // one global a rule, which is what QA measured at about 3:1 and underlined.
        string[] painted = await page.EvaluateAsync<string[]>(
            "() => { const link = document.querySelector('.topbar .lang a');"
                + " const style = getComputedStyle(link);"
                + " return [style.color, style.textDecorationLine, getComputedStyle(link.closest('.topbar')).backgroundColor]; }");

        Contrast(painted[0], painted[2]).Should().BeGreaterThanOrEqualTo(4.5,
            $"the switch is light text on the dark top bar, not blue on navy ({painted[0]} on {painted[2]})");
        painted[1].Should().NotContain("underline", "the switch underlines only when pointed at");

        await page.ClickAsync(".lang a[lang='vi']");

        // The switch used to land on the waiting screen because its redirect was double-encoded.
        await page.WaitForURLAsync($"**/run/{runId}/exchange");
        await page.WaitForFunctionAsync(
            "() => document.querySelector('h1')?.textContent.trim() === 'Thị trường giao dịch qua sàn'");
        (await page.TextContentAsync(".lang a[lang='en']")).Should().Contain("English");

        await context.CloseAsync();
    }

    [PlaywrightFact]
    public async Task The_chart_legend_clears_the_axis_title_at_phone_width()
    {
        await _host.StartSeededYearAsync();

        IBrowser browser = _host.Browser ?? throw new InvalidOperationException("Chromium was not launched.");
        IBrowserContext context = await browser.NewContextAsync(new BrowserNewContextOptions
        {
            ViewportSize = new ViewportSize { Width = 390, Height = 844 },
        });
        IPage page = await context.NewPageAsync();

        await _host.SignInAsync(page, "playwright-legend@example.com");
        string runId = RunId(page);

        await page.GotoAsync($"{_host.BaseUrl}/run/{runId}/exchange");
        await page.WaitUntilInteractiveAsync();
        await page.WaitForSelectorAsync(".chart svg text", new PageWaitForSelectorOptions { Timeout = 30000 });

        string[] overlaps = await page.EvaluateAsync<string[]>(LegendOverlaps);

        overlaps.Should().BeEmpty("at 390px a wrapped legend must sit above the axis, not on it");

        await context.CloseAsync();
    }

    public async Task The_waiting_screen_knows_the_run_is_under_way_and_the_exchange_reads_as_three_markets()
    {
        await _host.StartSeededYearAsync();

        IBrowser browser = _host.Browser ?? throw new InvalidOperationException("Chromium was not launched.");
        IBrowserContext context = await browser.NewContextAsync();
        IPage page = await context.NewPageAsync();

        await _host.SignInAsync(page, "playwright-feedback-screens@example.com");
        string runId = RunId(page);

        await page.GotoAsync(_host.BaseUrl + "/");
        await page.WaitForFunctionAsync("() => document.body.innerText.includes('Simulation under way')");

        await page.GotoAsync($"{_host.BaseUrl}/run/{runId}/exchange");
        await page.WaitUntilInteractiveAsync();

        foreach (string heading in new[] { "Allowance auction", "Exchange market", "OTC market" })
        {
            ILocator group = page.Locator("h3", new PageLocatorOptions { HasTextString = heading });

            (await group.CountAsync()).Should().Be(1, $"the strip groups {heading}");
        }

        (await page.TextContentAsync("body")).Should().Contain("OTC best price");

        await context.CloseAsync();
    }

    private static string RunId(IPage page)
    {
        string[] parts = new Uri(page.Url).AbsolutePath.Trim('/').Split('/');

        return parts[1];
    }

    /// <summary>
    /// Every legend label that lands on the axis title or reaches down into the plot. The charts
    /// draw to SVG (wwwroot/js/charts.js), so the legend and the axis title are elements that can be
    /// measured; the labels ECharts puts in the legend come from the chart's own option, so the
    /// legend's own page counter ("1/2", a scroll legend's furniture) is not mistaken for one.
    /// </summary>
    private const string LegendOverlaps =
        """
        () => {
          const bad = [];
          for (const chart of document.querySelectorAll('.chart')) {
            const instance = window.echarts && window.echarts.getInstanceByDom(chart);
            const option = instance && instance.getOption();
            if (!option) continue;
            const names = ((option.legend && option.legend[0] && option.legend[0].data) || []).map(entry => entry && entry.name ? entry.name : entry);
            if (names.length === 0) continue;
            const texts = [...chart.querySelectorAll('svg text')];
            const legend = texts.filter(text => names.includes(text.textContent.trim()));
            if (legend.length === 0) continue;
            const tick = texts
              .filter(text => /^-?[0-9][0-9,.]*$/.test(text.textContent.trim()))
              .map(text => text.getBoundingClientRect())
              .reduce((highest, box) => Math.min(highest, box.top), Infinity);
            const titleName = option.yAxis && option.yAxis[0] && option.yAxis[0].name;
            const title = titleName ? texts.find(text => text.textContent.trim() === titleName) : null;
            const titleBox = title ? title.getBoundingClientRect() : null;
            const overlaps = (one, two) =>
              Math.min(one.right, two.right) - Math.max(one.left, two.left) > 0.5
                && Math.min(one.bottom, two.bottom) - Math.max(one.top, two.top) > 0.5;
            for (const label of legend) {
              const box = label.getBoundingClientRect();
              const name = label.textContent.trim();
              if (titleBox && overlaps(box, titleBox)) bad.push('legend "' + name + '" lands on the axis title');
              if (box.bottom > tick + 0.5) {
                bad.push('legend "' + name + '" reaches into the plot (bottom ' + box.bottom.toFixed(1) + ' past ' + tick.toFixed(1) + ')');
              }
            }
          }
          return bad;
        }
        """;

    /// <summary>The WCAG contrast ratio between two colours the browser computed as rgb().</summary>
    private static double Contrast(string foreground, string background)
    {
        static double Channel(double value) =>
            value <= 0.03928 ? value / 12.92 : Math.Pow((value + 0.055) / 1.055, 2.4);

        static double Luminance(string colour)
        {
            double[] parts =
            [
                .. Regex.Matches(colour, "[0-9.]+").Select(match => double.Parse(match.Value, CultureInfo.InvariantCulture)),
            ];

            return (0.2126 * Channel(parts[0] / 255d))
                + (0.7152 * Channel(parts[1] / 255d))
                + (0.0722 * Channel(parts[2] / 255d));
        }

        double lighter = Math.Max(Luminance(foreground), Luminance(background));
        double darker = Math.Min(Luminance(foreground), Luminance(background));

        return (lighter + 0.05) / (darker + 0.05);
    }

    [PlaywrightFact]
    public async Task Each_console_action_reaches_the_players_screen()
    {
        await _host.StartSeededYearAsync();

        IBrowser browser = _host.Browser ?? throw new InvalidOperationException("Chromium was not launched.");
        IBrowserContext playerContext = await browser.NewContextAsync();
        IBrowserContext adminContext = await browser.NewContextAsync();
        IPage player = await playerContext.NewPageAsync();
        IPage admin = await adminContext.NewPageAsync();

        await _host.SignInAsync(player, "playwright-console-action@example.com");
        string company = (await player.TextContentAsync(".topbar .company") ?? string.Empty).Trim();

        company.Should().NotBeNullOrWhiteSpace();

        await _host.AdminSignInAsync(admin);
        await _host.OpenSeededRunAsync(admin);
        await admin.WaitUntilInteractiveAsync();

        // 1. A fine takes money out of the player's company.
        decimal before = await MoneyAsync(player, "Capital");

        await SelectByTextAsync(admin, "#fine-unit", company);
        await admin.FillAsync("#fine-amount", "500000");
        await admin.FillAsync("#fine-description", "late monitoring report");
        await ClickAsync(admin, "Fine a unit");
        (await NoticeAsync(admin, "#fine-amount")).Should().Contain("500,000");

        await player.ReloadAsync();

        (await MoneyAsync(player, "Capital")).Should().Be(
            before - 500_000m, "the fine is charged to the company the unit belongs to");

        // 2. Handing out offsets shows up in the player's holdings, on top of the offsets the
        //    scenario granted with the year's allocation.
        await player.GotoAsync($"{_host.BaseUrl}/run/{RunId(player)}/surrender");
        await player.WaitUntilInteractiveAsync();
        decimal offsetsBefore = await OffsetsHeldAsync(player);

        await SelectByTextAsync(admin, "#offset-unit", company);
        await admin.FillAsync("#offset-volume", "100000");
        await ClickAsync(admin, "Hand out offsets");
        (await NoticeAsync(admin, "#offset-volume")).Should().Contain("100,000");

        string expected = (offsetsBefore + 100_000m).ToString("N0", CultureInfo.GetCultureInfo("en-US"));
        await player.WaitForFunctionAsync(
            "expected => [...document.querySelectorAll('table.grid tr')].some(row => row.innerText.startsWith('Offset') && row.innerText.includes(expected))",
            expected,
            new PageWaitForFunctionOptions { Timeout = 30000 });

        // 3. An abatement committed for the player's unit appears on the abatement screen.
        await SelectByTextAsync(admin, "#abatement-unit", company);
        await ClickAsync(admin, "Add abatement for a unit");

        string abatementNotice = await NoticeAsync(admin, "#abatement-unit");

        abatementNotice.Should().Contain("Committed");

        await player.GotoAsync($"{_host.BaseUrl}/run/{RunId(player)}/abatement");
        await player.WaitUntilInteractiveAsync();
        await player.WaitForFunctionAsync(
            "() => document.body.innerText.includes('Building') || document.body.innerText.includes('Operating')",
            null,
            new PageWaitForFunctionOptions { Timeout = 30000 });

        // 4. An end-of-year modification moves the emissions the player forecasts.
        await player.GotoAsync($"{_host.BaseUrl}/run/{RunId(player)}/dashboard");
        await player.WaitUntilInteractiveAsync();

        decimal forecast = await MoneyAsync(player, "Forecast emissions");

        await SelectByTextAsync(admin, "#shock-unit", company);
        await admin.FillAsync("#shock-year", "1");
        await admin.FillAsync("#shock-tonnes", "50000");
        await ClickAsync(admin, "Apply");
        (await NoticeAsync(admin, "#shock-tonnes")).Should().Contain("50,000");

        await player.ReloadAsync();
        await player.WaitUntilInteractiveAsync();

        (await MoneyAsync(player, "Forecast emissions")).Should().Be(
            forecast + 50_000m, "the modification raised what the unit is forecast to emit");

        // 5. Switching messaging off stops the player sending.
        await admin.ClickAsync("#admin-messaging-toggle");
        await admin.WaitForFunctionAsync(
            "() => document.querySelector('#admin-messaging-state')?.textContent.trim() === 'Off'",
            null,
            new PageWaitForFunctionOptions { Timeout = 30000 });

        await player.GotoAsync($"{_host.BaseUrl}/run/{RunId(player)}/messages");
        await player.WaitUntilInteractiveAsync();
        await player.WaitForFunctionAsync("() => document.querySelector('#message-text')?.disabled === true");

        await playerContext.CloseAsync();
        await adminContext.CloseAsync();
    }

    /// <summary>Picks the option whose text carries the name the trainer is looking for.</summary>
    private static async Task SelectByTextAsync(IPage page, string selector, string text)
    {
        // The selects arrive with the interactive render, so wait for them to have their options.
        await page.WaitForFunctionAsync(
            "selector => document.querySelectorAll(selector + ' option').length > 0",
            selector,
            new PageWaitForFunctionOptions { Timeout = 30000 });

        string? value = await page.EvaluateAsync<string?>(
            "([selector, text]) => [...document.querySelectorAll(selector + ' option')].find(option => option.text.includes(text))?.value",
            new[] { selector, text });

        if (string.IsNullOrWhiteSpace(value))
        {
            string options = await page.EvaluateAsync<string>(
                "selector => [...document.querySelectorAll(selector + ' option')].slice(0, 4).map(option => option.text).join(' | ')",
                selector);

            value.Should().NotBeNullOrWhiteSpace($"{selector} has an option for {text}; it offers {options}");
        }

        await page.SelectOptionAsync(selector, value!);
    }

    private static Task ClickAsync(IPage page, string caption) =>
        page.Locator("button", new PageLocatorOptions { HasTextString = caption }).First.ClickAsync();

    /// <summary>What the form that holds this control has to say for itself.</summary>
    private static async Task<string> NoticeAsync(IPage page, string control)
    {
        ILocator notice = page.Locator($".card:has({control}) .notice").First;

        await notice.WaitForAsync();

        return await notice.TextContentAsync() ?? string.Empty;
    }

    /// <summary>The number a named metric shows, with its grouping and currency stripped off.</summary>
    /// <summary>The offsets the holdings table shows as held, zero when the company has none.</summary>
    private static async Task<decimal> OffsetsHeldAsync(IPage page)
    {
        string held = await page.EvaluateAsync<string>(
            "() => { const row = [...document.querySelectorAll('table.grid tr')].find(row => row.innerText.startsWith('Offset'));"
                + " const cells = row ? [...row.querySelectorAll('td')] : []; return cells.length ? cells[cells.length - 1].textContent.replace(/[^0-9.]/g, '') : '0'; }");

        return decimal.Parse(held, CultureInfo.InvariantCulture);
    }

    private static async Task<decimal> MoneyAsync(IPage page, string label)
    {
        string[] found = await page.EvaluateAsync<string[]>(
            "label => { const wanted = label.toLowerCase();"
                + " const labels = [...document.querySelectorAll('dt')].map(dt => dt.textContent.trim());"
                + " const row = [...document.querySelectorAll('dt')].find(dt => dt.textContent.trim().toLowerCase().startsWith(wanted));"
                + " const text = row?.parentElement?.querySelector('dd')?.textContent ?? '';"
                + " return [text.replace(/[^0-9.]/g, ''), labels.join(' | ')]; }",
            label);

        found[0].Should().NotBeNullOrWhiteSpace(
            $"the {label} metric is on the page with a number; its labels are: {found[1]}");

        return decimal.Parse(found[0], System.Globalization.CultureInfo.InvariantCulture);
    }
}
