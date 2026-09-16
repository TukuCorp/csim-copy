using CarbonSim.Engine.Domain;
using CarbonSim.Engine.Finance;

namespace CarbonSim.Engine.Abatement;

/// <summary>
/// What every unit has committed to. Reductions are additive per the original's documented
/// simplification: projects do not interact with each other, they are simply summed and then
/// capped at what the unit actually emits, so a unit can never claim to have reduced more than
/// it produced. A unit may also be shut down for a single year, which removes its emissions
/// for that year and with them any reduction it could claim.
/// </summary>
public sealed class AbatementPortfolio
{
    private readonly Simulation _simulation;
    private readonly Dictionary<int, List<ImplementedAbatement>> _byUnit = [];
    private readonly HashSet<(int UnitId, int Year)> _shutdowns = [];

    internal AbatementPortfolio(Simulation simulation)
    {
        _simulation = simulation;
    }

    public IReadOnlyList<ImplementedAbatement> All =>
        [.. _byUnit.Values.SelectMany(projects => projects).OrderBy(project => project.ImplementedIn).ThenBy(project => project.Unit.Id)];

    public IReadOnlyList<ImplementedAbatement> For(Unit unit)
    {
        ArgumentNullException.ThrowIfNull(unit);

        return _byUnit.TryGetValue(unit.Id, out List<ImplementedAbatement>? projects) ? projects : [];
    }

    public bool HasImplemented(Unit unit, AbatementOption option)
    {
        ArgumentNullException.ThrowIfNull(unit);

        return For(unit).Any(project => ReferenceEquals(project.Option, option));
    }

    /// <summary>
    /// Commits the unit to a project from its own menu, paying the capital up front. There is
    /// no way back: an implemented project cannot be cancelled, refunded or repeated.
    /// </summary>
    public ImplementedAbatement Implement(Unit unit, AbatementOption option, int year)
    {
        ArgumentNullException.ThrowIfNull(unit);
        ArgumentNullException.ThrowIfNull(option);
        RequireYear(year);

        if (!unit.AbatementOptions.Any(candidate => ReferenceEquals(candidate, option)))
        {
            throw new ArgumentException(
                $"Abatement '{option.Code}' is not on the menu of unit '{unit.Name}'.",
                nameof(option));
        }

        if (HasImplemented(unit, option))
        {
            throw new InvalidOperationException(
                $"Abatement '{option.Code}' has already been implemented on unit '{unit.Name}' and cannot be undone.");
        }

        if (unit.Company.Capital < option.UpfrontCost)
        {
            throw new InvalidOperationException(
                $"Company '{unit.Company.Name}' has insufficient capital to implement abatement '{option.Code}' "
                + $"({unit.Company.Capital} against {option.UpfrontCost}).");
        }

        _simulation.Cash.Withdraw(
            unit.Company,
            option.UpfrontCost,
            CashCategory.AbatementCapital,
            $"{option.Code} {option.Name} on {unit.Name}");

        ImplementedAbatement project = new(unit, option, year);

        if (!_byUnit.TryGetValue(unit.Id, out List<ImplementedAbatement>? projects))
        {
            projects = [];
            _byUnit[unit.Id] = projects;
        }

        projects.Add(project);

        return project;
    }

    /// <summary>Tonnes CO2e the unit removes in a year, additive across projects and capped at its emissions.</summary>
    public decimal ReductionsFor(Unit unit, int year)
    {
        ArgumentNullException.ThrowIfNull(unit);
        RequireYear(year);

        if (IsShutDown(unit, year))
        {
            return 0m;
        }

        decimal claimed = For(unit).Sum(project => project.ReductionIn(year));
        decimal emitted = _simulation.Allocation.BausEmissionsFor(unit, year);

        return claimed > emitted ? emitted : claimed;
    }

    public decimal ReductionsFor(Company company, int year)
    {
        ArgumentNullException.ThrowIfNull(company);
        RequireYear(year);

        return company.Units.Sum(unit => ReductionsFor(unit, year));
    }

    /// <summary>Takes a unit out of service for one year; it emits nothing in that year.</summary>
    public void ShutdownForYear(Unit unit, int year)
    {
        ArgumentNullException.ThrowIfNull(unit);
        RequireYear(year);

        if (!_shutdowns.Add((unit.Id, year)))
        {
            throw new InvalidOperationException($"Unit '{unit.Name}' is already shut down in year {year}.");
        }
    }

    public bool IsShutDown(Unit unit, int year)
    {
        ArgumentNullException.ThrowIfNull(unit);
        RequireYear(year);

        return _shutdowns.Contains((unit.Id, year));
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
