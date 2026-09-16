using CarbonSim.Engine.Abatement;
using CarbonSim.Engine.Domain;

namespace CarbonSim.Engine.Allocation;

/// <summary>
/// The forecast long/short position: what a unit or company still has to cover in a year,
/// being its business-as-usual emissions less the allowances it was given free. A positive
/// number is short (must buy, abate or surrender), a negative one is long (can sell or bank).
/// </summary>
public sealed class CompliancePosition
{
    private readonly Simulation _simulation;

    public CompliancePosition(Simulation simulation)
    {
        ArgumentNullException.ThrowIfNull(simulation);

        _simulation = simulation;
    }

    public decimal ForecastShortfallFor(Unit unit, int year) => ShortfallFor(unit, year);

    /// <summary>The tonnes a unit emits in a year: shuttered units emit nothing, and abatement comes off.</summary>
    public decimal EmissionsFor(Unit unit, int year)
    {
        ArgumentNullException.ThrowIfNull(unit);

        AllocationPlan plan = _simulation.Allocation;
        AbatementPortfolio abatements = _simulation.Abatements;

        if (abatements.IsShutDown(unit, year))
        {
            return 0m;
        }

        return plan.BausEmissionsFor(unit, year) - abatements.ReductionsFor(unit, year);
    }

    public decimal EmissionsFor(Company company, int year)
    {
        ArgumentNullException.ThrowIfNull(company);

        RequireYear(year);

        return company.Units.Sum(unit => EmissionsFor(unit, year));
    }

    public decimal ForecastShortfallFor(Company company, int year)
    {
        ArgumentNullException.ThrowIfNull(company);

        RequireYear(year);

        return company.Units.Sum(unit => ShortfallFor(unit, year));
    }

    /// <summary>The company's position over every year of the run.</summary>
    public decimal OverallShortfallFor(Company company)
    {
        ArgumentNullException.ThrowIfNull(company);

        return _simulation.Allocation.Years.Sum(year => ForecastShortfallFor(company, year));
    }

    private decimal ShortfallFor(Unit unit, int year)
    {
        AllocationPlan plan = _simulation.Allocation;

        return EmissionsFor(unit, year) - plan.FreeAllocationFor(unit, year);
    }

    private void RequireYear(int year)
    {
        if (year < 1 || year > _simulation.Allocation.Years.Count)
        {
            throw new ArgumentOutOfRangeException(
                nameof(year),
                year,
                $"This simulation runs for {_simulation.Allocation.Years.Count} years.");
        }
    }
}
