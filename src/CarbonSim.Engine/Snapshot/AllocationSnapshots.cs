using CarbonSim.Engine.Domain;

namespace CarbonSim.Engine.Snapshot;

/// <summary>
/// The allocation plan exactly as it was computed at creation. The cap and the free cake are
/// per year, and each unit's slice and business-as-usual path are stored per year, so a
/// restore never repeats the split or the growth draw. The growth rates are carried for the
/// same reason: they came out of the simulation's random stream and re-drawing them would
/// shift every later draw in the run.
/// </summary>
public sealed record AllocationPlanSnapshot(
    IReadOnlyList<decimal> Caps,
    IReadOnlyList<decimal> FreeAllocation,
    IReadOnlyList<UnitAllocationSnapshot> Units);

/// <summary>One unit's place in the plan: the growth rate drawn for it and its year lines.</summary>
/// <remarks>
/// Both lists are indexed by year, position 0 being year 1, matching
/// <see cref="AllocationPlanSnapshot.Caps"/>.
/// </remarks>
public sealed record UnitAllocationSnapshot(
    int UnitId,
    decimal BausGrowth,
    IReadOnlyList<decimal> FreeAllocation,
    IReadOnlyList<decimal> BausEmissions);

/// <summary>What every unit has committed to: its implemented projects and its shutdown years.</summary>
public sealed record AbatementPortfolioSnapshot(
    IReadOnlyList<ImplementedAbatementSnapshot> Projects,
    IReadOnlyList<UnitYearSnapshot> Shutdowns);

/// <summary>
/// An implemented project. The unit and the position of the option in that unit's menu
/// identify which option it is: the option instance has to be the same one the unit holds, or
/// the portfolio would no longer recognise the project as implemented. The code is carried
/// alongside for readability; the position is what a restore uses.
/// </summary>
/// <remarks>
/// The project's cost and its annual reduction are not repeated here: a project has none of its
/// own, they come from the menu entry at <see cref="OptionIndex"/> in the unit's
/// <see cref="UnitSnapshot.AbatementOptions"/>, which is the single place they are stored.
/// </remarks>
public sealed record ImplementedAbatementSnapshot(
    int UnitId,
    int OptionIndex,
    string OptionCode,
    int ImplementedIn,
    int OperatingFromYear,
    int ExpiresAfterYear);

/// <summary>A year in which a unit was taken out of service.</summary>
public sealed record UnitYearSnapshot(int UnitId, int Year);
