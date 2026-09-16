using CarbonSim.Engine.Allocation;
using CarbonSim.Engine.Domain;
using FluentAssertions;

namespace CarbonSim.Engine.Tests.Allocation;

public sealed class CompliancePositionTests
{
    [Fact]
    public void A_unit_that_emits_more_than_its_free_allocation_is_short()
    {
        Simulation simulation = TestSimulation.Build();
        CompliancePosition position = new(simulation);
        Unit unit = simulation.FindUnit(1);

        decimal allocation = simulation.Allocation.FreeAllocationFor(unit, 1);
        decimal bau = simulation.Allocation.BausEmissionsFor(unit, 1);

        bau.Should().BeGreaterThan(allocation);
        position.ForecastShortfallFor(unit, 1).Should().Be(bau - allocation);
    }

    [Fact]
    public void A_company_position_is_the_sum_of_its_units()
    {
        Simulation simulation = TestSimulation.Build();
        CompliancePosition position = new(simulation);
        Company company = simulation.FindCompany(1);

        position.ForecastShortfallFor(company, 1).Should().Be(580_000m);
        position.ForecastShortfallFor(company, 1).Should().Be(
            company.Units.Sum(unit => position.ForecastShortfallFor(unit, 1)));
    }

    [Fact]
    public void The_whole_run_position_adds_up_the_years()
    {
        Simulation simulation = TestSimulation.Build(years: 3);
        CompliancePosition position = new(simulation);
        Company company = simulation.FindCompany(1);

        decimal perYear = position.ForecastShortfallFor(company, 1)
            + position.ForecastShortfallFor(company, 2)
            + position.ForecastShortfallFor(company, 3);

        position.OverallShortfallFor(company).Should().Be(perYear);
        position.OverallShortfallFor(company).Should().BeGreaterThan(0m, "a 3%/yr cap cut on growing emissions means the company is short overall");
    }

    [Fact]
    public void A_position_can_only_be_forecast_inside_the_run()
    {
        Simulation simulation = TestSimulation.Build();
        CompliancePosition position = new(simulation);
        Company company = simulation.FindCompany(1);

        Action before = () => position.ForecastShortfallFor(company, 0);
        Action after = () => position.ForecastShortfallFor(company, 99);

        before.Should().Throw<ArgumentOutOfRangeException>();
        after.Should().Throw<ArgumentOutOfRangeException>();
    }
}
