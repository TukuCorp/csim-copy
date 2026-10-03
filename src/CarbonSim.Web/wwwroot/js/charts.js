// Draws one ECharts option onto one element. The library itself is vendored under
// wwwroot/lib/echarts, so an offline training box renders the same charts as a connected one.

function chartFor(element) {
    return echarts.getInstanceByDom(element) ?? echarts.init(element);
}

export function render(element, optionJson) {
    if (!element || typeof echarts === "undefined") {
        return;
    }

    const chart = chartFor(element);
    chart.setOption(JSON.parse(optionJson), true);
    chart.resize();

    if (!element.dataset.carbonResizeBound) {
        element.dataset.carbonResizeBound = "true";
        window.addEventListener("resize", () => chartFor(element).resize());
    }
}

export function dispose(element) {
    if (!element || typeof echarts === "undefined") {
        return;
    }

    const chart = echarts.getInstanceByDom(element);

    if (chart) {
        chart.dispose();
    }
}
