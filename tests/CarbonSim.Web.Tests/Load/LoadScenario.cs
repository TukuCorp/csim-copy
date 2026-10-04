using System.Globalization;
using System.Text;

namespace CarbonSim.Web.Tests.Load;

/// <summary>
/// The exercise the load harness plays, written for this project. Every company is human and owns
/// one unit, so the sessions the harness signs in are the only traders and the read path is put
/// under the whole of the load: a fleet of asking companies rather than a handful with bots
/// filling the room. The numbers are the shape of the trainer-deck parameters, not a copy of them.
/// </summary>
internal static class LoadScenario
{
    public const int AuctionsPerYear = 4;

    public static string Build(int companies, int yearLengthMinutes, int years)
    {
        StringBuilder roster = new();

        for (int index = 1; index <= companies; index++)
        {
            if (index > 1)
            {
                roster.Append(',');
            }

            roster.Append(CultureInfo.InvariantCulture, $$"""

                {
                  "name": "Load Co {{index:D3}}",
                  "sector": "Power",
                  "ownerKind": "human",
                  "capital": 1000000000,
                  "overdraftLimit": 0,
                  "units": [
                    { "name": "Load Unit {{index:D3}}", "baselineEmissions": 100000, "normalOperatingProfit": 4000000 }
                  ]
                }
                """);
        }

        // One section per auction; the auction window takes 45% of its section, the same reading of
        // the deck's week as the shipped scenario uses.
        double sectionMinutes = yearLengthMinutes / (double)AuctionsPerYear;
        double auctionMinutes = sectionMinutes * 0.45;

        return $$"""
        {
          "name": "Load run",
          "seed": 424242,
          "parameters": {
            "cap": {{companies * 100000L}},
            "annualCapReductionRate": 0.03,
            "freeAllocationShare": 0.9,
            "years": {{years}},
            "offsetUsageLimit": 0.1,
            "bankingLimit": 1.0,
            "penaltyPerTonne": 300,
            "penaltyAllowanceDebit": 1,
            "auctionFloorPrice": 100,
            "auctionCeilingPrice": 300,
            "auctionsPerYear": {{AuctionsPerYear}},
            "yearLengthMinutes": {{yearLengthMinutes}},
            "auctionDurationMinutes": {{auctionMinutes.ToString("0.####", CultureInfo.InvariantCulture)}},
            "tradingOpenShareOfYear": 0.45,
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
                  "upfrontCostPerTonne": 10000,
                  "annualNetRevenuePerTonne": -2,
                  "implementationYears": 1,
                  "lifetimeYears": 10
                }
              ]
            }
          ],
          "companies": [{{roster}}
          ]
        }
        """;
    }
}
