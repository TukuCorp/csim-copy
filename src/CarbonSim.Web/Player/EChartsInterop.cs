using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace CarbonSim.Web.Player;

/// <summary>
/// Hands an ECharts option to the vendored copy of the library in the browser. The library ships
/// under wwwroot/lib so a training deployment needs no internet; the module is imported once and
/// the chart is created against the element the component holds.
/// </summary>
public sealed class EChartsInterop : ICharts, IAsyncDisposable
{
    private readonly IJSRuntime _js;
    private IJSObjectReference? _module;

    public EChartsInterop(IJSRuntime js)
    {
        _js = js ?? throw new ArgumentNullException(nameof(js));
    }

    public async Task RenderAsync(ElementReference element, string optionJson)
    {
        IJSObjectReference module = await ModuleAsync().ConfigureAwait(false);

        await module.InvokeVoidAsync("render", element, optionJson).ConfigureAwait(false);
    }

    public async Task DisposeAsync(ElementReference element)
    {
        if (_module is null)
        {
            return;
        }

        try
        {
            await _module.InvokeVoidAsync("dispose", element).ConfigureAwait(false);
        }
        catch (JSDisconnectedException)
        {
            // The circuit is gone; the page it drew on has gone with it.
        }
        catch (ObjectDisposedException)
        {
            // Same: nothing left to dispose.
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_module is null)
        {
            return;
        }

        try
        {
            await _module.DisposeAsync().ConfigureAwait(false);
        }
        catch (JSDisconnectedException)
        {
        }
        catch (ObjectDisposedException)
        {
        }
    }

    private async Task<IJSObjectReference> ModuleAsync()
    {
        return _module ??= await _js.InvokeAsync<IJSObjectReference>("import", "./js/charts.js").ConfigureAwait(false);
    }
}
