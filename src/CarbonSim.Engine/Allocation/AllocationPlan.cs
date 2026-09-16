using CarbonSim.Engine.Domain;

namespace CarbonSim.Engine.Allocation;

/// <summary>
/// Everything about how many allowances exist and who gets them, worked out once when the
/// simulation is created: the cap for every year, each sector's free cake, each unit's share
/// of it, and the business-as-usual emissions each unit is heading for. Nothing here changes
/// while the simulation is played, so a unit's allocation for every year can be shown on day
/// one, exactly as the original promises.
/// </summary>
/// <remarks>
/// All quantities are whole tonnes. A year's free cake is split across sectors by emission
/// share, and each sector's cake across its units by baseline emissions, both using largest
/// remainders so nothing is invented or lost to rounding at either step; ties go to the lower
/// sector position or unit id, which keeps runs reproducible.
/// </remarks>
public sealed class AllocationPlan
{
    private readonly decimal[] _caps;
    private readonly decimal[] _freeAllocation;
    private readonly Dictionary<int, decimal[]> _allocationByUnit;
    private readonly Dictionary<int, decimal[]> _bauByUnit;
    private readonly Dictionary<int, decimal> _growthByUnit;

    internal AllocationPlan(Simulation simulation)
    {
        ArgumentNullException.ThrowIfNull(simulation);

        Parameters parameters = simulation.TradingSystems.Single().Parameters;
        Years = [.. Enumerable.Range(1, parameters.Years)];

        _caps = new decimal[parameters.Years];
        _freeAllocation = new decimal[parameters.Years];
        _allocationByUnit = [];
        _bauByUnit = [];
        _growthByUnit = [];

        Dictionary<string, SectorBausGrowth> growthBySector = parameters.BausGrowthBySector
            .ToDictionary(growth => growth.Sector, StringComparer.Ordinal);

        foreach (Unit unit in simulation.Units)
        {
            if (!growthBySector.TryGetValue(unit.Sector.Name, out SectorBausGrowth? growth))
            {
                throw new InvalidOperationException($"Sector '{unit.Sector.Name}' has no business-as-usual growth band.");
            }

            // Drawn in unit id order so the stream, and therefore the run, is reproducible.
            _growthByUnit[unit.Id] = simulation.Random.NextDecimal(growth.MinAnnualRate, growth.MaxAnnualRate);
        }

        for (int year = 1; year <= parameters.Years; year++)
        {
            _caps[year - 1] = Round(parameters.Cap * Pow(1m - parameters.AnnualCapReductionRate, year - 1));
            _freeAllocation[year - 1] = Round(_caps[year - 1] * parameters.FreeAllocationShare);
        }

        foreach (int year in Years)
        {
            decimal freeCake = _freeAllocation[year - 1];
            Sector[] yearSectors = [.. simulation.Sectors];
            decimal shareTotal = yearSectors.Sum(sector => sector.EmissionShare);
            decimal[] sectorCakes = shareTotal > 0m
                ? Split(freeCake, [.. yearSectors.Select(sector => sector.EmissionShare / shareTotal)])
                : new decimal[yearSectors.Length];

            for (int index = 0; index < yearSectors.Length; index++)
            {
                Sector sector = yearSectors[index];
                Unit[] units = [.. simulation.Units.Where(unit => ReferenceEquals(unit.Sector, sector))];
                decimal sectorBaseline = units.Sum(unit => unit.BaselineEmissions);
                decimal[] unitCakes = units.Length > 0 && sectorBaseline > 0m
                    ? Split(sectorCakes[index], [.. units.Select(unit => unit.BaselineEmissions / sectorBaseline)])
                    : new decimal[units.Length];

                for (int unitIndex = 0; unitIndex < units.Length; unitIndex++)
                {
                    Unit unit = units[unitIndex];
                    AllocationFor(unit, year)[year - 1] = unitCakes[unitIndex];
                    BausFor(unit, year)[year - 1] = Round(unit.BaselineEmissions * Pow(1m + _growthByUnit[unit.Id], year - 1));
                }
            }
        }
    }

    /// <summary>The virtual years the plan covers, in order.</summary>
    public IReadOnlyList<int> Years { get; }

