using CarbonSim.Engine.Domain;
using CarbonSim.Engine.Scenarios;

namespace CarbonSim.Web.Tests.Simulations;

/// <summary>Runs for the web tests, loaded through the public scenario loader so this project
/// never depends on the engine's internals.</summary>
internal static class TestRunFactory
{
    public const string DeltaCompany = "Delta Power";

    public const string RedRiverCompany = "Red River Power";

    /// <summary>Two human companies, so no bot interferes with the bids the test places. A
    /// 20-minute year splits into four auctions with a 3-minute window each.</summary>
    private const string Scenario = """
        {
          "name": "Hub test run",
          "seed": 7,
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
            "tradingOpenShareOfYear": 0.6,
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
              "capital": 1000000000,
              "overdraftLimit": 0,
              "units": [
                { "name": "Delta Unit 1", "baselineEmissions": 100000, "normalOperatingProfit": 4000000 }
              ]
            },
            {
              "name": "Red River Power",
              "sector": "Power",
              "ownerKind": "human",
              "capital": 1000000000,
              "overdraftLimit": 0,
              "units": [
                { "name": "Red River Unit 1", "baselineEmissions": 100000, "normalOperatingProfit": 4000000 }
              ]
            }
          ]
        }
        """;

    /// <summary>
    /// A two-sector run whose bots are forced to trade with each other on the exchange: Delta
    /// holds almost the whole free allocation and is long, while Red River holds a sliver and is
    /// short, so one sells and the other buys in the same tick. Both companies are human, and a
    /// test turns their AutoTrade switch on, which is what makes the host build bots for them.
    /// The abatement menus are far too expensive for a bot to touch, so only the exchange moves.
    /// </summary>
    private const string BotScenario = """
        {
          "name": "Hub bot run",
          "seed": 11,
          "parameters": {
            "cap": 1000000,
            "annualCapReductionRate": 0.03,
            "freeAllocationShare": 0.9,
            "years": 3,
            "offsetUsageLimit": 0.0,
            "bankingLimit": 1.0,
            "penaltyPerTonne": 300,
            "penaltyAllowanceDebit": 1,
            "auctionFloorPrice": 100,
            "auctionCeilingPrice": 300,
            "auctionsPerYear": 4,
            "yearLengthMinutes": 20,
            "auctionDurationMinutes": 3,
            "tradingOpenShareOfYear": 0.6,
            "volatilityBand": 0.1,
            "overdraftInterestRate": 0.07,
            "bausGrowthBySector": [
              { "sector": "Long", "minAnnualRate": 0.0, "maxAnnualRate": 0.0 },
              { "sector": "Short", "minAnnualRate": 0.0, "maxAnnualRate": 0.0 }
            ]
          },
          "sectors": [
            {
              "name": "Long",
              "emissionShare": 0.99,
              "abatementOptions": [
                {
                  "code": "L1",
                  "name": "Costly retrofit",
                  "annualReductionShare": 0.05,
                  "upfrontCostPerTonne": 10000,
                  "annualNetRevenuePerTonne": 0,
                  "implementationYears": 0,
                  "lifetimeYears": 1
                }
              ]
            },
            {
              "name": "Short",
              "emissionShare": 0.01,
              "abatementOptions": [
                {
                  "code": "S1",
                  "name": "Costly retrofit",
                  "annualReductionShare": 0.05,
                  "upfrontCostPerTonne": 10000,
                  "annualNetRevenuePerTonne": 0,
                  "implementationYears": 0,
                  "lifetimeYears": 1
                }
              ]
            }
          ],
          "companies": [
            {
              "name": "Delta Power",
              "sector": "Long",
              "ownerKind": "human",
              "capital": 1000000000,
              "overdraftLimit": 0,
              "units": [
                { "name": "Delta Unit 1", "baselineEmissions": 10000, "normalOperatingProfit": 4000000 }
              ]
            },
            {
              "name": "Red River Power",
              "sector": "Short",
              "ownerKind": "human",
              "capital": 1000000000,
              "overdraftLimit": 0,
              "units": [
                { "name": "Red River Unit 1", "baselineEmissions": 10000, "normalOperatingProfit": 4000000 }
              ]
            }
          ]
        }
        """;

    public static Simulation Build() => ScenarioLoader.LoadJson(Scenario, "hub-test");

    public static Simulation BuildBotRun() => ScenarioLoader.LoadJson(BotScenario, "hub-bot-test");

    /// <summary>The standard two-company scenario as a document, for the administrator's setup path.</summary>
    public static string ScenarioJson => Scenario;

    /// <summary>The same shape under another name and seed, so it is a second run and not the first.</summary>
    public static Simulation BuildOther() => ScenarioLoader.LoadJson(
        Scenario
            .Replace("\"name\": \"Hub test run\"", "\"name\": \"Hub other run\"", StringComparison.Ordinal)
            .Replace("\"seed\": 7", "\"seed\": 8", StringComparison.Ordinal),
        "hub-test-other");
}
