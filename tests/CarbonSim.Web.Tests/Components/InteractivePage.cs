using Microsoft.Playwright;

namespace CarbonSim.Web.Tests.Components;

/// <summary>
/// Blazor sends the prerendered markup before the circuit is connected and only attaches its event
/// handlers on the first interactive render. A click, or a value typed into an <c>@bind</c> box,
/// issued in between is dropped without an error, because the page still shows the prerendered
/// markup. The layouts mark that first interactive render with <c>data-interactive="true"</c>, so a
/// Playwright step waits for it before it drives the page.
/// </summary>
internal static class InteractivePage
{
    public static Task WaitUntilInteractiveAsync(this IPage page)
    {
        ArgumentNullException.ThrowIfNull(page);

        return page.WaitForSelectorAsync(
            "[data-interactive='true']",
            new PageWaitForSelectorOptions { State = WaitForSelectorState.Attached });
    }
}
