using CarbonSim.Engine.Abatement;
using CarbonSim.Engine.Allocation;
using CarbonSim.Engine.Domain;
using FluentAssertions;

namespace CarbonSim.Engine.Tests.Abatement;

public sealed class AbatementPortfolioTests
{
    private const decimal UnitBaseline = 2_000_000m;

    private static AbatementOption Option(Unit unit, string code)
    {
        return unit.AbatementOptions.Single(option => option.Code == code);
    }

    [Fact]
    public void A_project_sized_for_a_unit_shows_its_tonnes_and_money()
    {
        Unit unit = TestSimulation.Build().FindUnit(1);
        AbatementOption p1 = Option(unit, "P1");

        p1.AnnualReduction.Should().Be(UnitBaseline * 0.05m);
        p1.UpfrontCost.Should().Be(10_000_000m);
        p1.AnnualNetRevenue.Should().Be(-200_000m);
        p1.ImplementationYears.Should().Be(1);
        p1.LifetimeYears.Should().Be(10);
    }

    [Fact]
    public void Implementing_a_project_pays_its_upfront_cost_and_starts_building()
    {
        Simulation simulation = TestSimulation.Build();
        Unit unit = simulation.FindUnit(1);
        Company company = unit.Company;
        decimal capital = company.Capital;

        ImplementedAbatement project = simulation.Abatements.Implement(unit, Option(unit, "P1"), year: 1);

        company.Capital.Should().Be(capital - 10_000_000m);
        project.ImplementedIn.Should().Be(1);
        project.OperatingFromYear.Should().Be(2);
        project.StatusIn(1).Should().Be(AbatementStatus.Building);
        project.ReductionIn(1).Should().Be(0m, "a project removes nothing while it is being built");
        simulation.Abatements.For(unit).Should().ContainSingle().Which.Should().BeSameAs(project);
    }

    [Fact]
    public void A_project_reduces_emissions_while_it_operates_and_stops_at_the_end_of_its_lifetime()
    {
        Simulation simulation = TestSimulation.Build();
        Unit unit = simulation.FindUnit(1);
        ImplementedAbatement project = simulation.Abatements.Implement(unit, Option(unit, "P1"), year: 1);

        project.ReductionIn(2).Should().Be(100_000m);
        project.ReductionIn(3).Should().Be(100_000m);
        project.ExpiresAfterYear.Should().Be(11);
        project.ReductionIn(11).Should().Be(100_000m);
        project.ReductionIn(12).Should().Be(0m);
        project.StatusIn(12).Should().Be(AbatementStatus.Expired);
    }

    [Fact]
    public void A_project_that_earns_its_capital_back_is_in_profit()
    {
        Simulation simulation = TestSimulation.Build();
        Unit unit = simulation.FindUnit(1);
        ImplementedAbatement project = simulation.Abatements.Implement(unit, Option(unit, "P3"), year: 1);

        project.CashFlowIn(1).Should().Be(-1_000_000m);
        project.CashFlowIn(2).Should().Be(5_000_000m);
        project.StatusIn(2).Should().Be(AbatementStatus.InProfit);
        project.CumulativeNetIn(2).Should().Be(4_000_000m);
    }

    [Fact]
    public void A_project_that_costs_money_to_run_stays_operating()
    {
        Simulation simulation = TestSimulation.Build();
        Unit unit = simulation.FindUnit(1);
        ImplementedAbatement project = simulation.Abatements.Implement(unit, Option(unit, "P1"), year: 1);

        project.StatusIn(2).Should().Be(AbatementStatus.Operating);
        project.StatusIn(3).Should().Be(AbatementStatus.Operating);
    }

    [Fact]
    public void Cost_per_tonne_and_forecast_roi_describe_the_project()
    {
        Unit unit = TestSimulation.Build().FindUnit(1);

        AbatementOption p1 = Option(unit, "P1");
        p1.CapitalCostPerTonne.Should().Be(10m);
        p1.NetCostPerTonne.Should().Be(12m);
        p1.ForecastRoi.Should().Be(-1.2m);

        AbatementOption p3 = Option(unit, "P3");
        p3.CapitalCostPerTonne.Should().Be(1m);
        p3.NetCostPerTonne.Should().Be(-49m);
        p3.ForecastRoi.Should().Be(49m);
    }

