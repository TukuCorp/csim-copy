using CarbonSim.Engine.Domain;
using CarbonSim.Engine.Market;
using CarbonSim.Engine.Scenarios;
using CarbonSim.Web.Simulations;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace CarbonSim.Web.Tests.Simulations;

/// <summary>
/// What this pins: the host builds every run's schedule from the run's own settings, so a run that
/// asks for forward sales gets them. It used to build the schedule with the share left at zero while
/// the fidelity harnesses passed 0.20 by hand, which meant the live app played a different run from
/// the one the envelopes measure.
/// </summary>
public sealed class ForwardVintageHostTests : IClassFixture<SimulationTestHost>
{
    private readonly SimulationTestHost _host;

    public ForwardVintageHostTests(SimulationTestHost host)
    {
        _host = host;
    }

    [Fact]
    public void A_host_built_run_offers_the_forward_vintage_its_scenario_asks_for()
    {
        SimulationRegistry runs = _host.Server.Services.GetRequiredService<SimulationRegistry>();
        Simulation simulation = ScenarioLoader.LoadJson(Scenario, "forward-vintage-host");
        Guid id = runs.Start(simulation);
        Auction[] yearOne = [runs.Run(id).Auctions.ForSection(1, 1), runs.Run(id).Auctions.ForSection(1, 2)];

        yearOne.Should().OnlyContain(
            auction => auction.Lots.Count == 2 && auction.Lots[1].Vintage == 2 && auction.Lots[1].IsForward,
            "each of the year's auctions sells part of the next vintage early");
        yearOne.Sum(auction => auction.Lots[1].Volume).Should().Be(
            Math.Round(0.20m * simulation.Allocation.AuctionableVolumeForYear(2), 0, MidpointRounding.AwayFromZero));
    }

    /// <summary>Two auctions a year, each selling half of year 1's forward share of vintage 2.</summary>
    private const string Scenario = """
        {
          "name": "Forward vintage host run",
          "seed": 23,
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
            "auctionsPerYear": 2,
            "yearLengthMinutes": 20,
            "auctionDurationMinutes": 3,
            "volatilityBand": 0.1,
            "overdraftInterestRate": 0.07,
            "forwardVintageShare": 0.20,
            "bausGrowthBySector": [
              { "sector": "Power", "minAnnualRate": 0.02, "maxAnnualRate": 0.06 }
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
                  "annualReductionShare": 0.05,
                  "upfrontCostPerTonne": 100,
                  "annualNetRevenuePerTonne": -2,
                  "implementationYears": 1,
                  "lifetimeYears": 10
                }
              ]
            }
          ],
          "companies": [
            {
              "name": "Delta Power",
              "sector": "Power",
              "ownerKind": "human",
              "capital": 1000000,
              "overdraftLimit": 0,
              "units": [
                { "name": "Delta Unit 1", "baselineEmissions": 100000, "normalOperatingProfit": 4000000 }
              ]
            }
          ]
        }
        """;
}
