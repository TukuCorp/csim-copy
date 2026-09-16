using CarbonSim.Engine.Allocation;
using CarbonSim.Engine.Clock;
using CarbonSim.Engine.Compliance;
using CarbonSim.Engine.Domain;
using CarbonSim.Engine.Finance;
using CarbonSim.Engine.Market;
using CarbonSim.Engine.Market.Exchange;
using CarbonSim.Engine.Market.Otc;
using CarbonSim.Engine.Reporting;
using FluentAssertions;

namespace CarbonSim.Engine.Tests.Reporting;

public sealed class ReportingTests
{
    private static readonly Product Vintage1 = Product.Allowance(1);

    private static (Simulation Simulation, SimulationClock Clock, TestTimeProvider Time) Build()
    {
        Simulation simulation = TestSimulation.Build();
        TestTimeProvider time = new();
        SimulationClock clock = new(simulation, time);
        clock.Start();

        return (simulation, clock, time);
    }

    private static void NextYear(SimulationClock clock, TestTimeProvider time)
    {
        time.Advance(TimeSpan.FromMinutes(20));
        clock.Advance();
        clock.EndYear();
        clock.BeginNextYear();
    }

    [Fact]
    public void The_journal_records_every_trade_with_its_channel_and_year()
    {
        (Simulation simulation, _, _) = Build();
        simulation.Ledger.Grant(simulation.FindCompany(1), Vintage1, 10_000m);
        Exchange exchange = new(simulation);
        exchange.Place(new OrderRequest(simulation.FindUnit(1), Vintage1, OrderSide.Sell, OrderKind.Limit, 1_000m, Price: 105m));
        exchange.Place(new OrderRequest(simulation.FindUnit(4), Vintage1, OrderSide.Buy, OrderKind.Limit, 1_000m, Price: 105m));

        OtcMarket otc = new(simulation);
        OtcOffer offer = otc.Send(simulation.FindUnit(1), simulation.FindUnit(4), Vintage1, 100m, 500m);
        otc.Accept(simulation.FindUnit(4), offer.Id);

        Auction auction = AuctionSchedule.Build(simulation).ForSection(1, 1);
        auction.PlaceBid(simulation.FindUnit(1), 1, 102m, 200m);
        auction.Clear();

        simulation.Journal.Trades.Should().HaveCount(3);
        simulation.Journal.Trades.Select(trade => trade.Channel)
            .Should().Equal(TradeChannel.Exchange, TradeChannel.Otc, TradeChannel.Auction);
        simulation.Journal.Trades.Should().OnlyContain(trade => trade.Year == 1);
        simulation.Journal.Trades[1].Seller!.Name.Should().Be("Cong ty Dien luc Binh Minh");
        simulation.Journal.Trades[2].Seller.Should().BeNull("the government sold it");
        simulation.Journal.Trades[2].Price.Should().Be(100m, "the auction was under-subscribed, so it cleared at the floor");
        simulation.Journal.AveragePrice(1, 1, kind: ProductKind.Allowance)
            .Should().Be((1_000m * 105m + 500m * 100m + 200m * 100m) / 1_700m);
    }

    [Fact]
    public void The_system_report_separates_this_year_from_the_run_so_far()
    {
        (Simulation simulation, SimulationClock clock, TestTimeProvider time) = Build();
        simulation.Ledger.GrantFreeAllocation(1);
        simulation.Compliance.Reconcile(1);
        NextYear(clock, time);
        simulation.Ledger.GrantFreeAllocation(2);
        simulation.Compliance.Reconcile(2);

        SystemReport report = SystemReport.For(simulation, 2);
        CompliancePosition position = new(simulation);

        report.Year.Should().Be(2);
        report.ThisYear.ForecastEmissions.Should().Be(
            simulation.Companies.Sum(company => position.EmissionsFor(company, 2)));
        report.ToDate.ForecastEmissions.Should().BeGreaterThan(report.ThisYear.ForecastEmissions);
        report.ThisYear.AllowancesSurrendered.Should().BeGreaterThan(0m);
        report.ToDate.AllowancesSurrendered.Should().BeGreaterThan(report.ThisYear.AllowancesSurrendered);
    }

