using CarbonSim.Engine.Domain;

namespace CarbonSim.Engine.Abatement;

/// <summary>
/// A project a unit has committed to. Once implemented it cannot be undone: it is built over
/// its implementation time, counted from the moment it was bought, then reduces emissions for its
/// lifetime, feeding the company's cash flow as it goes. A year in which it runs for only part of
/// the time gets that part of its annual reduction and revenue.
/// </summary>
/// <remarks>
/// Points in the run are counted in years: year Y runs from Y to Y + 1, so a project bought a
/// quarter of the way into year 1 with a 0.75-year build starts operating at 2.0.
/// </remarks>
public sealed class ImplementedAbatement
{
    /// <summary>A project bought in a year, at a share of the way through it (0 = January 1).</summary>
    internal static ImplementedAbatement Bought(Unit unit, AbatementOption option, int implementedIn, decimal implementedAtShareOfYear)
    {
        ArgumentNullException.ThrowIfNull(option);

        decimal startsAt = implementedIn + implementedAtShareOfYear + option.ImplementationYears;

        return new ImplementedAbatement(
            unit,
            option,
            implementedIn,
            (int)decimal.Floor(startsAt),
            (int)decimal.Ceiling(startsAt + option.LifetimeYears) - 1,
            startsAt);
    }

    /// <summary>
    /// Rebuilds a project with the years it was stored with, rather than working them out from
    /// its option again: the running years are part of what the run committed to, so they are
    /// put back exactly as they were.
    /// </summary>
    internal ImplementedAbatement(
        Unit unit,
        AbatementOption option,
        int implementedIn,
        int operatingFromYear,
        int expiresAfterYear,
        decimal startsAt)
    {
        Unit = unit;
        Option = option;
        ImplementedIn = implementedIn;
        OperatingFromYear = operatingFromYear;
        ExpiresAfterYear = expiresAfterYear;
        StartsAt = startsAt;
    }

    public Unit Unit { get; }

    public AbatementOption Option { get; }

    /// <summary>The year the capital was spent.</summary>
    public int ImplementedIn { get; }

    /// <summary>The first year the project reduces emissions, for some or all of it.</summary>
    public int OperatingFromYear { get; }

    /// <summary>The last year the project reduces emissions, for some or all of it.</summary>
    public int ExpiresAfterYear { get; }

    /// <summary>The point in the run the project starts operating (year 2 begins at 2.0).</summary>
    public decimal StartsAt { get; }

    /// <summary>The share of a year the project operates in: none, part, or all of it.</summary>
    public decimal OperatingShareIn(int year)
    {
        decimal from = Math.Max(StartsAt, year);
        decimal to = Math.Min(StartsAt + Option.LifetimeYears, year + 1);

        return to > from ? to - from : 0m;
    }

    public AbatementStatus StatusIn(int year)
    {
        if (year < OperatingFromYear)
        {
            return AbatementStatus.Building;
        }

        if (year > ExpiresAfterYear)
        {
            return AbatementStatus.Expired;
        }

        return CumulativeNetIn(year) >= 0m ? AbatementStatus.InProfit : AbatementStatus.Operating;
    }

    /// <summary>Tonnes CO2e the project removes in a year; zero while building or after expiry.</summary>
    public decimal ReductionIn(int year)
    {
        return Option.AnnualReduction * OperatingShareIn(year);
    }

    /// <summary>Money out of the company in a year: the capital once, and net revenue or cost while it runs.</summary>
    public decimal CashFlowIn(int year)
    {
        return (year == ImplementedIn ? -Option.UpfrontCost : 0m) + OperatingRevenueIn(year);
    }

    /// <summary>The project's running revenue or cost in a year, ignoring the capital it cost.</summary>
    public decimal OperatingRevenueIn(int year)
    {
        return Option.AnnualNetRevenue * OperatingShareIn(year);
    }

    /// <summary>Everything this project has cost or earned from <see cref="ImplementedIn"/> through a year.</summary>
    public decimal CumulativeNetIn(int year)
    {
        if (year < ImplementedIn)
        {
            return 0m;
        }

        decimal total = CashFlowIn(ImplementedIn);

        for (int current = ImplementedIn + 1; current <= year; current++)
        {
            total += CashFlowIn(current);
        }

        return total;
    }

    public override string ToString() => $"{Option.Code} on {Unit.Name} from {OperatingFromYear}";
}
