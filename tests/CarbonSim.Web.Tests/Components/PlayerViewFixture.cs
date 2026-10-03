using CarbonSim.Web.Player;

namespace CarbonSim.Web.Tests.Components;

/// <summary>
/// A prepared player view with one of everything on it, so a screen test renders real content
/// rather than empty panels. It is hand-built: the point is to exercise the screen, not the engine.
/// </summary>
internal static class PlayerViewFixture
{
    public const string CompanyName = "Delta Power";

    public static PlayerView Sample(Guid simulationId)
    {
        ProductView vintage1 = ProductView.Allowance(1);
        ProductView vintage2 = ProductView.Allowance(2);

        return new PlayerView(
            simulationId,
            "Vietnam pilot",
            20260913UL,
            "Running",
            1,
            3,
            2,
            4,
            true,
            true,
            TimeSpan.FromMinutes(11),
            TimeSpan.FromMinutes(2),
            TimeSpan.FromMinutes(3),
            "Tung",
            CompanyName,
            "Power",
            CompanyName,
            false,
            new FinancialView(1_000_000m, 900_000m, 100_000m, 50_000m, 50_000m, 4_000_000m, 1_500m, 4_001_500m, -250_000m, 0m, 3_751_500m),
            new ComplianceView(250_000m, 250_000m, 2.5m, 2.5m, 100_000m, 90_000m, 90_000m, 80_000m, 0m, 0m, 0m, true),
            new PositionView(81_000m, 100_000m, 5_000m, 14_000m, 42_000m),
            Units(),
            Abatements(),
            [ClearedAuction(), OpenAuction()],
            OpenAuction(),
            250_000m,
            Books(vintage1, vintage2),
            [vintage1, vintage2, ProductView.Offset],
            new AuctionPriceSummaryView(100m, 120m, 110m, 115m, 110m),
            [new CounterpartyView(99, "Red River Unit 1", "Red River Power", "Power")],
            [Offer(7, mine: false)],
            [Offer(6, mine: true)],
            [new HoldingView(vintage1, 81_000m, 12_000m), new HoldingView(ProductView.Offset, 2_000m, 0m)],
            [Trade(1, vintage1, 110m, 100m)],
            Leaderboard(),
            System(),
            [new ChatLine(1, "Red River Power", "Anyone selling vintage 1?")],
            true);
    }

    public static RunSummary Summary(Guid simulationId) => new(
        simulationId,
        "Vietnam pilot",
        "Running",
        1,
        3,
        39,
        37,
        242,
        4,
        TimeSpan.FromMinutes(20),
        355_850_000m,
        0.9m,
        0.1m,
        1m,
        100m,
        300m,
        0.45m);

    private static List<UnitView> Units() =>
    [
        new UnitView(
            1,
            "Delta Unit 1",
            "Power",
            100_000m,
            81_000m,
            100_000m,
            5_000m,
            4_000_000m,
            false,
            false,
            1,
            [
                new AbatementOpportunityView(1, "Delta Unit 1", 0, "P1", "Boiler efficiency", 1_606_662m, 53_555m, 1, 10, -107_110m, -0.4m, 20m, true),
                new AbatementOpportunityView(1, "Delta Unit 1", 1, "P2", "Waste heat recovery", 2_500_000m, 40_000m, 1, 10, 200_000m, 0.4m, 7m, false),
            ]),
    ];

    private static List<AbatementView> Abatements() =>
    [
        new AbatementView(1, "Delta Unit 1", "P1", "Boiler efficiency", 1, 2, 11, "Operating", 53_555m, 1_606_662m),
    ];

    private static AuctionView OpenAuction() => new(
        1,
        2,
        false,
        250_000m,
        250_000m,
        null,
        0m,
        [new AuctionLotView(1, 250_000m, false)],
        [],
        []);

    private static AuctionView ClearedAuction() => new(
        1,
        1,
        true,
        250_000m,
        0m,
        100m,
        250_000m,
        [new AuctionLotView(1, 250_000m, false)],
        [new MyBidView(1, 105m, 20_000m, 20_000m, 2_000_000m)],
        [new AuctionAwardView(1, 20_000m, 100m, 2_000_000m)]);

    private static List<ProductBookView> Books(ProductView vintage1, ProductView vintage2) =>
    [
        new ProductBookView(
            vintage1,
            vintage1.Key,
            108m,
            112m,
            110m,
            99m,
            121m,
            [new OrderView(11, 1, vintage1.Key, "Sell", "Limit", 5_000m, 0m, 112m, null, "Open")],
            [new OrderView(11, 1, vintage1.Key, "Sell", "Limit", 5_000m, 0m, 112m, null, "Open")],
            [Trade(1, vintage1, 110m, 100m)],
            [new YearPriceView(1, 100m, 118m, 98m, 110m, 250_000m)]),
        new ProductBookView(
            vintage2,
            vintage2.Key,
            104m,
            null,
            106m,
            95m,
            117m,
            [],
            [],
            [],
            [new YearPriceView(1, 106m, 106m, 106m, 106m, 1_000m)]),
    ];

    private static TradeRow Trade(int year, ProductView product, decimal price, decimal volume) =>
        new(year, "Exchange", product.Key, price, volume, CompanyName, "Red River Power", true);

    private static OtcOfferView Offer(long id, bool mine) => new(
        id,
        mine ? "Delta Unit 1" : "Red River Unit 1",
        mine ? "Red River Unit 1" : "Delta Unit 1",
        mine ? CompanyName : "Red River Power",
        mine ? "Red River Power" : CompanyName,
        ProductView.Allowance(1),
        110m,
        1_000m,
        110_000m,
        "Pending",
        !mine,
        mine);

    private static List<LeaderboardRow> Leaderboard() =>
    [
        new LeaderboardRow(1, CompanyName, "Delta Unit 1", CompanyName, 250_000m, 2.5m, 81_000m, false, true),
        new LeaderboardRow(2, "Red River Power", "Red River Unit 1", "Red River Power", 400_000m, 4.0m, -1_000m, true, false),
    ];

    private static SystemInfoView System() => new(
        "Vietnam pilot",
        355_850_000m,
        0.03m,
        0.9m,
        0.1m,
        1m,
        300m,
        1m,
        100m,
        300m,
        4,
        TimeSpan.FromMinutes(20),
        TimeSpan.FromMinutes(3),
        0.45m,
        0.1m,
        0.07m,
        [new SectorView("Power", 0.43m, 200, 137_000_000m)],
        Totals(355_850_000m, 320_265_000m, 35_585_000m),
        Totals(355_850_000m, 320_265_000m, 35_585_000m),
        3_558_500m,
        [new HoldingView(ProductView.Allowance(1), 2_000_000m, 35_585_000m)]);

    private static SystemTotalsView Totals(decimal cap, decimal free, decimal auctionable) => new(
        350_000_000m,
        300_000_000m,
        21_000_000m,
        0m,
        0m,
        30_000_000m,
        30_000_000m,
        1_000_000m,
        1_000_000m,
        5_000_000m,
        550_000_000m,
        110m,
        87m,
        4,
        200_000m,
        200_000m,
        0,
        0m,
        0m,
        cap,
        free,
        auctionable);
}
