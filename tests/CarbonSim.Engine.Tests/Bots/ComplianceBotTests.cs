using CarbonSim.Engine.Bots;
using CarbonSim.Engine.Clock;
using CarbonSim.Engine.Domain;
using CarbonSim.Engine.Market;
using CarbonSim.Engine.Market.Exchange;
using CarbonSim.Engine.Market.Otc;
using CarbonSim.Engine.Reporting;
using FluentAssertions;

namespace CarbonSim.Engine.Tests.Bots;

public sealed class ComplianceBotTests
{
    private sealed record Run(Simulation Simulation, SimulationClock Clock, TestTimeProvider Time, SimulationBots Bots, BotMarkets Markets);

    private static Run Build(BotDifficulty difficulty = BotDifficulty.Normal, ulong seed = 1, bool automateHumans = false)
    {
        Simulation simulation = TestSimulation.Build(seed: seed);
        TestTimeProvider time = new();
        SimulationClock clock = new(simulation, time);
        SimulationBots bots = SimulationBots.Create(simulation, BotSettings.For(difficulty));

        if (automateHumans)
        {
            foreach (Unit unit in simulation.Units.Where(unit => unit.Company.Owner.Kind == PlayerKind.Human))
            {
                unit.AutoTrade = true;
            }

            bots = SimulationBots.Create(simulation, BotSettings.For(difficulty));
        }

        clock.Start();

        return new Run(simulation, clock, time, bots, SimulationBots.Markets(simulation));
    }

    private static IReadOnlyList<BotAction> Advance(Run run, TimeSpan amount)
    {
        run.Time.Advance(amount);
        run.Clock.Advance();

        return run.Bots.Act(run.Simulation, run.Clock, run.Markets);
    }

    [Fact]
    public void The_difficulties_differ_in_margin_noise_and_appetite()
    {
        BotSettings easy = BotSettings.For(BotDifficulty.Easy);
        BotSettings normal = BotSettings.For(BotDifficulty.Normal);
        BotSettings hard = BotSettings.For(BotDifficulty.Hard);

        easy.AbatementMargin.Should().BeLessThan(normal.AbatementMargin).And.BeLessThan(hard.AbatementMargin);
        hard.BidPriceNoise.Should().BeLessThan(normal.BidPriceNoise).And.BeLessThan(easy.BidPriceNoise);
        hard.BidVolumeFraction.Should().BeGreaterThan(easy.BidVolumeFraction);
        hard.OffsetDiscount.Should().BeLessThan(easy.OffsetDiscount, "a sharper bot knows offsets are worth more");
    }

    [Fact]
    public void Only_ai_companies_and_autotrade_units_have_a_bot()
    {
        Run withoutAssistance = Build();
        Run withAssistance = Build(automateHumans: true);

        withoutAssistance.Bots.Bots.Should().ContainSingle()
            .Which.Company.Name.Should().Be("AI Power 1");
        withAssistance.Bots.Bots.Should().HaveCount(3);
        withAssistance.Bots.Bots.Single(bot => bot.Company.Name == "AI Power 1").Units.Should().ContainSingle();
    }

    [Fact]
    public void A_bot_implements_abatement_it_can_afford_while_it_beats_the_allowance_price()
    {
        Run run = Build();
        Unit unit = run.Simulation.FindUnit(3);

        Advance(run, TimeSpan.FromSeconds(30));

        // Cheapest net cost per tonne first, and only what beats the expected allowance price.
        run.Simulation.Abatements.For(unit).Select(project => project.Option.Code).Should().Equal("P3", "P1");
        run.Simulation.Abatements.For(unit).Select(project => project.Option.Code)
            .Should().NotContain("P2", "the retrofit costs 290 a tonne, far above the expected allowance price");
        unit.Company.Capital.Should().BeLessThan(TestSimulation.CompanyCapital);
    }

    [Fact]
    public void A_bot_leaves_the_projects_it_cannot_finish_in_time()
    {
        Run run = Build();
        Unit unit = run.Simulation.FindUnit(3);

        // Two years in, the two-year retrofit still fits; by year 3 it does not.
        Advance(run, TimeSpan.FromMinutes(20));
        run.Clock.EndYear();
        run.Clock.BeginNextYear();
        Advance(run, TimeSpan.FromMinutes(20));
        run.Clock.EndYear();
        run.Clock.BeginNextYear();
        Advance(run, TimeSpan.FromSeconds(30));

        run.Simulation.Abatements.For(unit).Select(project => project.Option.Code)
            .Should().NotContain("P2", "a two year build started in the last year would never run");
    }

    [Fact]
    public void A_bot_bids_part_of_its_shortfall_at_auction_and_only_once_per_auction()
    {
        Run run = Build();
        Unit unit = run.Simulation.FindUnit(3);
        decimal shortfall = run.Bots.Bots.Single().Shortfall(run.Simulation, 1);

        Advance(run, TimeSpan.FromMinutes(2) + TimeSpan.FromSeconds(1));
        Auction auction = run.Markets.Schedule.ForSection(1, 1);
        int bidsAfterFirstTurn = auction.Bids.Count;

        Advance(run, TimeSpan.FromSeconds(10));

        bidsAfterFirstTurn.Should().Be(1, "a bot bids once per auction");
        auction.Bids.Should().HaveCount(1);
        auction.Bids[0].Unit.Should().BeSameAs(unit);
        auction.Bids[0].Volume.Should().BeApproximately(shortfall * 0.7m, 1m);
        auction.Bids[0].Price.Should().BeInRange(100m, 300m);
    }

