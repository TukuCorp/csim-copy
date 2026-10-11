using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CarbonSim.Web.Player;

/// <summary>
/// Builds the ECharts option for each chart the player screens show. Options are plain JSON built
/// from these records, so a chart never reaches into the engine and the option is easy to assert on.
/// </summary>
public static class Charts
{
    private const string Blue = "#3f7fbf";
    private const string Green = "#2f6f4f";
    private const string Red = "#c0392b";
    private const string Amber = "#d68910";

    /// <summary>
    /// One colour per product on the price chart, so a legend swatch identifies its series. The
    /// floor line keeps the amber.
    /// </summary>
    private static readonly string[] ProductColours = ["#2f6fbf", "#2f6f4f", "#6c4bb6", "#c0392b"];

    /// <summary>Distinct line styles for the close of each product, for readers who cannot tell the colours apart.</summary>
    private static readonly string[] CloseStyles = ["solid", "dashed", "dotted"];

    private static readonly JsonSerializerOptions Options = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    /// <summary>
    /// The marginal abatement cost curve: every open project as a bar as wide as the tonnes it
    /// would abate and as tall as it costs per tonne, cheapest first, with the gap the company
    /// still has to cover drawn across the chart. The width is the point of the curve - a project
    /// that abates fifteen times as much is fifteen times as wide - so each project is its own
    /// one-bar series, which is what ECharts needs to give a bar a width of its own.
    /// </summary>
    public static string Macc(
        IReadOnlyList<AbatementOpportunityView> opportunities,
        decimal shortfall,
        string shortfallLabel)
    {
        ArgumentNullException.ThrowIfNull(opportunities);

        List<AbatementOpportunityView> open = [.. opportunities.Where(opportunity => !opportunity.Implemented)];
        List<AbatementOpportunityView> ordered =
        [
            .. (open.Count > 0 ? open : opportunities).OrderBy(opportunity => opportunity.CostPerTonne),
        ];

        decimal widest = ordered.Count == 0 ? 0m : ordered.Max(opportunity => opportunity.AnnualReduction);
        decimal total = ordered.Sum(opportunity => opportunity.AnnualReduction);
        List<object> series = [];
        decimal start = 0m;

        foreach (AbatementOpportunityView option in ordered)
        {
            // The widest project gets 120px and the rest share that scale, so the widths keep their
            // proportion whatever the chart is drawn at.
            int width = widest <= 0m
                ? 8
                : Math.Max(3, (int)Math.Round((double)(option.AnnualReduction / widest) * 120d));
            decimal middle = start + (option.AnnualReduction / 2m);

            series.Add(new
            {
                name = string.Create(
                    CultureInfo.InvariantCulture,
                    $"{option.Code} {option.Name} - {option.AnnualReduction:N0} t at {option.CostPerTonne:N0} per tonne"),
                type = "bar",
                barWidth = width,
                data = new object[] { new { value = new[] { middle, option.CostPerTonne } } },
                itemStyle = new { color = option.CostPerTonne <= 0m ? Blue : Green },
            });

            start += option.AnnualReduction;
        }

        // The company's own gap is the line to read the curve against: everything to its left is
        // the cheapest way to close it. It rides on a series of its own with nothing in it, so the
        // projects stay plain one-bar series.
        series.Add(new
        {
            name = shortfallLabel,
            type = "line",
            data = Array.Empty<decimal>(),
            silent = true,
            symbol = "none",
            markLine = new
            {
                symbol = "none",
                silent = true,
                lineStyle = new { type = "dashed", color = Red },
                label = new { formatter = $"{shortfallLabel} {{c}}", position = "insideEndTop" },
                data = new object[] { new { xAxis = shortfall } },
            },
        });

        return JsonSerializer.Serialize(
            new
            {
                // Room at the top for the axis title: on a phone the plot would otherwise start on it.
                grid = new { left = 68, right = 24, top = 44, bottom = 44 },
                // Each project carries what it abates in its own name, so the tooltip has nothing to
                // look up: the axis reads in tonnes and the bar says which project it is.
                tooltip = new { trigger = "item", formatter = "{a}" },
                xAxis = new
                {
                    type = "value",
                    name = "tonnes",
                    min = 0,
                    max = Math.Max(total, shortfall),
                    axisLabel = new { fontSize = 10 },
                },
                yAxis = new { type = "value", name = "per tonne", scale = true },
                series,
            },
            Options);
    }

