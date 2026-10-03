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
    private const string Grey = "#8a94a6";

    private static readonly JsonSerializerOptions Options = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    /// <summary>Marginal abatement cost per project, cheapest first, each labelled with its tonnes.</summary>
    public static string Macc(IReadOnlyList<AbatementOpportunityView> opportunities)
    {
        ArgumentNullException.ThrowIfNull(opportunities);

        List<AbatementOpportunityView> open = [.. opportunities.Where(opportunity => !opportunity.Implemented)];
        List<AbatementOpportunityView> source = open.Count > 0 ? open : [.. opportunities];
        List<AbatementOpportunityView> ordered = [.. source.OrderBy(opportunity => opportunity.CostPerTonne)];

        return JsonSerializer.Serialize(
            new
            {
                grid = new { left = 64, right = 16, top = 24, bottom = 48 },
                tooltip = new { trigger = "item" },
                xAxis = new
                {
                    type = "category",
                    data = ordered.Select(opportunity => Label(opportunity)).ToArray(),
                    axisLabel = new { interval = 0, fontSize = 10 },
                },
                yAxis = new { type = "value", name = "per tonne" },
                series = new object[]
                {
                    new
                    {
                        name = "Cost per tonne",
                        type = "bar",
                        data = ordered.Select(opportunity => opportunity.CostPerTonne).ToArray(),
                        itemStyle = new { color = Green },
                        barMaxWidth = 44,
                    },
                },
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

    /// <summary>The year's free allocation and the gap the company still has to cover.</summary>
    public static string LongShort(PositionView position, string company, string freeLabel, string shortLabel)
    {
        ArgumentNullException.ThrowIfNull(position);

        decimal shortfall = position.Shortfall > 0m ? position.Shortfall : 0m;

        return JsonSerializer.Serialize(
            new
            {
                grid = new { left = 16, right = 24, top = 34, bottom = 28 },
                tooltip = new { trigger = "axis", axisPointer = new { type = "shadow" } },
                legend = new { data = new[] { freeLabel, shortLabel }, top = 0, textStyle = new { fontSize = 11 } },
                xAxis = new { type = "value", axisLabel = new { show = false }, splitLine = new { show = false } },
                yAxis = new { type = "category", data = new[] { company }, axisLabel = new { show = false } },
                series = new object[]
                {
                    new
                    {
                        name = freeLabel,
                        type = "bar",
                        stack = "position",
                        data = new[] { position.FreeAllocation },
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

        foreach (ProductBookView book in books)
        {
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
                    color = Green,
                    color0 = Red,
                    borderColor = Green,
                    borderColor0 = Red,
                },
            });

            series.Add(new
            {
                name = $"{book.Label} close",
                type = "line",
                data = closes,
                symbol = "none",
                connectNulls = true,
                lineStyle = new { width = 1, type = "dashed", color = Grey },
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
                grid = new { left = 56, right = 16, top = 34, bottom = 40 },
                tooltip = new { trigger = "axis", axisPointer = new { type = "cross" } },
                legend = new { data = legend.ToArray(), top = 0, textStyle = new { fontSize = 11 } },
                xAxis = new { type = "category", data = axis },
                yAxis = new { type = "value", scale = true, name = "per tonne" },
                series = series,
            },
            Options);
    }

    private static string Label(AbatementOpportunityView opportunity) => string.Create(
        CultureInfo.InvariantCulture,
        $"{opportunity.Code} - {opportunity.AnnualReduction:N0} t");
}