    [Fact]
    public void A_bot_never_bids_more_than_its_credit_allows()
    {
        Simulation simulation = TestSimulation.Build(capital: 500_000m);
        TestTimeProvider time = new();
        SimulationClock clock = new(simulation, time);
        SimulationBots bots = SimulationBots.Create(simulation);
        BotMarkets markets = SimulationBots.Markets(simulation);
        clock.Start();
        time.Advance(TimeSpan.FromMinutes(2) + TimeSpan.FromSeconds(1));
        clock.Advance();

        bots.Act(simulation, clock, markets);

        Auction auction = markets.Schedule.ForSection(1, 1);
        auction.Bids.Should().ContainSingle();
        (auction.Bids[0].Volume * auction.Bids[0].Price).Should().BeLessThanOrEqualTo(500_000m);
    }

    [Fact]
    public void A_bot_buys_allowances_on_the_exchange_when_it_is_short()
    {
        Run run = Build();
        Product vintage1 = Product.Allowance(1);
        run.Simulation.Ledger.Grant(run.Simulation.FindCompany(1), vintage1, 1_000_000m);
        Exchange exchange = run.Markets.Exchange;
        exchange.Place(new OrderRequest(
            run.Simulation.FindUnit(1), vintage1, OrderSide.Sell, OrderKind.Limit, 500_000m, Price: 100m));

        Advance(run, TimeSpan.FromMinutes(12));

        exchange.Book(vintage1).Trades.Should().NotBeEmpty("the bot took the offer");
        run.Simulation.FindCompany(3).Units.Should().OnlyContain(unit => run.Simulation.Ledger.Held(unit.Company, vintage1) > 0m);
    }

    [Fact]
    public void A_bot_sells_the_surplus_it_cannot_keep()
    {
        Run run = Build();
        Product vintage1 = Product.Allowance(1);
        Company bot = run.Simulation.FindCompany(3);
        run.Simulation.Ledger.Grant(bot, vintage1, 100_000_000m);
        Exchange exchange = run.Markets.Exchange;
        exchange.Place(new OrderRequest(
            run.Simulation.FindUnit(4), vintage1, OrderSide.Buy, OrderKind.Limit, 5_000_000m, Price: 100m));

        Advance(run, TimeSpan.FromMinutes(12));

        run.Simulation.Journal.Trades.Should().ContainSingle()
            .Which.Seller.Should().BeSameAs(bot);
        run.Simulation.Journal.Trades[0].Product.Should().Be(vintage1);
    }

    [Fact]
    public void A_bot_values_offsets_below_allowances_and_trades_the_ones_it_cannot_use()
    {
        Run run = Build();
        Company seller = run.Simulation.FindCompany(3);
        Company buyer = run.Simulation.FindCompany(1);
        Product offsets = Product.Offset;

        // The bot holds far more offsets than it can ever surrender; the human company needs them.
        run.Simulation.Ledger.Grant(seller, offsets, 50_000_000m);
        foreach (Unit unit in buyer.Units)
        {
            unit.AutoTrade = true;
        }

        SimulationBots bots = SimulationBots.Create(run.Simulation);
        BotMarkets markets = SimulationBots.Markets(run.Simulation, run.Markets.Exchange, run.Markets.Otc, run.Markets.Schedule);

        run.Time.Advance(TimeSpan.FromMinutes(12));
        run.Clock.Advance();
        bots.Act(run.Simulation, run.Clock, markets);

        MarketTrade? offsetTrade = run.Simulation.Journal.Trades.FirstOrDefault(trade => trade.Product.IsOffset);
        offsetTrade.Should().NotBeNull("one bot had spare offsets and another wanted them");
        offsetTrade!.Price.Should().BeLessThan(100m, "offsets are worth less than allowances because they can only cover a share of the obligation");
        offsetTrade.Price.Should().BeGreaterThan(75m, "the discount is bounded at 25% for the softest bot");
    }

    [Fact]
    public void The_same_seed_gives_the_same_bots_the_same_timing()
    {
        Run first = Build(seed: 7);
        Run second = Build(seed: 7);
        Run other = Build(seed: 8);

        first.Bots.Bots.Select(bot => bot.TriggerTime(BotTrigger.Abatement))
            .Should().Equal(second.Bots.Bots.Select(bot => bot.TriggerTime(BotTrigger.Abatement)));
        first.Bots.Bots.Select(bot => bot.TriggerTime(BotTrigger.Trade))
            .Should().NotEqual(other.Bots.Bots.Select(bot => bot.TriggerTime(BotTrigger.Trade)));
    }

    [Fact]
    public void Bots_act_once_per_trigger_and_learn_from_the_market()
    {
        Run run = Build();
        ComplianceBot bot = run.Bots.Bots.Single();
        bot.ExpectedAllowancePrice.Should().Be(100m);

        Advance(run, TimeSpan.FromSeconds(60));
        IReadOnlyList<BotAction> second = Advance(run, TimeSpan.FromSeconds(5));

        second.Should().NotContain(action => action.Trigger == BotTrigger.Abatement, "the abatement turn is once a year");
        bot.ExpectedAllowancePrice.Should().BeGreaterThanOrEqualTo(100m);
    }

    [Fact]
    public void A_bot_does_nothing_while_the_clock_is_not_running()
    {
        Run run = Build();
        run.Clock.Pause();

        run.Bots.Act(run.Simulation, run.Clock, run.Markets).Should().BeEmpty();
    }

    [Fact]
    public void A_human_unit_can_be_left_to_the_owner()
    {
        Run run = Build();
        OtcMarket otc = run.Markets.Otc;

        otc.Pending.Should().BeEmpty();
        run.Simulation.FindUnit(1).AutoTrade.Should().BeFalse();
        run.Bots.Bots.Select(bot => bot.Company.Name).Should().NotContain("Cong ty Dien luc Binh Minh");
    }
}
