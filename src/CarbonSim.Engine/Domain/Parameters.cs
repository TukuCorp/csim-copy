using System.Globalization;

namespace CarbonSim.Engine.Domain;

/// <summary>
/// The numbers an administrator configures for one trading system before the simulation
/// starts. Held by the trading system, not by the scenario, so a future multi-system
/// simulation can run each ETS under its own rules.
/// </summary>
/// <remarks>
/// Value equality covers the fixed-width members; <see cref="BausGrowthBySector"/> is a
/// list, so two parameter sets are only equal when they share that list instance. Compare
/// fields explicitly when testing scenario round-trips.
/// </remarks>
public sealed record Parameters
{
    /// <summary>Year-1 cap in tonnes CO2e; later years follow <see cref="AnnualCapReductionRate"/>.</summary>
    public required decimal Cap { get; init; }

    /// <summary>Share the cap falls by each year (0.03 = -3%/yr).</summary>
    public required decimal AnnualCapReductionRate { get; init; }

    /// <summary>Share of the cap handed out free, per sector, fixed for all years up front.</summary>
    public required decimal FreeAllocationShare { get; init; }

    /// <summary>Number of virtual years in the simulation.</summary>
    public required int Years { get; init; }

    /// <summary>Business-as-usual growth band per sector, keyed by sector name.</summary>
    public required IReadOnlyList<SectorBausGrowth> BausGrowthBySector { get; init; }

    /// <summary>Share of a year's obligation that may be met with offsets.</summary>
    public required decimal OffsetUsageLimit { get; init; }

    /// <summary>Share of the obligation a company may carry into the next year (1.0 = all of it).</summary>
    public required decimal BankingLimit { get; init; }

    /// <summary>Cash penalty per tonne of uncovered obligation, in simulation currency.</summary>
    public required decimal PenaltyPerTonne { get; init; }

    /// <summary>Allowances debited from the next year's allocation per tonne of uncovered obligation.</summary>
    public required decimal PenaltyAllowanceDebit { get; init; }

    /// <summary>Lowest price an auction may clear at.</summary>
    public required decimal AuctionFloorPrice { get; init; }

    /// <summary>Highest price an auction may clear at.</summary>
    public required decimal AuctionCeilingPrice { get; init; }

    /// <summary>Number of auctions held inside one virtual year.</summary>
    public required int AuctionsPerYear { get; init; }

    /// <summary>Wall-clock length of one virtual year.</summary>
    public required TimeSpan YearLength { get; init; }

    /// <summary>Wall-clock length of a single auction window.</summary>
    public required TimeSpan AuctionDuration { get; init; }

    /// <summary>Share of the year during which auctions are open (0.45 = 45%).</summary>
    public required decimal TradingOpenShareOfYear { get; init; }

    /// <summary>How far a trade may move from the last price before it is rejected (0.10 = +/-10%).</summary>
    public required decimal VolatilityBand { get; init; }

    /// <summary>Yearly interest charged on a negative balance (0.07 = 7%/yr).</summary>
    public required decimal OverdraftInterestRate { get; init; }

