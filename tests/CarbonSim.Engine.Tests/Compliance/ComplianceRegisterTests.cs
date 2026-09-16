using CarbonSim.Engine.Allocation;
using CarbonSim.Engine.Compliance;
using CarbonSim.Engine.Domain;
using CarbonSim.Engine.Market;
using FluentAssertions;

namespace CarbonSim.Engine.Tests.Compliance;

public sealed class ComplianceRegisterTests
{
    private const decimal Obligation = 4_000_000m;

    private static readonly Product Vintage1 = Product.Allowance(1);
    private static readonly Product Vintage2 = Product.Allowance(2);

    private static (Simulation Simulation, Company Company) Build()
    {
        Simulation simulation = TestSimulation.Build();
        return (simulation, simulation.FindCompany(1));
    }

    [Fact]
    public void The_obligation_is_what_the_companys_units_emitted()
    {
        (Simulation simulation, Company company) = Build();

        CompanyCompliance result = simulation.Compliance.Reconcile(company, 1);

        result.Obligation.Should().Be(Obligation);
        result.Year.Should().Be(1);
        result.Company.Should().BeSameAs(company);
    }

    [Fact]
    public void Offsets_are_surrendered_first_and_only_up_to_the_usage_limit()
    {
        (Simulation simulation, Company company) = Build();
        simulation.Ledger.Grant(company, Product.Offset, 1_000_000m);
        simulation.Ledger.Grant(company, Vintage1, 4_000_000m);

        CompanyCompliance result = simulation.Compliance.Reconcile(company, 1);

        result.OffsetsSurrendered.Should().Be(400_000m, "10% of a 4 Mt obligation");
        result.AllowancesSurrendered.Should().Be(Obligation - 400_000m);
        simulation.Ledger.Available(company, Product.Offset).Should().Be(600_000m, "the rest cannot be used this year");
        result.Shortfall.Should().Be(0m);
    }

    [Fact]
    public void Allowances_are_surrendered_current_vintage_first_then_older_ones()
    {
        (Simulation simulation, Company company) = Build();
        simulation.Ledger.Grant(company, Product.Allowance(0), 500_000m);
        simulation.Ledger.Grant(company, Vintage1, 1_000_000m);

        CompanyCompliance result = simulation.Compliance.Reconcile(company, 1);

        result.AllowancesSurrendered.Should().Be(1_500_000m);
        simulation.Ledger.Available(company, Vintage1).Should().Be(0m, "this year's vintage goes first");
        simulation.Ledger.Available(company, Product.Allowance(0)).Should().Be(0m);
        result.Shortfall.Should().Be(Obligation - 1_500_000m);
    }

    [Fact]
    public void Allowances_of_a_later_vintage_cannot_be_used_for_an_earlier_year()
    {
        (Simulation simulation, Company company) = Build();
        simulation.Ledger.Grant(company, Vintage2, 5_000_000m);

        CompanyCompliance result = simulation.Compliance.Reconcile(company, 1);

        result.AllowancesSurrendered.Should().Be(0m);
        result.Shortfall.Should().Be(Obligation);
        simulation.Ledger.Available(company, Vintage2).Should().Be(5_000_000m);
    }

    [Fact]
    public void Leftover_allowances_are_banked_up_to_the_obligation_and_the_excess_is_forfeited()
    {
        (Simulation simulation, Company company) = Build();
        simulation.Ledger.Grant(company, Vintage1, 9_000_000m);

        CompanyCompliance result = simulation.Compliance.Reconcile(company, 1);

        result.AllowancesSurrendered.Should().Be(Obligation);
        result.Banked.Should().Be(Obligation, "banking is capped at the year's obligation");
        result.Forfeited.Should().Be(1_000_000m);
        simulation.Ledger.Available(company, Vintage1).Should().Be(Obligation);
    }

    [Fact]
    public void A_company_that_emitted_nothing_keeps_what_it_holds()
    {
        (Simulation simulation, Company company) = Build();
        simulation.Ledger.Grant(company, Vintage1, 1_000_000m);
        foreach (Unit unit in company.Units)
        {
            simulation.Abatements.ShutdownForYear(unit, 1);
        }

        CompanyCompliance result = simulation.Compliance.Reconcile(company, 1);

        result.Obligation.Should().Be(0m);
        result.Banked.Should().Be(1_000_000m, "a company with no emissions cannot have exceeded a banking cap");
        result.Forfeited.Should().Be(0m);
    }