    /// <summary>The trading system's cap for a year, in tonnes CO2e.</summary>
    public decimal CapForYear(int year) => _caps[YearIndex(year)];

    /// <summary>The part of the cap handed out free across all sectors in a year.</summary>
    public decimal FreeAllocationForYear(int year) => _freeAllocation[YearIndex(year)];

    /// <summary>The part of the cap the government keeps to auction, in a year.</summary>
    public decimal AuctionableVolumeForYear(int year) => CapForYear(year) - FreeAllocationForYear(year);

    /// <summary>A unit's free allocation for a year; fixed up front and identical every year in share.</summary>
    public decimal FreeAllocationFor(Unit unit, int year)
    {
        ArgumentNullException.ThrowIfNull(unit);

        return AllocationFor(unit, year)[YearIndex(year)];
    }

    public decimal FreeAllocationFor(Company company, int year)
    {
        ArgumentNullException.ThrowIfNull(company);

        YearIndex(year);

        return company.Units.Sum(unit => FreeAllocationFor(unit, year));
    }

    /// <summary>The unit's business-as-usual emissions in a year, grown from its baseline.</summary>
    public decimal BausEmissionsFor(Unit unit, int year)
    {
        ArgumentNullException.ThrowIfNull(unit);

        return BausFor(unit, year)[YearIndex(year)];
    }

    public decimal BausEmissionsFor(Company company, int year)
    {
        ArgumentNullException.ThrowIfNull(company);

        YearIndex(year);

        return company.Units.Sum(unit => BausEmissionsFor(unit, year));
    }

    /// <summary>The annual growth rate drawn for a unit from its sector's band.</summary>
    public decimal BausGrowthFor(Unit unit)
    {
        ArgumentNullException.ThrowIfNull(unit);

        return _growthByUnit.TryGetValue(unit.Id, out decimal growth)
            ? growth
            : throw new KeyNotFoundException($"Unit {unit.Id} is not part of this allocation plan.");
    }

    private decimal[] AllocationFor(Unit unit, int year)
    {
        _ = year;

        if (!_allocationByUnit.TryGetValue(unit.Id, out decimal[]? allocation))
        {
            allocation = new decimal[Years.Count];
            _allocationByUnit[unit.Id] = allocation;
        }

        return allocation;
    }

    private decimal[] BausFor(Unit unit, int year)
    {
        _ = year;

        if (!_bauByUnit.TryGetValue(unit.Id, out decimal[]? bau))
        {
            bau = new decimal[Years.Count];
            _bauByUnit[unit.Id] = bau;
        }

        return bau;
    }

    private int YearIndex(int year)
    {
        return year >= 1 && year <= Years.Count
            ? year - 1
            : throw new ArgumentOutOfRangeException(nameof(year), year, $"This simulation runs for {Years.Count} years.");
    }

    /// <summary>
    /// Splits <paramref name="total"/> by <paramref name="shares"/> so the parts add up to the
    /// total exactly: each part gets its floor, and the leftover tonnes go to the largest
    /// remainders, ties first by position.
    /// </summary>
    private static decimal[] Split(decimal total, IReadOnlyList<decimal> shares)
    {
        decimal[] parts = new decimal[shares.Count];
        decimal[] remainders = new decimal[shares.Count];
        decimal assigned = 0m;

        for (int index = 0; index < shares.Count; index++)
        {
            decimal exact = total * shares[index];
            parts[index] = decimal.Floor(exact);
            remainders[index] = exact - parts[index];
            assigned += parts[index];
        }

        int leftover = (int)(total - assigned);
        int[] order = [.. Enumerable.Range(0, shares.Count)
            .OrderByDescending(index => remainders[index])
            .ThenBy(index => index)];

        for (int index = 0; index < leftover; index++)
        {
            parts[order[index]] += 1m;
        }

        return parts;
    }

    private static decimal Pow(decimal value, int exponent)
    {
        decimal result = 1m;

        for (int power = 0; power < exponent; power++)
        {
            result *= value;
        }

        return result;
    }

    private static decimal Round(decimal value) => Math.Round(value, 0, MidpointRounding.AwayFromZero);
}