    /// <summary>
    /// Returns one message per value that cannot be run, so an administrator sees every
    /// problem with a scenario in a single pass. Empty means the parameters are usable.
    /// </summary>
    public IReadOnlyList<string> Validate()
    {
        List<string> errors = [];

        if (Cap <= 0m)
        {
            errors.Add(Message("cap", "must be greater than zero", Cap));
        }

        if (AnnualCapReductionRate is < 0m or >= 1m)
        {
            errors.Add(Message("annualCapReductionRate", "must be between 0 and 1", AnnualCapReductionRate));
        }

        if (FreeAllocationShare is < 0m or > 1m)
        {
            errors.Add(Message("freeAllocationShare", "must be between 0 and 1", FreeAllocationShare));
        }

        if (Years < 1)
        {
            errors.Add(Message("years", "must be at least 1", Years));
        }

        ValidateBausGrowth(errors);

        if (OffsetUsageLimit is < 0m or > 1m)
        {
            errors.Add(Message("offsetUsageLimit", "must be between 0 and 1", OffsetUsageLimit));
        }

        if (BankingLimit is < 0m or > 1m)
        {
            errors.Add(Message("bankingLimit", "must be between 0 and 1", BankingLimit));
        }

        if (PenaltyPerTonne < 0m)
        {
            errors.Add(Message("penaltyPerTonne", "must not be negative", PenaltyPerTonne));
        }

        if (PenaltyAllowanceDebit < 0m)
        {
            errors.Add(Message("penaltyAllowanceDebit", "must not be negative", PenaltyAllowanceDebit));
        }

        if (AuctionFloorPrice < 0m)
        {
            errors.Add(Message("auctionFloorPrice", "must not be negative", AuctionFloorPrice));
        }

        if (AuctionCeilingPrice <= AuctionFloorPrice)
        {
            errors.Add(string.Create(
                CultureInfo.InvariantCulture,
                $"auctionCeilingPrice must be greater than auctionFloorPrice (was {AuctionCeilingPrice} against {AuctionFloorPrice})."));
        }

        if (AuctionsPerYear < 1)
        {
            errors.Add(Message("auctionsPerYear", "must be at least 1", AuctionsPerYear));
        }

        if (YearLength <= TimeSpan.Zero)
        {
            errors.Add("yearLength must be greater than zero.");
        }

        if (AuctionDuration <= TimeSpan.Zero)
        {
            errors.Add(Message("auctionDuration", "must be greater than zero", AuctionDuration));
        }
        else if (YearLength > TimeSpan.Zero && AuctionDuration >= YearLength)
        {
            errors.Add(string.Create(
                CultureInfo.InvariantCulture,
                $"auctionDuration must be shorter than yearLength (was {AuctionDuration} against {YearLength})."));
        }

        if (TradingOpenShareOfYear is <= 0m or > 1m)
        {
            errors.Add(Message("tradingOpenShareOfYear", "must be greater than 0 and at most 1", TradingOpenShareOfYear));
        }

        if (VolatilityBand is <= 0m or > 1m)
        {
            errors.Add(Message("volatilityBand", "must be greater than 0 and at most 1", VolatilityBand));
        }

        if (OverdraftInterestRate is < 0m or > 1m)
        {
            errors.Add(Message("overdraftInterestRate", "must be between 0 and 1", OverdraftInterestRate));
        }

        return errors;
    }

    private void ValidateBausGrowth(List<string> errors)
    {
        if (BausGrowthBySector.Count == 0)
        {
            errors.Add("bausGrowthBySector must give a growth band for at least one sector.");
            return;
        }

        foreach (SectorBausGrowth growth in BausGrowthBySector)
        {
            if (string.IsNullOrWhiteSpace(growth.Sector))
            {
                errors.Add("bausGrowthBySector entries must name a sector.");
                continue;
            }

            if (growth.MinAnnualRate <= -1m)
            {
                errors.Add(string.Create(
                    CultureInfo.InvariantCulture,
                    $"bausGrowthBySector entry for '{growth.Sector}' must have rates greater than -1 (was {growth.MinAnnualRate})."));
            }

            if (growth.MinAnnualRate > growth.MaxAnnualRate)
            {
                errors.Add(string.Create(
                    CultureInfo.InvariantCulture,
                    $"bausGrowthBySector entry for '{growth.Sector}' must have minAnnualRate no greater than maxAnnualRate (was {growth.MinAnnualRate} to {growth.MaxAnnualRate})."));
            }
        }
    }

    private static string Message<T>(string field, string requirement, T value)
        where T : struct, IFormattable
    {
        return string.Create(
            CultureInfo.InvariantCulture,
            $"{field} {requirement} (was {value}).");
    }
}