    [Fact]
    public void A_shortfall_costs_cash_and_takes_allowances_from_next_year()
    {
        (Simulation simulation, Company company) = Build();
        simulation.Ledger.Grant(company, Vintage1, 1_000_000m);
        decimal capital = company.Capital;

        CompanyCompliance result = simulation.Compliance.Reconcile(company, 1);

        result.Shortfall.Should().Be(3_000_000m);
        result.PenaltyCash.Should().Be(3_000_000m * 300m);
        result.PenaltyAllowanceDebit.Should().Be(3_000_000m);
        company.Capital.Should().Be(capital - result.PenaltyCash);
    }

    [Fact]
    public void The_penalty_debit_is_taken_out_of_next_years_free_allocation()
    {
        (Simulation simulation, Company company) = Build();
        simulation.Ledger.Grant(company, Vintage1, 1_000_000m);
        simulation.Compliance.Reconcile(company, 1);

        simulation.Ledger.GrantFreeAllocation(2);

        simulation.Ledger.Available(company, Vintage2).Should().Be(
            3_317_400m - 3_000_000m,
            "the free allocation for year 2 less the penalty debit");
    }

    [Fact]
    public void A_penalty_larger_than_the_allocation_leaves_nothing_to_grant()
    {
        Simulation simulation = TestSimulation.Build();
        Company company = simulation.FindCompany(1);

        // Year 1 with nothing on hand: the whole 4 Mt obligation is a shortfall.
        simulation.Compliance.Reconcile(company, 1);

        simulation.Ledger.GrantFreeAllocation(2);

        simulation.Ledger.Available(company, Vintage2).Should().Be(
            0m, "the 4 Mt debit is larger than the 3,317,400 t the company was due");
    }

    [Fact]
    public void A_year_can_only_be_reconciled_once_per_company()
    {
        (Simulation simulation, Company company) = Build();
        simulation.Compliance.Reconcile(company, 1);

        Action again = () => simulation.Compliance.Reconcile(company, 1);

        again.Should().Throw<InvalidOperationException>().WithMessage("*already*");
    }

    [Fact]
    public void Reconciling_a_whole_year_settles_every_company()
    {
        Simulation simulation = TestSimulation.Build();

        IReadOnlyList<CompanyCompliance> results = simulation.Compliance.Reconcile(1);

        results.Should().HaveCount(3);
        results.Select(result => result.Company.Name).Should().BeEquivalentTo(
            "Cong ty Dien luc Binh Minh", "Cong ty Xi mang Da Trang", "AI Power 1");
        simulation.Compliance.Results.Should().HaveCount(3);
    }

    [Fact]
    public void A_year_outside_the_run_cannot_be_reconciled()
    {
        (Simulation simulation, Company company) = Build();

        Action before = () => simulation.Compliance.Reconcile(company, 0);
        Action after = () => simulation.Compliance.Reconcile(company, 4);

        before.Should().Throw<ArgumentOutOfRangeException>();
        after.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void An_administrator_can_fine_a_unit()
    {
        (Simulation simulation, Company company) = Build();
        Unit unit = simulation.FindUnit(1);
        decimal capital = company.Capital;

        Fine fine = simulation.Compliance.IssueFine(unit, 2_500_000m, "Late surrender");

        fine.Amount.Should().Be(2_500_000m);
        fine.Description.Should().Be("Late surrender");
        fine.Unit.Should().BeSameAs(unit);
        fine.Year.Should().Be(simulation.CurrentYear);
        company.Capital.Should().Be(capital - 2_500_000m);
        simulation.Compliance.Fines.Should().ContainSingle().Which.Should().BeSameAs(fine);
    }

    [Fact]
    public void A_fine_needs_an_amount_and_a_description()
    {
        (Simulation simulation, _) = Build();
        Unit unit = simulation.FindUnit(1);

        Action noAmount = () => simulation.Compliance.IssueFine(unit, 0m, "Late surrender");
        Action noDescription = () => simulation.Compliance.IssueFine(unit, 1_000m, "  ");

        noAmount.Should().Throw<ArgumentOutOfRangeException>().WithMessage("*amount*");
        noDescription.Should().Throw<ArgumentException>().WithMessage("*description*");
    }

    [Fact]
    public void Abatement_operating_in_the_year_lowers_the_obligation()
    {
        (Simulation simulation, Company company) = Build();
        Unit unit = simulation.FindUnit(1);
        AbatementOption reduction = unit.AbatementOptions.Single(option => option.Code == "P1");
        simulation.Abatements.Implement(unit, reduction, year: 1);

        AllocationPlan plan = simulation.Allocation;
        decimal bareEmissions = plan.BausEmissionsFor(simulation.FindUnit(1), 2)
            + plan.BausEmissionsFor(simulation.FindUnit(2), 2);

        CompanyCompliance result = simulation.Compliance.Reconcile(company, 2);

        result.Obligation.Should().Be(bareEmissions - reduction.AnnualReduction);
    }
}
