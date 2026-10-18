// Draws one ECharts option onto one element. The library itself is vendored under
// wwwroot/lib/echarts, so an offline training box renders the same charts as a connected one.

// Vector text: the legend and the axis titles are drawn as real elements rather than into a bitmap,
// so they stay sharp at any zoom and a test can measure whether one lands on the other.
function chartFor(element) {
    return echarts.getInstanceByDom(element) ?? echarts.init(element, null, { renderer: "svg" });
}

export function render(element, optionJson) {
    if (!element || typeof echarts === "undefined") {
        return;
    }

    const chart = chartFor(element);
    chart.setOption(JSON.parse(optionJson), true);
    chart.resize();

    // One handler per element, kept on it so dispose can take it off the window again.
    if (!element.carbonResize) {
        element.carbonResize = () => echarts.getInstanceByDom(element)?.resize();
        window.addEventListener("resize", element.carbonResize);
    }
}

export function dispose(element) {
    if (!element || typeof echarts === "undefined") {
        return;
    }

    if (element.carbonResize) {
        window.removeEventListener("resize", element.carbonResize);
        delete element.carbonResize;
    }

    const chart = echarts.getInstanceByDom(element);

    if (chart) {
        chart.dispose();
    }
}
