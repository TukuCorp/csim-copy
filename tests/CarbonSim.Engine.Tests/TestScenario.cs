using CarbonSim.Engine.Domain;

namespace CarbonSim.Engine.Tests;

/// <summary>
/// Scenario values used across the engine tests. The numbers mirror the Vietnam pilot
/// settings described in the exercise documentation (cap ~356 Mt, 3%/yr, 90% free,
/// 4 auctions, 100/300 collar, 10% offsets, penalty 300 plus one allowance).
/// </summary>
internal static class TestScenario
{
    public static Parameters GreenParameters()
    {
        return new Parameters
        {
            Cap = 355_850_000m,
            AnnualCapReductionRate = 0.03m,
            FreeAllocationShare = 0.90m,
            Years = 3,
            OffsetUsageLimit = 0.10m,
            BankingLimit = 1.00m,
            PenaltyPerTonne = 300_000m,
            PenaltyAllowanceDebit = 1m,
            AuctionFloorPrice = 100_000m,
            AuctionCeilingPrice = 300_000m,
            AuctionsPerYear = 4,
            YearLength = TimeSpan.FromMinutes(20),
            AuctionDuration = TimeSpan.FromMinutes(3),
            TradingOpenShareOfYear = 0.45m,
            VolatilityBand = 0.10m,
            OverdraftInterestRate = 0.07m,
            BausGrowthBySector =
            [
                new SectorBausGrowth("Power", 0.02m, 0.06m),
                new SectorBausGrowth("Cement", 0.02m, 0.04m),
            ],
        };
    }
}
