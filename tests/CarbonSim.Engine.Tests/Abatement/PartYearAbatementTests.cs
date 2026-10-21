using CarbonSim.Engine.Abatement;
using CarbonSim.Engine.Clock;
using CarbonSim.Engine.Domain;
using CarbonSim.Engine.Scenarios;
using FluentAssertions;

namespace CarbonSim.Engine.Tests.Abatement;

/// <summary>
/// Build times run from three quarters of a year to five years in the original, and a project
/// starts cutting emissions the day it is finished: a 0.75-year retrofit bought on January 1 takes
/// a quarter of a year's tonnes off that same year (demo 05:06-06:08). A project's year is counted
/// from the moment it was bought, not from the start of the next whole year.
/// </summary>
public sealed class PartYearAbatementTests
{
    private const decimal AnnualReduction = 5_000m;

    [Fact]
    public void A_three_quarter_year_build_bought_on_january_1_cuts_a_quarter_of_that_years_tonnes()
    {
        Simulation simulation = ScenarioLoader.LoadJson(Scenario, "part-year-abatement");
        SimulationClock clock = new(simulation, new TestTimeProvider());
        clock.Start();
        Unit unit = simulation.Units.Single();

        ImplementedAbatement project = simulation.Abatements.Implement(unit, unit.AbatementOptions.Single(), year: 1);

        project.ReductionIn(1).Should().Be(AnnualReduction * 0.25m, "it runs for the last quarter of year 1");
        project.ReductionIn(2).Should().Be(AnnualReduction);
        project.StatusIn(1).Should().NotBe(AbatementStatus.Building, "it is operating before the year is out");
        project.ExpiresAfterYear.Should().Be(3, "two years of service from three quarters into year 1 end three quarters into year 3");
        project.ReductionIn(3).Should().Be(AnnualReduction * 0.75m);
    }

    [Fact]
    public void A_project_bought_halfway_through_the_year_starts_halfway_through_its_first_operating_year()
    {
        Simulation simulation = ScenarioLoader.LoadJson(Scenario, "part-year-abatement");
        TestTimeProvider time = new();
        SimulationClock clock = new(simulation, time);
        clock.Start();
        time.Advance(simulation.Rules.YearLength / 2);
        Unit unit = simulation.Units.Single();

        ImplementedAbatement project = simulation.Abatements.Implement(unit, unit.AbatementOptions.Single(), year: 1);

        project.ReductionIn(1).Should().Be(0m, "a 0.75-year build bought at mid-year is not finished by December");
        project.ReductionIn(2).Should().Be(AnnualReduction * 0.75m, "it is finished a quarter of the way into year 2");
    }

    private const string Scenario = """
        {
          "name": "Part-year abatement",
          "seed": 5,
          "parameters": {
            "cap": 1000000,
            "annualCapReductionRate": 0.03,
            "freeAllocationShare": 0.9,
            "years": 3,
            "offsetUsageLimit": 0.1,
            "bankingLimit": 1.0,
            "penaltyPerTonne": 300,
            "penaltyAllowanceDebit": 1,
            "auctionFloorPrice": 100,
            "auctionCeilingPrice": 300,
            "auctionsPerYear": 4,
            "yearLengthMinutes": 20,
            "auctionDurationMinutes": 3,
            "volatilityBand": 0.1,
            "overdraftInterestRate": 0.07,
            "bausGrowthBySector": [
              { "sector": "Power", "minAnnualRate": 0.0, "maxAnnualRate": 0.0 }
            ]
          },
          "sectors": [
            {
              "name": "Power",
              "emissionShare": 1.0,
              "abatementOptions": [
                {
                  "code": "R1",
                  "name": "Burner retrofit",
                  "annualReductionShare": 0.05,
                  "upfrontCostPerTonne": 10,
                  "annualNetRevenuePerTonne": 0,
                  "implementationYears": 0.75,
                  "lifetimeYears": 2
                }
              ]
            }
          ],
          "companies": [
            {
              "name": "Delta Power",
              "sector": "Power",
              "ownerKind": "human",
              "capital": 100000000,
              "overdraftLimit": 0,
              "units": [
                { "name": "Delta Unit 1", "baselineEmissions": 100000, "normalOperatingProfit": 4000000 }
              ]
            }
          ]
        }
        """;
}
