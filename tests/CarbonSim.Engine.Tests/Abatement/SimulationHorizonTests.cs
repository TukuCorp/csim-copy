using CarbonSim.Engine.Abatement;
using CarbonSim.Engine.Clock;
using CarbonSim.Engine.Domain;
using FluentAssertions;

namespace CarbonSim.Engine.Tests.Abatement;

/// <summary>
/// The original's Abatement table can be read over the simulation or over the project's lifetime
/// (FAQ #3-#4). Over the simulation, a project counts only the years it can operate before the run
/// ends, so the same project looks dearer as the years pass, and one that cannot be finished in
/// time has no cost per tonne and a forecast return of -100%.
/// </summary>
public sealed class SimulationHorizonTests
{
    // The power menu's P1: built in one year, it runs for ten, far longer than the three-year run.
    private static (Simulation Simulation, TestTimeProvider Time, Unit Unit, AbatementOption Option) Build()
    {
        Simulation simulation = TestSimulation.Build(years: 3);
        TestTimeProvider time = new();
        new SimulationClock(simulation, time).Start();
        Unit unit = simulation.FindUnit(1);

        return (simulation, time, unit, unit.AbatementOptions.Single(option => option.Code == "P1"));
    }

    [Fact]
    public void Bought_on_january_1_of_year_1_a_one_year_build_operates_for_the_last_two_years()
    {
        (Simulation simulation, _, _, AbatementOption option) = Build();

        AbatementEconomics economics = simulation.Abatements.OverSimulation(option, year: 1);

        economics.OperatingYears.Should().Be(2m);
        economics.Tonnes.Should().Be(option.AnnualReduction * 2m);
        economics.NetCostPerTonne.Should().Be(((option.UpfrontCost - (option.AnnualNetRevenue * 2m)) / (option.AnnualReduction * 2m)));
        economics.ForecastRoi.Should().Be(((option.AnnualNetRevenue * 2m) - option.UpfrontCost) / option.UpfrontCost);
    }

    [Fact]
    public void The_same_project_bought_later_counts_fewer_years_and_costs_more_per_tonne()
    {
        (Simulation simulation, TestTimeProvider time, _, AbatementOption option) = Build();
        AbatementEconomics early = simulation.Abatements.OverSimulation(option, year: 1);

        time.Advance(simulation.Rules.YearLength / 2);
        AbatementEconomics midYear = simulation.Abatements.OverSimulation(option, year: 1);
        AbatementEconomics yearTwo = simulation.Abatements.OverSimulation(option, year: 2);

        midYear.OperatingYears.Should().Be(1.5m, "bought half way through year 1, it starts half way through year 2");
        yearTwo.OperatingYears.Should().Be(0.5m);
        midYear.NetCostPerTonne.Should().BeGreaterThan(early.NetCostPerTonne!.Value);
        yearTwo.NetCostPerTonne.Should().BeGreaterThan(midYear.NetCostPerTonne!.Value);
    }

    [Fact]
    public void A_project_that_cannot_be_finished_before_the_end_has_no_cost_per_tonne_and_loses_everything()
    {
        (Simulation simulation, _, _, AbatementOption option) = Build();

        AbatementEconomics economics = simulation.Abatements.OverSimulation(option, year: 3);

        economics.OperatingYears.Should().Be(0m);
        economics.Tonnes.Should().Be(0m);
        economics.NetCostPerTonne.Should().BeNull("there are no reductions to divide the cost by");
        economics.ForecastRoi.Should().Be(-1m, "the capital is spent and nothing comes back");
    }

    [Fact]
    public void A_project_whose_life_ends_inside_the_run_counts_only_its_life()
    {
        Simulation simulation = TestSimulation.Build(years: 20);
        new SimulationClock(simulation, new TestTimeProvider()).Start();
        AbatementOption option = simulation.FindUnit(1).AbatementOptions.Single(candidate => candidate.Code == "P1");

        AbatementEconomics economics = simulation.Abatements.OverSimulation(option, year: 1);

        economics.OperatingYears.Should().Be(option.LifetimeYears);
        economics.NetCostPerTonne.Should().Be(option.NetCostPerTonne, "over its whole life it is the lifetime figure");
        economics.ForecastRoi.Should().Be(option.ForecastRoi);
    }
}