    /// <summary>Operating profit with and without the abatement the company has committed to.</summary>
    public static string NetProfit(decimal normalOperatingProfit, decimal withAbatement, string normalLabel, string abatementLabel)
    {
        return JsonSerializer.Serialize(
            new
            {
                grid = new { left = 168, right = 24, top = 16, bottom = 28 },
                tooltip = new { trigger = "axis", axisPointer = new { type = "shadow" } },
                xAxis = new { type = "value", axisLabel = new { show = false }, splitLine = new { show = false } },
                yAxis = new
                {
                    type = "category",
                    data = new[] { abatementLabel, normalLabel },
                    axisLabel = new { fontSize = 10, width = 150, overflow = "break" },
                },
                series = new object[]
                {
                    new
                    {
                        type = "bar",
                        data = new[] { withAbatement, normalOperatingProfit },
                        itemStyle = new { color = Blue },
                        barMaxWidth = 28,
                    },
                },
            },
            Options);
    }

    /// <summary>What the company holds against the year, and the gap it still has to cover.</summary>
    public static string LongShort(PositionView position, string company, string heldLabel, string shortLabel)
    {
        ArgumentNullException.ThrowIfNull(position);

        decimal shortfall = position.Shortfall > 0m ? position.Shortfall : 0m;

        return JsonSerializer.Serialize(
            new
            {
                grid = new { left = 16, right = 24, top = 34, bottom = 28 },
                tooltip = new { trigger = "axis", axisPointer = new { type = "shadow" } },
                legend = new { data = new[] { heldLabel, shortLabel }, top = 0, textStyle = new { fontSize = 11 } },
                xAxis = new { type = "value", axisLabel = new { show = false }, splitLine = new { show = false } },
                yAxis = new { type = "category", data = new[] { company }, axisLabel = new { show = false } },
                series = new object[]
                {
                    new
                    {
                        name = heldLabel,
                        type = "bar",
                        stack = "position",
                        data = new[] { position.Held },
                        itemStyle = new { color = Blue },
                    },
                    new
                    {
                        name = shortLabel,
                        type = "bar",
                        stack = "position",
                        data = new[] { shortfall },
                        itemStyle = new { color = Red },
                    },
                },
            },
            Options);
    }

    /// <summary>
    /// Per-product price history: one candle per virtual year that saw a trade, drawn from that
    /// year's first, highest, lowest and last traded price, with the year's closing price as a line.
    /// </summary>
    public static string Prices(IReadOnlyList<ProductBookView> books, int years, decimal floor, string floorLabel)
    {
        ArgumentNullException.ThrowIfNull(books);

        string[] axis = [.. Enumerable.Range(1, years).Select(year => $"Year {year}")];
        List<object> series = [];
        List<string> legend = [];

        for (int index = 0; index < books.Count; index++)
        {
            ProductBookView book = books[index];
            string colour = ProductColours[index % ProductColours.Length];
            // A bearish candle is the product's own colour at half weight, so the series is still
            // identifiable when the market is falling.
            string faded = colour + "80";
            Dictionary<int, YearPriceView> byYear = book.YearPrices.ToDictionary(price => price.Year);
            // ECharts wants "-" for a year a product did not trade in; a bare null makes its
            // candlestick layout read through a missing point and throw.
            List<object> candles = [];
            List<object> closes = [];

            for (int year = 1; year <= years; year++)
            {
                if (byYear.TryGetValue(year, out YearPriceView? price))
                {
                    candles.Add(new decimal?[] { price.Open, price.Close, price.Low, price.High });
                    closes.Add(price.Close);
                }
                else
                {
                    candles.Add("-");
                    closes.Add("-");
                }
            }

            legend.Add(book.Label);

            series.Add(new
            {
                name = book.Label,
                type = "candlestick",
                data = candles,
                itemStyle = new
                {
                    color = colour,
                    color0 = faded,
                    borderColor = colour,
                    borderColor0 = faded,
                },
            });

            series.Add(new
            {
                name = $"{book.Label} close",
                type = "line",
                data = closes,
                symbol = "none",
                connectNulls = true,
                lineStyle = new
                {
                    width = 1,
                    type = CloseStyles[index % CloseStyles.Length],
                    color = colour,
                },
            });
        }

        // Every book's volatility band is anchored on the auction floor, so the floor is drawn even
        // while nothing has traded: it is what the first order of the year will be priced against.
        series.Add(new
        {
            name = floorLabel,
            type = "line",
            data = Enumerable.Repeat<decimal?>(floor, years).ToArray(),
            symbol = "none",
            lineStyle = new { width = 1, type = "dashed", color = Amber },
        });

        return JsonSerializer.Serialize(
            new
            {
                // Room for the legend to sit above the plot: on a phone it wraps onto a second line
                // and would otherwise land on the axis title and the top gridline.
                grid = new { left = 56, right = 16, top = 60, bottom = 40 },
                tooltip = new { trigger = "axis", axisPointer = new { type = "cross" } },
                legend = new { data = legend.ToArray(), top = 0, type = "scroll", textStyle = new { fontSize = 11 } },
                xAxis = new { type = "category", data = axis },
                yAxis = new { type = "value", scale = true, name = "per tonne" },
                series = series,
            },
            Options);
    }

}
