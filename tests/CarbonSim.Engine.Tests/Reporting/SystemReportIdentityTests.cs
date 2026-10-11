using CarbonSim.Engine.Domain;
using CarbonSim.Engine.Market;
using CarbonSim.Engine.Market.Exchange;
using CarbonSim.Engine.Reporting;
using FluentAssertions;

namespace CarbonSim.Engine.Tests.Reporting;

/// <summary>
/// Every trade has two sides, so at system level what the fleet sold is what it bought. The report
/// computes one journal volume per product kind and uses it for both figures; this pins the identity
/// so a later change cannot quietly split them.
/// </summary>
public sealed class SystemReportIdentityTests
{
    [Fact]
    public void The_fleets_sales_and_purchases_are_one_measurement()
    {
        Simulation simulation = TestSimulation.Build();
        Company seller = simulation.FindCompany(1);
        Company buyer = simulation.FindCompany(2);

        TestSimulation.Running(simulation);
        simulation.Ledger.Grant(seller, Product.Allowance(1), 1_000m);
        simulation.Ledger.Grant(seller, Product.Offset, 500m);

        Exchange exchange = new(simulation);
        exchange.Place(new OrderRequest(seller.Units[0], Product.Allowance(1), OrderSide.Sell, OrderKind.Limit, 100m, 105m));
        exchange.Place(new OrderRequest(buyer.Units[0], Product.Allowance(1), OrderSide.Buy, OrderKind.Limit, 100m, 105m));
        exchange.Place(new OrderRequest(seller.Units[0], Product.Offset, OrderSide.Sell, OrderKind.Limit, 40m, 100m));
        exchange.Place(new OrderRequest(buyer.Units[0], Product.Offset, OrderSide.Buy, OrderKind.Limit, 40m, 100m));

        SystemTotals totals = SystemReport.For(simulation, 1).ThisYear;

        totals.AllowancesSold.Should().BePositive("a trade happened");
        totals.OffsetsSold.Should().BePositive("and one for offsets too");
        totals.AllowancesSold.Should().Be(totals.AllowancesBought, "a sale is somebody's purchase");
        totals.OffsetsSold.Should().Be(totals.OffsetsBought, "and so is an offset sale");
    }
}
