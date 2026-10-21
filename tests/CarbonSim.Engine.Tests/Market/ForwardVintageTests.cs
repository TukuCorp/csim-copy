using CarbonSim.Engine.Domain;
using CarbonSim.Engine.Market;
using CarbonSim.Engine.Scenarios;
using FluentAssertions;

namespace CarbonSim.Engine.Tests.Market;

/// <summary>
/// Forward vintages are a setting of the run, read wherever the schedule is built. The host used to
/// build its schedule with the share left at zero while every fidelity harness passed 0.20, so the
/// app played a run the envelopes never measured. These pin the one place the share comes from.
/// </summary>
public sealed class ForwardVintageTests
{
    private const decimal SharePerYear = 0.20m;

    /// <summary>The shipped scenario, loaded the way the host loads it.</summary>
    private static Simulation Shipped() =>
        ScenarioLoader.LoadFile(Path.Combine("scenarios", "vietnam-2024.json"));

    [Fact]
    public void The_shipped_scenario_sells_the_next_vintage_forward_in_every_auction_of_the_year()
    {
        // Year 1's first auction already offers year 1 and year 2 allowances (demo 07:11).
        Simulation simulation = Shipped();
        AuctionSchedule schedule = AuctionSchedule.Build(simulation);
        Auction[] yearOne = [.. schedule.Auctions.Where(auction => auction.Year == 1)];

        yearOne.Should().OnlyContain(
            auction => auction.Lots.Count == 2 && auction.Lots[0].Vintage == 1 && !auction.Lots[0].IsForward
                && auction.Lots[1].Vintage == 2 && auction.Lots[1].IsForward,
            "every auction of the year sells its own vintage and part of the next one");
        yearOne.Sum(auction => auction.Lots[1].Volume).Should().Be(
            Forward(simulation, 2),
            "the forward lots add up to the calibrated share of the next year's auctionable volume");

        // Nothing is invented and nothing is lost: the year still offers its own vintage in full.
        schedule.Auctions
            .Where(auction => auction.Year == 1)
            .Sum(auction => auction.Lots.Where(lot => !lot.IsForward).Sum(lot => lot.Volume))
            .Should().Be(simulation.Allocation.AuctionableVolumeForYear(1));
    }

    [Fact]
    public void A_scenario_that_never_mentions_the_share_still_plays_the_calibrated_run()
    {
        Simulation simulation = ScenarioLoader.LoadJson(Minimal, "forward-vintage-default");
        AuctionSchedule schedule = AuctionSchedule.Build(simulation);

        schedule.Auctions.Where(auction => auction.Year == 1)
            .Sum(auction => auction.Lots.Where(lot => lot.IsForward && lot.Vintage == 2).Sum(lot => lot.Volume))
            .Should().Be(Forward(simulation, 2));
        schedule.ForSection(2, 1).Lots.Should().ContainSingle("the last year has no next vintage to sell");
    }

    /// <summary>The share the calibration assumes, as a volume of the given year.</summary>
    private static decimal Forward(Simulation simulation, int vintage) =>
        Math.Round(SharePerYear * simulation.Allocation.AuctionableVolumeForYear(vintage), 0, MidpointRounding.AwayFromZero);

    /// <summary>A complete two-year scenario with nothing said about forward sales.</summary>
    private const string Minimal = """
        {
          "name": "Forward vintage default",
          "seed": 3,
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
