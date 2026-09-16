using CarbonSim.Engine.Domain;
using CarbonSim.Engine.Market;
using FluentAssertions;

namespace CarbonSim.Engine.Tests.Market;

public sealed class AllowanceLedgerTests
{
    [Fact]
    public void A_year_s_free_allocation_is_granted_to_the_company_that_owns_the_units()
    {
        Simulation simulation = TestSimulation.Build();
        Company company = simulation.FindCompany(1);

        simulation.Ledger.GrantFreeAllocation(1);

        simulation.Ledger.Available(company, Product.Allowance(1)).Should().Be(1_710_000m * 2m);
        simulation.Ledger.Available(company, Product.Allowance(2)).Should().Be(0m);
        simulation.Ledger.Available(simulation.FindCompany(2), Product.Allowance(1)).Should().Be(3_420_000m);
    }

    [Fact]
    public void Free_allocation_is_granted_once_per_year()
    {
        Simulation simulation = TestSimulation.Build();
        simulation.Ledger.GrantFreeAllocation(1);

        Action again = () => simulation.Ledger.GrantFreeAllocation(1);
        Action beyond = () => simulation.Ledger.GrantFreeAllocation(4);

        simulation.Ledger.GrantFreeAllocation(2);
        simulation.Ledger.Available(simulation.FindCompany(1), Product.Allowance(2)).Should().Be(3_317_400m);

        again.Should().Throw<InvalidOperationException>().WithMessage("*already*");
        beyond.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Vintage_allowances_are_held_separately_from_offsets()
    {
        Simulation simulation = TestSimulation.Build();
        Company company = simulation.FindCompany(1);

        simulation.Ledger.Grant(company, Product.Allowance(1), 100m);
        simulation.Ledger.Grant(company, Product.Offset, 40m);

        simulation.Ledger.Available(company, Product.Allowance(1)).Should().Be(100m);
        simulation.Ledger.Available(company, Product.Allowance(2)).Should().Be(0m);
        simulation.Ledger.Available(company, Product.Offset).Should().Be(40m);
    }

    [Fact]
    public void Allowances_can_be_transferred_between_companies()
    {
        Simulation simulation = TestSimulation.Build();
        Company seller = simulation.FindCompany(1);
        Company buyer = simulation.FindCompany(2);
        simulation.Ledger.Grant(seller, Product.Allowance(1), 500m);

        simulation.Ledger.Transfer(seller, buyer, Product.Allowance(1), 200m);

        simulation.Ledger.Available(seller, Product.Allowance(1)).Should().Be(300m);
        simulation.Ledger.Available(buyer, Product.Allowance(1)).Should().Be(200m);
    }

    [Fact]
    public void A_company_cannot_transfer_instruments_it_does_not_hold()
    {
        Simulation simulation = TestSimulation.Build();
        Company seller = simulation.FindCompany(1);
        Company buyer = simulation.FindCompany(2);
        simulation.Ledger.Grant(seller, Product.Allowance(1), 500m);

        Action oversell = () => simulation.Ledger.Transfer(seller, buyer, Product.Allowance(1), 600m);

        oversell.Should().Throw<InvalidOperationException>().WithMessage("*insufficient*");
        simulation.Ledger.Available(seller, Product.Allowance(1)).Should().Be(500m);
    }

    [Fact]
    public void Escrowed_instruments_are_not_available_for_anything_else()
    {
        Simulation simulation = TestSimulation.Build();
        Company company = simulation.FindCompany(1);
        simulation.Ledger.Grant(company, Product.Allowance(1), 500m);

        simulation.Ledger.Escrow(company, Product.Allowance(1), 200m);

        simulation.Ledger.Available(company, Product.Allowance(1)).Should().Be(300m);
        simulation.Ledger.Escrowed(company, Product.Allowance(1)).Should().Be(200m);

        Action overescrow = () => simulation.Ledger.Escrow(company, Product.Allowance(1), 400m);
        overescrow.Should().Throw<InvalidOperationException>();

        simulation.Ledger.ReleaseEscrow(company, Product.Allowance(1), 200m);
        simulation.Ledger.Available(company, Product.Allowance(1)).Should().Be(500m);
    }
}
