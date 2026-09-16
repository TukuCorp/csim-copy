using CarbonSim.Engine.Domain;

namespace CarbonSim.Engine.Abatement;

/// <summary>
/// A project a unit has committed to. Once implemented it cannot be undone: it is built over
/// its implementation time, then reduces emissions every year of its lifetime, feeding the
/// company's cash flow as it goes.
/// </summary>
public sealed class ImplementedAbatement
{
    internal ImplementedAbatement(Unit unit, AbatementOption option, int implementedIn)
    {
        Unit = unit;
        Option = option;
        ImplementedIn = implementedIn;
        OperatingFromYear = implementedIn + option.ImplementationYears;
        ExpiresAfterYear = OperatingFromYear + option.LifetimeYears - 1;
    }

    public Unit Unit { get; }

    public AbatementOption Option { get; }

    /// <summary>The year the capital was spent.</summary>
    public int ImplementedIn { get; }

    /// <summary>The first year the project reduces emissions.</summary>
    public int OperatingFromYear { get; }

    /// <summary>The last year the project reduces emissions.</summary>
    public int ExpiresAfterYear { get; }

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
        return year >= OperatingFromYear && year <= ExpiresAfterYear ? Option.AnnualReduction : 0m;
    }

    /// <summary>Money out of the company in a year: the capital once, then net revenue or cost.</summary>
    public decimal CashFlowIn(int year)
    {
        if (year == ImplementedIn)
        {
            return -Option.UpfrontCost;
        }

        return OperatingRevenueIn(year);
    }

    /// <summary>The project's running revenue or cost in a year, ignoring the capital it cost.</summary>
    public decimal OperatingRevenueIn(int year)
    {
        return year >= OperatingFromYear && year <= ExpiresAfterYear ? Option.AnnualNetRevenue : 0m;
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