    [Fact]
    public void Reductions_from_several_projects_are_added_together()
    {
        Simulation simulation = TestSimulation.Build();
        Unit unit = simulation.FindUnit(1);

        simulation.Abatements.Implement(unit, Option(unit, "P1"), year: 1);
        simulation.Abatements.Implement(unit, Option(unit, "P3"), year: 1);

        simulation.Abatements.ReductionsFor(unit, 1).Should().Be(0m, "both are still being built");
        simulation.Abatements.ReductionsFor(unit, 2).Should().Be(200_000m);
    }

    [Fact]
    public void A_project_cannot_be_implemented_twice()
    {
        Simulation simulation = TestSimulation.Build();
        Unit unit = simulation.FindUnit(1);
        AbatementOption p1 = Option(unit, "P1");
        simulation.Abatements.Implement(unit, p1, year: 1);

        Action again = () => simulation.Abatements.Implement(unit, p1, year: 2);

        again.Should().Throw<InvalidOperationException>().WithMessage("*already*");
    }

    [Fact]
    public void A_unit_can_only_implement_projects_from_its_own_menu()
    {
        Simulation simulation = TestSimulation.Build();
        Unit cementUnit = simulation.FindUnit(4);
        Unit powerUnit = simulation.FindUnit(1);
        AbatementOption cementOnly = Option(cementUnit, "C1");

        Action implement = () => simulation.Abatements.Implement(powerUnit, cementOnly, year: 1);

        implement.Should().Throw<ArgumentException>().WithMessage("*C1*");
    }

    [Fact]
    public void Implementing_is_refused_when_the_company_cannot_afford_it()
    {
        Simulation simulation = TestSimulation.Build();
        Unit unit = simulation.FindUnit(1);
        AbatementOption expensive = Option(unit, "P2");

        Action implement = () => simulation.Abatements.Implement(unit, expensive, year: 1);

        implement.Should().Throw<InvalidOperationException>().WithMessage("*capital*");
        unit.Company.Capital.Should().Be(TestSimulation.CompanyCapital);
        simulation.Abatements.For(unit).Should().BeEmpty();
    }

    [Fact]
    public void A_project_implemented_later_still_builds_before_it_operates()
    {
        Simulation simulation = TestSimulation.Build();
        Unit unit = simulation.FindUnit(1);

        ImplementedAbatement project = simulation.Abatements.Implement(unit, Option(unit, "P1"), year: 2);

        project.StatusIn(2).Should().Be(AbatementStatus.Building);
        project.StatusIn(3).Should().Be(AbatementStatus.Operating);
    }

    [Fact]
    public void Implemented_projects_reduce_the_forecast_shortfall_for_the_company()
    {
        Simulation simulation = TestSimulation.Build();
        CompliancePosition position = new(simulation);
        Unit unit = simulation.FindUnit(1);
        Company company = unit.Company;
        decimal beforeYearOne = position.ForecastShortfallFor(company, 1);
        decimal beforeYearTwo = position.ForecastShortfallFor(company, 2);
        decimal beforeOverall = position.OverallShortfallFor(company);

        simulation.Abatements.Implement(unit, Option(unit, "P1"), year: 1);

        position.ForecastShortfallFor(company, 1).Should().Be(beforeYearOne, "the project is still being built in year 1");
        position.ForecastShortfallFor(company, 2).Should().Be(beforeYearTwo - 100_000m);
        position.OverallShortfallFor(company).Should().Be(
            beforeOverall - 200_000m,
            "the project operates in years 2 and 3 of a three year run");
    }

