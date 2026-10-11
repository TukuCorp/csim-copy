using CarbonSim.Engine.Domain;
using CarbonSim.Engine.Market;
using CarbonSim.Engine.Market.Exchange;
using CarbonSim.Engine.Run;
using FluentAssertions;

namespace CarbonSim.Engine.Tests.Market;

/// <summary>
/// A book's volatility band is the last trade less a share of it, so its edges rarely land on a
/// whole cent. A price a bot has rounded to two decimals can therefore sit a fraction of a cent
/// outside the band and be refused by the book that asked for it. FitToBand is the one place that
/// turns a wanted price into one the book will take, and this pins it at both edges.
/// </summary>
public sealed class OrderBookBandTests
{
    [Fact]
    public void A_price_fitted_to_the_band_is_one_the_book_accepts_at_both_edges()
    {
        Simulation simulation = TestSimulation.Build();
        Product product = Product.Allowance(1);
        OrderBook book = SimulationRun.Create(simulation, new TestTimeProvider()).Exchange.Book(product);
        Unit unit = TestSimulation.Unit(simulation, 1);

        // A last trade whose band edges carry more than two decimals, which is exactly the case a
        // whole-cent quote rounds through: the floor is 81.8235 and the ceiling 100.0065.
        book.Reanchor(90.915m);

        decimal[] offsets = [-0.02m, -0.005m, -0.001m, 0m, 0.001m, 0.004m, 0.005m, 0.02m];
        List<decimal> probes = [];
        foreach (decimal offset in offsets)
        {
            probes.Add(book.BandFloor + offset);
            probes.Add(book.BandCeiling + offset);
        }

        foreach (decimal probe in probes)
        {
            decimal price = book.FitToBand(probe);

            price.Should().Be(Math.Round(price, 2, MidpointRounding.AwayFromZero), $"{probe} fits to a whole cent");
            price.Should().BeGreaterThanOrEqualTo(book.BandFloor, $"{probe} must not fit below the floor");
            price.Should().BeLessThanOrEqualTo(book.BandCeiling, $"{probe} must not fit above the ceiling");

            Action place = () => book.Submit(
                new OrderRequest(unit, product, OrderSide.Buy, OrderKind.Limit, 10m, price));

            place.Should().NotThrow($"the book must accept the price it was handed for {probe}");
        }
    }
}
