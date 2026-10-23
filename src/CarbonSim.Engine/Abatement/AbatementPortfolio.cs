using CarbonSim.Engine.Domain;
using CarbonSim.Engine.Finance;
using CarbonSim.Engine.Snapshot;

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

    /// <summary>
    /// Puts back what every unit had committed to. Each project is tied to the option instance
    /// on its own unit's menu, so the portfolio keeps recognising it as implemented and the
    /// unit cannot buy the same project twice.
    /// </summary>
    internal void Restore(AbatementPortfolioSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        _byUnit.Clear();
        _shutdowns.Clear();

        foreach (ImplementedAbatementSnapshot project in snapshot.Projects)
        {
            Unit unit = _simulation.FindUnit(project.UnitId);

            if (project.OptionIndex < 0 || project.OptionIndex >= unit.AbatementOptions.Count)
            {
                throw new ArgumentException(
                    $"Abatement {project.OptionIndex} of unit {project.UnitId} is not on that unit's menu.",
                    nameof(snapshot));
            }

            if (!_byUnit.TryGetValue(unit.Id, out List<ImplementedAbatement>? projects))
            {
                projects = [];
                _byUnit[unit.Id] = projects;
            }

            projects.Add(new ImplementedAbatement(
                unit,
                unit.AbatementOptions[project.OptionIndex],
                project.ImplementedIn,
                project.OperatingFromYear,
                project.ExpiresAfterYear,
                // A save from before part years were kept started its project at the turn of a year.
                project.StartsAt == 0m ? project.OperatingFromYear : project.StartsAt));
        }

        foreach (UnitYearSnapshot shutdown in snapshot.Shutdowns)
        {
            _shutdowns.Add((shutdown.UnitId, shutdown.Year));
        }
    }

    /// <summary>
    /// What an option would deliver if it were bought now, counted over what is left of the run rather
    /// than over its own life (FAQ #3-#4, the "Simulation" view): only the years it can operate before
    /// the last year closes. One that cannot be finished in time delivers nothing, so it has no cost
    /// per tonne and loses all its capital.
    /// </summary>
    public AbatementEconomics OverSimulation(AbatementOption option, int year)
    {
        ArgumentNullException.ThrowIfNull(option);
        RequireYear(year);

        decimal startsAt = year + ShareOfYear() + option.ImplementationYears;
        decimal runEnds = _simulation.Allocation.Years.Count + 1;
        decimal operating = Math.Clamp(runEnds - startsAt, 0m, option.LifetimeYears);
        decimal tonnes = option.AnnualReduction * operating;
        decimal revenue = option.AnnualNetRevenue * operating;

        return new AbatementEconomics(
            operating,
            tonnes,
            tonnes > 0m ? (option.UpfrontCost - revenue) / tonnes : null,
            option.UpfrontCost == 0m ? null : (revenue - option.UpfrontCost) / option.UpfrontCost);
    }

    /// <summary>
    /// Where the clock stands in the year, from 0 to 1. A build time runs from the moment of purchase,
    /// so this is part of when a project starts.
    /// </summary>
    private decimal ShareOfYear() => _simulation.Rules.YearLength > TimeSpan.Zero
        ? Math.Clamp((decimal)(_simulation.YearOffset / _simulation.Rules.YearLength), 0m, 1m)
        : 0m;

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

        ImplementedAbatement project = ImplementedAbatement.Bought(unit, option, year, ShareOfYear());

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
