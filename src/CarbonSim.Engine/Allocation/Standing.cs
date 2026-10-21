using CarbonSim.Engine.Abatement;
using CarbonSim.Engine.Compliance;
using CarbonSim.Engine.Domain;
using CarbonSim.Engine.Market;

namespace CarbonSim.Engine.Allocation;

/// <summary>
/// Where one company stands in one year: what the obligation is measured on, what it holds that
/// could meet it, and the position that follows. One module answers the question for a live year and
/// for a settled one, because the same question asked five different ways is how a screen comes to
/// show a figure the year end never acted on. A settled year answers from its settlement rather than
/// from a fresh recomputation, so a year that is over cannot drift.
/// </summary>
public sealed class Standing
{
    private readonly Simulation _simulation;

    public Standing(Simulation simulation)
    {
        ArgumentNullException.ThrowIfNull(simulation);

        _simulation = simulation;
    }

    /// <summary>
    /// The tonnes a year's obligation is measured on: business-as-usual less what abatement takes
    /// off, and nothing at all for a unit that is shut down for the year.
    /// </summary>
    public decimal Emissions(Unit unit, int year)
    {
        ArgumentNullException.ThrowIfNull(unit);

        if (_simulation.Abatements.IsShutDown(unit, year))
        {
            return 0m;
        }

        return _simulation.Allocation.BausEmissionsFor(unit, year) - _simulation.Abatements.ReductionsFor(unit, year);
    }

    /// <summary>The company's emissions for the year: every one of its units added up.</summary>
    public decimal Emissions(Company company, int year)
    {
        ArgumentNullException.ThrowIfNull(company);
        RequireYear(year);

        return company.Units.Sum(unit => Emissions(unit, year));
    }

    /// <summary>How much of a year's obligation may be met with offsets. Computed here, once.</summary>
    public decimal OffsetLimit(Company company, int year)
    {
        ArgumentNullException.ThrowIfNull(company);

        return Math.Round(Emissions(company, year) * _simulation.Rules.OffsetUsageLimit, 0, MidpointRounding.AwayFromZero);
    }

    /// <summary>Every offset the company holds, whether or not the year may use it.</summary>
    public decimal OffsetsHeld(Company company, int year)
    {
        ArgumentNullException.ThrowIfNull(company);
        RequireYear(year);

        return _simulation.Ledger.Available(company, Product.Offset)
            + _simulation.Ledger.Escrowed(company, Product.Offset);
    }

    /// <summary>
    /// The offsets the year may count, up to its limit: escrowed ones included, as for allowances. An
    /// offset on offer is still the company's until somebody buys it, so offering it leaves the
    /// position where it was (the original's header "ignores escrow"); <see cref="Available"/> is
    /// the figure that leaves it out.
    /// </summary>
    public decimal OffsetsUsable(Company company, int year)
    {
        ArgumentNullException.ThrowIfNull(company);

        return Math.Min(OffsetsHeld(company, year), OffsetLimit(company, year));
    }

    /// <summary>
    /// The allowances the year may be met from: every one of the year's vintage and older, escrowed
    /// ones included. This is the allowance side of <see cref="Held"/> on its own, for a caller that
    /// asks about allowances rather than about the whole position.
    /// </summary>
    public decimal HeldAllowances(Company company, int year)
    {
        ArgumentNullException.ThrowIfNull(company);
        RequireYear(year);

        return UsableVintages(year).Sum(product => HeldIn(company, product));
    }

    /// <summary>
    /// What the company holds against the year: the offsets it may use plus every allowance of the
    /// year's vintage and older, escrowed ones included, because a tonne set aside for a resting
    /// order or a bid is still the company's.
    /// </summary>
    public decimal Held(Company company, int year)
    {
        ArgumentNullException.ThrowIfNull(company);
        RequireYear(year);

        return OffsetsUsable(company, year) + HeldAllowances(company, year);
    }

    /// <summary>The same set, with what is set aside for resting orders and bids left out.</summary>
    public decimal Available(Company company, int year)
    {
        ArgumentNullException.ThrowIfNull(company);
        RequireYear(year);

        return Math.Min(
                _simulation.Ledger.Available(company, Product.Offset),
                OffsetLimit(company, year))
            + UsableVintages(year).Sum(product => _simulation.Ledger.Available(company, product));
    }

    /// <summary>
    /// The company's position for the year: held less what it emits. Long is positive (it could sell
    /// or bank), short is negative.
    /// </summary>
    public decimal Position(Company company, int year)
    {
        ArgumentNullException.ThrowIfNull(company);

        CompanyCompliance? settlement = Settlement(company, year);

        return settlement is not null
            ? -settlement.Shortfall
            : Held(company, year) - Emissions(company, year);
    }

    /// <summary>
    /// What the company still has to cover: the position the wrong way up, and never negative. A
    /// settled year answers from its settlement.
    /// </summary>
    public decimal Shortfall(Company company, int year)
    {
        ArgumentNullException.ThrowIfNull(company);

        CompanyCompliance? settlement = Settlement(company, year);

        return settlement is not null
            ? settlement.Shortfall
            : Math.Max(0m, Emissions(company, year) - Held(company, year));
    }

    /// <summary>Whether the year is covered: nothing left to surrender.</summary>
    public bool Covered(Company company, int year)
    {
        ArgumentNullException.ThrowIfNull(company);

        return Settlement(company, year)?.IsCompliant ?? Shortfall(company, year) <= 0m;
    }

    /// <summary>The year's settlement, or null while the year is still live.</summary>
    public CompanyCompliance? Settlement(Company company, int year)
    {
        ArgumentNullException.ThrowIfNull(company);
        RequireYear(year);

        return _simulation.Compliance.For(company, year);
    }

    /// <summary>Whether the year has been settled.</summary>
    public bool IsSettled(Company company, int year) => Settlement(company, year) is not null;

    /// <summary>The vintages a year's obligation may be met from: its own first, then older ones.</summary>
    public static IEnumerable<Product> UsableVintages(int year)
    {
        yield return Product.Allowance(year);

        for (int vintage = 0; vintage < year; vintage++)
        {
            yield return Product.Allowance(vintage);
        }
    }

    private decimal HeldIn(Company company, Product product) =>
        _simulation.Ledger.Available(company, product) + _simulation.Ledger.Escrowed(company, product);

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
