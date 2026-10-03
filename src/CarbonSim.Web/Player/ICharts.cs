using Microsoft.AspNetCore.Components;

namespace CarbonSim.Web.Player;

/// <summary>
/// The chart surface the pages use. Real screens hand the ECharts option to the browser; the
/// component tests substitute a recorder, so a chart is asserted on what it was asked to draw.
/// </summary>
public interface ICharts
{
    Task RenderAsync(ElementReference element, string optionJson);

    Task DisposeAsync(ElementReference element);
}