    [Fact]
    public void Reductions_never_exceed_what_the_unit_emits()
    {
        // Two projects that each remove 60% of the unit's emissions: together they would claim
        // more than the unit emits, so the claim is capped at what is actually there.
        Simulation simulation = OverlappingSimulation();
        Unit unit = simulation.FindUnit(1);

        simulation.Abatements.Implement(unit, unit.AbatementOptions.Single(option => option.Code == "X1"), year: 1);
        simulation.Abatements.Implement(unit, unit.AbatementOptions.Single(option => option.Code == "X2"), year: 1);

        unit.AbatementOptions.Sum(option => option.AnnualReduction).Should().Be(1_200_000m);
        simulation.Abatements.ReductionsFor(unit, 1).Should().Be(1_000_000m);
    }

    private static Simulation OverlappingSimulation()
    {
        Sector power = new("Power", 1m,
        [
            new AbatementMenu("X1", "Waste heat recovery", 0.6m, 10m, 0m, 0, 5),
            new AbatementMenu("X2", "Fuel switch", 0.6m, 10m, 0m, 0, 5),
        ]);

        Player human = new(1, "Alice", PlayerKind.Human);
        Company company = new(1, "Overlapping Power", power, human, 100_000_000m);
        company.AddUnit(new Unit(1, "U1", company, 1_000_000m, AbatementMenu.For(power, 1_000_000m)));

        TradingSystem system = new(1, "ETS", TestSimulation.Parameters() with { Cap = 900_000m }, [power], [company]);

        return Simulation.Create("Overlapping", 21UL, [system], [human]);
    }

    private static Simulation RichSimulation()
    {
        // A company with enough capital for the 1.2 bn carbon capture retrofit.
        return TestSimulation.Build(capital: 5_000_000_000m);
    }

    [Fact]
    public void A_unit_can_be_shut_down_for_one_year_only()
    {
        Simulation simulation = TestSimulation.Build();
        Unit unit = simulation.FindUnit(1);
        CompliancePosition position = new(simulation);
        decimal allocation = simulation.Allocation.FreeAllocationFor(unit, 2);

        simulation.Abatements.ShutdownForYear(unit, 2);

        simulation.Abatements.IsShutDown(unit, 2).Should().BeTrue();
        simulation.Abatements.IsShutDown(unit, 1).Should().BeFalse();
        simulation.Abatements.IsShutDown(unit, 3).Should().BeFalse();
        position.ForecastShortfallFor(unit, 2).Should().Be(-allocation, "a shut down unit emits nothing but keeps its free allocation");
        position.ForecastShortfallFor(unit, 3).Should().BeGreaterThan(0m);
    }

    [Fact]
    public void A_shut_down_unit_cannot_claim_abatement_reductions()
    {
        Simulation simulation = TestSimulation.Build();
        Unit unit = simulation.FindUnit(1);
        simulation.Abatements.Implement(unit, Option(unit, "P1"), year: 1);
        simulation.Abatements.ShutdownForYear(unit, 2);

        simulation.Abatements.ReductionsFor(unit, 2).Should().Be(0m);
        simulation.Abatements.ReductionsFor(unit, 3).Should().Be(100_000m);
    }

    [Fact]
    public void A_unit_cannot_be_shut_down_twice_in_the_same_year_or_outside_the_run()
    {
        Simulation simulation = TestSimulation.Build();
        Unit unit = simulation.FindUnit(1);
        simulation.Abatements.ShutdownForYear(unit, 1);

        Action twice = () => simulation.Abatements.ShutdownForYear(unit, 1);
        Action beyond = () => simulation.Abatements.ShutdownForYear(unit, 9);

        twice.Should().Throw<InvalidOperationException>().WithMessage("*shut down*");
        beyond.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Options_can_be_implemented_up_to_the_limit_of_the_company_capital()
    {
        Simulation simulation = RichSimulation();
        Unit unit = simulation.FindUnit(1);
        AbatementOption p2 = Option(unit, "P2");

        ImplementedAbatement project = simulation.Abatements.Implement(unit, p2, year: 1);

        project.Option.UpfrontCost.Should().Be(1_200_000_000m);
        unit.Company.Capital.Should().Be(5_000_000_000m - 1_200_000_000m);
        project.StatusIn(2).Should().Be(AbatementStatus.Building, "the retrofit takes two years");
        project.ReductionIn(3).Should().Be(400_000m);
    }
}
