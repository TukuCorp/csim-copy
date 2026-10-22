using CarbonSim.Engine.Domain;
using CarbonSim.Engine.Market;
using CarbonSim.Engine.Run;
using CarbonSim.Engine.Scenarios;
using FluentAssertions;

namespace CarbonSim.Engine.Tests.Allocation;

/// <summary>
/// The original's scenarios carry their own offset supply ("starting offsets", "total offsets
/// available"), with some companies holding more than they may surrender and selling the rest.
/// The supply is part of the plan, granted with each year's free allocation, so a run on the host
/// has an offset market without the trainer seeding one by hand.
/// </summary>
public sealed class OffsetSupplyTests
{
    [Fact]
    public void A_unit_with_an_offset_supply_receives_that_share_of_its_emissions_with_each_years_allocation()
    {
        Simulation simulation = ScenarioLoader.LoadJson(Scenario, "offset-supply");
        SimulationRun run = SimulationRun.Create(simulation, new TestTimeProvider());
        Company provider = simulation.Companies.Single(company => company.Name == "Provider Power");
        Company plain = simulation.Companies.Single(company => company.Name == "Plain Power");

        run.Start();

        simulation.Allocation.OffsetSupplyFor(provider, 1).Should().Be(20_000m, "a fifth of its 100,000 t");
        simulation.Ledger.Available(provider, Product.Offset).Should().Be(20_000m);
        simulation.Ledger.Available(plain, Product.Offset).Should().Be(0m, "a unit that names no supply gets none");
    }

    private const string Scenario = """
        {
          "name": "Offset supply",
          "seed": 5,
          "parameters": {
            "cap": 1000000,
            "annualCapReductionRate": 0.03,
            "freeAllocationShare": 0.9,
            "years": 2,
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
                  "code": "P1",
                  "name": "Boiler efficiency",
                  "annualReductionShare": 0.01,
                  "upfrontCostPerTonne": 30,
                  "annualNetRevenuePerTonne": 0,
                  "implementationYears": 1,
                  "lifetimeYears": 10
                }
              ]
            }
          ],
          "companies": [
            {
              "name": "Provider Power",
              "sector": "Power",
              "ownerKind": "ai",
              "capital": 100000000,
              "overdraftLimit": 0,
              "units": [
                { "name": "Provider Unit", "baselineEmissions": 100000, "offsetSupplyShare": 0.2 }
              ]
            },
            {
              "name": "Plain Power",
              "sector": "Power",
              "ownerKind": "ai",
              "capital": 100000000,
              "overdraftLimit": 0,
              "units": [
                { "name": "Plain Unit", "baselineEmissions": 100000 }
              ]
            }
          ]
        }
        """;
}
