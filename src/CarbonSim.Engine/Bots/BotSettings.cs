namespace CarbonSim.Engine.Bots;

/// <summary>
/// How sharp a bot is. The original exposes an "AI Difficulty" setting; the three levels map
/// onto the same handful of instincts with different confidence, noise and reaction speed.
/// </summary>
public enum BotDifficulty
{
    Easy,
    Normal,
    Hard,
}

/// <summary>
/// One bot's instincts, straight out of the design brief: the margin it demands before it will
/// abate, how much noise it puts into its prices, how much of its shortfall it goes after at
/// auction, how deeply it discounts offsets, and what it will pay on the secondary market.
/// </summary>
/// <remarks>
/// These are placeholders until the fidelity suite calibrates them against the published
/// exercises; only their ordering is meaningful today, not their exact values.
/// </remarks>
public sealed record BotSettings(
    BotDifficulty Difficulty,
    decimal AbatementMargin,
    decimal BidPriceNoise,
    decimal BidVolumeFraction,
    decimal OffsetDiscount,
    decimal ReservationPriceFactor)
{
    /// <summary>Abatement is worth doing while it costs less than the expected price times the margin.</summary>
    public static BotSettings For(BotDifficulty difficulty)
    {
        return difficulty switch
        {
            BotDifficulty.Easy => new BotSettings(difficulty, 0.70m, 0.10m, 0.50m, 0.25m, 0.90m),
            BotDifficulty.Hard => new BotSettings(difficulty, 1.00m, 0.02m, 1.00m, 0.05m, 1.05m),
            _ => new BotSettings(difficulty, 0.85m, 0.05m, 0.70m, 0.15m, 1.00m),
        };
    }
}