    [Fact]
    public void A_report_only_covers_the_years_that_have_run()
    {
        (Simulation simulation, _, _) = Build();

        Action before = () => SystemReport.For(simulation, 0);
        Action after = () => SystemReport.For(simulation, 4);

        before.Should().Throw<ArgumentOutOfRangeException>();
        after.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void The_report_counts_penalties_fines_and_abatement()
    {
        (Simulation simulation, _, _) = Build();
        Unit unit = simulation.FindUnit(1);
        simulation.Abatements.Implement(unit, unit.AbatementOptions.Single(option => option.Code == "P1"), year: 1);
        simulation.Compliance.Reconcile(1);
        simulation.Compliance.Reconcile(2);
        simulation.Compliance.Reconcile(3);
        simulation.Compliance.IssueFine(unit, 10_000m, "Late surrender");

        SystemReport report = SystemReport.For(simulation, 1);

        report.ThisYear.PenaltyCount.Should().Be(3, "no company was given any allowances to surrender");
        report.ThisYear.PenaltyValue.Should().BeGreaterThan(0m);
        report.ThisYear.FineValue.Should().Be(10_000m);
        report.ThisYear.AbatementsImplemented.Should().Be(1);
        report.ThisYear.AbatementTonnes.Should().Be(100_000m);
        report.ThisYear.EmissionsReduced.Should().Be(0m, "the project is still being built in year 1");
    }

    [Fact]
    public void Auction_volume_and_revenue_are_reported_for_the_year_they_cleared()
    {
        (Simulation simulation, _, _) = Build();
        Auction auction = AuctionSchedule.Build(simulation).ForSection(1, 1);
        auction.PlaceBid(simulation.FindUnit(1), 1, 150m, 10_000m);
        auction.Clear();

        SystemReport report = SystemReport.For(simulation, 1);

        report.ThisYear.AuctionVolume.Should().Be(10_000m);
        report.ThisYear.AuctionRevenue.Should().Be(10_000m * 100m, "the auction was under-subscribed, so it cleared at the floor");
        report.ThisYear.AverageAllowancePrice.Should().Be(100m);
        report.ThisYear.AllowancesSold.Should().Be(0m, "nothing traded in the secondary market");
    }

    [Fact]
    public void A_company_score_is_the_money_it_spent_on_compliance_per_tonne_it_emitted()
    {
        (Simulation simulation, _, _) = Build();
        Company company = simulation.FindCompany(1);
        simulation.Cash.Deposit(company, 1_000_000m, CashCategory.InstrumentSale, "Sold 10,000 t");
        simulation.Cash.Charge(company, 400_000m, CashCategory.Penalty, "Shortfall");
        simulation.Cash.Deposit(company, 900_000m, CashCategory.OperatingProfit, "A good year");

        CompanyScore score = Scoring.ForYear(simulation, company, 1);

        score.Emissions.Should().BeGreaterThan(0m);
        score.CostOfCompliance.Should().Be(400_000m - 1_000_000m, "selling instruments offsets what compliance cost");
        score.MarginalCostOfCompliance.Should().Be(score.CostOfCompliance / score.Emissions);
        score.PenaltyCash.Should().Be(0m, "no reconciliation has run");
    }

    [Fact]
    public void A_company_with_nothing_to_comply_with_costs_nothing_per_tonne()
    {
        (Simulation simulation, _, _) = Build();
        Unit unit = simulation.FindUnit(4);
        simulation.Abatements.ShutdownForYear(unit, 1);
        simulation.Abatements.ShutdownForYear(simulation.FindUnit(1), 1);
        simulation.Abatements.ShutdownForYear(simulation.FindUnit(2), 1);

        CompanyScore score = Scoring.ForYear(simulation, simulation.FindCompany(1), 1);

        score.Emissions.Should().Be(0m);
        score.MarginalCostOfCompliance.Should().Be(0m);
    }

    [Fact]
    public void The_overall_score_adds_up_the_years_and_the_final_position_shows_what_is_left()
    {
        (Simulation simulation, _, _) = Build();
        Company company = simulation.FindCompany(1);
        simulation.Ledger.Grant(company, Vintage1, 1_000m);
        simulation.Cash.Charge(company, 30_000m, CashCategory.Penalty, "A fine from the regulator");

        CompanyScore overall = Scoring.Overall(simulation, company);

        overall.CostOfCompliance.Should().Be(30_000m);
        overall.Emissions.Should().Be(
            simulation.Allocation.Years.Sum(year => Scoring.ForYear(simulation, company, year).Emissions));
        Scoring.FinalPosition(simulation, company).Should().Be(1_000m);
    }

    [Fact]
    public void A_shortfall_shows_up_as_a_negative_final_position()
    {
        (Simulation simulation, _, _) = Build();
        Company company = simulation.FindCompany(1);
        simulation.Ledger.Grant(company, Vintage1, 1_000m);

        CompanyCompliance result = simulation.Compliance.Reconcile(company, 1);

        Scoring.FinalPosition(simulation, company).Should().Be(-result.Shortfall, "its 1,000 allowances went in against the obligation");
        Scoring.FinalPosition(simulation, company).Should().BeLessThan(0m, "it could not cover what it emitted");
    }

    [Fact]
    public void The_leaderboard_ranks_the_cheapest_compliance_first_and_flags_the_bots()
    {
        (Simulation simulation, _, _) = Build();
        Company cheap = simulation.FindCompany(1);
        Company dear = simulation.FindCompany(2);
        simulation.Cash.Deposit(cheap, 100_000m, CashCategory.InstrumentSale, "Sold allowances");
        simulation.Cash.Charge(dear, 500_000m, CashCategory.Penalty, "Shortfall");

        IReadOnlyList<LeaderboardEntry> table = Leaderboard.Rank(simulation);

        table.Should().HaveCount(3);
        table.Select(entry => entry.Rank).Should().Equal(1, 2, 3);
        table[0].Company.Should().BeSameAs(cheap);
        table.Select(entry => entry.OverallMarginalCostOfCompliance).Should().BeInAscendingOrder();
        table.Single(entry => entry.Company.Name == "AI Power 1").IsAutomated.Should().BeTrue();
        table.Single(entry => entry.Company.Name == "Cong ty Dien luc Binh Minh").IsAutomated.Should().BeFalse();
    }

    [Fact]
    public void Companies_level_on_cost_are_split_by_what_they_are_left_holding()
    {
        (Simulation simulation, _, _) = Build();
        Company first = simulation.FindCompany(1);
        Company second = simulation.FindCompany(2);

        // Charge both the same cost per tonne, so only the tie-break can separate them.
        simulation.Cash.Charge(first, 100m * Scoring.Overall(simulation, first).Emissions, CashCategory.Penalty, "Same cost");
        simulation.Cash.Charge(second, 100m * Scoring.Overall(simulation, second).Emissions, CashCategory.Penalty, "Same cost");
        simulation.Ledger.Grant(first, Vintage1, 1_000m);
        simulation.Ledger.Grant(second, Vintage1, 5_000m);

        IReadOnlyList<LeaderboardEntry> table = Leaderboard.Rank(simulation);
        LeaderboardEntry firstEntry = table.Single(entry => ReferenceEquals(entry.Company, first));
        LeaderboardEntry secondEntry = table.Single(entry => ReferenceEquals(entry.Company, second));

        secondEntry.OverallMarginalCostOfCompliance.Should().Be(firstEntry.OverallMarginalCostOfCompliance);
        secondEntry.FinalPosition.Should().BeGreaterThan(firstEntry.FinalPosition);
        secondEntry.Rank.Should().BeLessThan(firstEntry.Rank, "level on cost per tonne, the larger holder ranks first");
    }
}
