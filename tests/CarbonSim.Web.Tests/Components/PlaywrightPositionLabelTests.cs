using System.Globalization;
using FluentAssertions;
using Microsoft.Playwright;

namespace CarbonSim.Web.Tests.Components;

/// <summary>
/// The live check for the T6 regression: a running year whose company holds less than it forecasts
/// emitting must read Short in the top bar. It drives the real host and a real browser, reads the
/// dashboard's own Allowances held and Forecast emissions, and holds the bar's label to them.
/// </summary>
[Collection(PlaywrightCollection.Name)]
public sealed class PlaywrightPositionLabelTests : IClassFixture<PlaywrightHostFixture>
{
    private readonly PlaywrightHostFixture _host;

    public PlaywrightPositionLabelTests(PlaywrightHostFixture host) => _host = host;

    [PlaywrightFact]
    public async Task A_short_company_reads_Short_in_the_top_bar()
    {
        await _host.StartSeededYearAsync();

        IBrowser browser = _host.Browser ?? throw new InvalidOperationException("Chromium was not launched.");
        IBrowserContext context = await browser.NewContextAsync();
        IPage page = await context.NewPageAsync();

        await _host.SignInAsync(page, "playwright-position@example.com");
        string runId = RunId(page);

        await page.GotoAsync($"{_host.BaseUrl}/run/{runId}/dashboard");
        await page.WaitUntilInteractiveAsync();
        await page.WaitForFunctionAsync(
            "() => [...document.querySelectorAll('.topbar dt')].some(dt => dt.textContent.trim() === 'Current position')");

        string[] read = await page.EvaluateAsync<string[]>(
            """
            () => {
              const read = (label) => {
                const dt = [...document.querySelectorAll('dt')].find(d => d.textContent.trim() === label);
                return dt && dt.parentElement.querySelector('dd') ? dt.parentElement.querySelector('dd').textContent.trim() : '';
              };
              const bar = document.querySelector('.topbar');
              const barDt = [...bar.querySelectorAll('dt')].find(d => d.textContent.trim() === 'Current position');
              return [
                read('Allowances held'),
                read('Forecast emissions'),
                barDt && barDt.parentElement.querySelector('dd') ? barDt.parentElement.querySelector('dd').textContent.trim() : '',
              ];
            }
            """);

        decimal held = Number(read[0]);
        decimal forecast = Number(read[1]);
        string position = read[2];

        held.Should().BeLessThan(
            forecast,
            $"the seeded company starts short: held {read[0]} against forecast {read[1]}");

        position.Should().StartWith("Short", "a company holding less than it emits is short, not long");
        position.Should().NotContain("Long");
        Number(position).Should().Be(
            forecast - held,
            $"the bar's figure is the gap between {read[1]} and {read[0]}");

        await context.CloseAsync();
    }

    private static string RunId(IPage page)
    {
        string[] parts = new Uri(page.Url).AbsolutePath.Trim('/').Split('/');

        return parts[1];
    }

    /// <summary>The tonnes in a rendered figure: letters, currency and grouping stripped off.</summary>
    private static decimal Number(string text)
    {
        string digits = new([.. text.Where(character => char.IsDigit(character))]);

        return decimal.Parse(digits, CultureInfo.InvariantCulture);
    }
}

