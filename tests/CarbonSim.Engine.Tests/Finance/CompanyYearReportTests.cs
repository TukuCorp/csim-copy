using CarbonSim.Engine.Clock;
using CarbonSim.Engine.Domain;
using CarbonSim.Engine.Finance;
using FluentAssertions;

namespace CarbonSim.Engine.Tests.Finance;

/// <summary>
/// The original's end-of-year company report (catalog `EndOfYearCompanyReport`): the capital the year
/// opened with, each line of profit and loss from the cash ledger, and the capital it closed with, so
/// the lines add up to the change in capital.
/// </summary>
public sealed class CompanyYearReportTests
{
    [Fact]
    public void The_lines_of_a_year_add_up_from_the_opening_to_the_closing_capital()
    {
        Simulation simulation = TestSimulation.Build(capital: 1_000_000m, overdraftLimit: 500_000m);
        new SimulationClock(simulation, new TestTimeProvider()).Start();
        Company company = simulation.FindCompany(1);

        simulation.Cash.Withdraw(company, 100_000m, CashCategory.AuctionPurchase, "auction");
        simulation.Cash.Withdraw(company, 20_000m, CashCategory.SecondaryPurchase, "exchange buy");
        simulation.Cash.Withdraw(company, 5_000m, CashCategory.OtcPurchase, "otc buy");
        simulation.Cash.Deposit(company, 30_000m, CashCategory.InstrumentSale, "sale");
        simulation.Cash.Withdraw(company, 50_000m, CashCategory.AbatementCapital, "project");
        simulation.Cash.Charge(company, 7_000m, CashCategory.Fine, "late report");
        simulation.Compliance.Reconcile(company, 1);
        simulation.Finance.CloseYear(1);

        CompanyYearReport report = simulation.Finance.YearReport(company, 1);

        report.CapitalAtStart.Should().Be(1_000_000m);
        report.AuctionPurchases.Should().Be(-100_000m);
        report.SecondaryMarketPurchases.Should().Be(-25_000m, "exchange and OTC buys together");
        report.InstrumentSales.Should().Be(30_000m);
        report.AbatementCapital.Should().Be(-50_000m);
        report.OtherFines.Should().Be(-7_000m);
        report.CompliancePenalties.Should().BeNegative("the company held nothing and was penalised");
        report.LoanInterest.Should().BeNegative("it ended the year borrowing");
        report.OperatingProfit.Should().Be(simulation.Finance.NormalOperatingProfit(company, 1));
        report.ComplianceNet.Should().Be(report.AuctionPurchases + report.SecondaryMarketPurchases + report.InstrumentSales + report.CompliancePenalties);
        report.CapitalAtEnd.Should().Be(company.Capital);
        report.ChangeInCapital.Should().Be(report.CapitalAtEnd - report.CapitalAtStart);
        (report.OperatingProfit + report.AbatementNetRevenue + report.AbatementCapital + report.ComplianceNet
            + report.OtherFines + report.LoanInterest).Should().Be(report.ChangeInCapital, "every line is accounted for");
    }

    [Fact]
    public void The_next_year_opens_on_the_capital_the_last_one_closed_with()
    {
        Simulation simulation = TestSimulation.Build();
        SimulationClock clock = new(simulation, new TestTimeProvider());
        clock.Start();
        Company company = simulation.FindCompany(1);
        simulation.Compliance.Reconcile(1);
        simulation.Finance.CloseYear(1);
        clock.RequestHalt();
        clock.EndYear();
        clock.BeginNextYear();

        simulation.Finance.YearReport(company, 2).CapitalAtStart.Should().Be(simulation.Finance.YearReport(company, 1).CapitalAtEnd);
    }
}
