using CarbonSim.Engine.Allocation;
using CarbonSim.Engine.Domain;
using CarbonSim.Engine.Finance;
using CarbonSim.Engine.Market;

namespace CarbonSim.Engine.Compliance;

/// <summary>
/// Year-end reconciliation. For each company: work out what its units emitted, surrender
/// offsets against it first (never more than the offset limit allows), then allowances of the
/// compliance year and older vintages, then either bank what is left — up to one year's
/// obligation — or forfeit the excess. Anything the company could not cover is a shortfall:
/// a cash penalty, plus a debit against next year's free allocation.
/// </summary>
/// <remarks>
/// The penalty is charged whatever the company's credit limit, because the regulator does not
/// extend credit; everything else in the engine refuses spending that would breach the limit.
/// </remarks>
public sealed class ComplianceRegister
{
    private readonly Simulation _simulation;
    private readonly List<CompanyCompliance> _results = [];
    private readonly List<Fine> _fines = [];
    private readonly HashSet<(int CompanyId, int Year)> _reconciled = [];

    internal ComplianceRegister(Simulation simulation)
    {
        _simulation = simulation;
    }

    public IReadOnlyList<CompanyCompliance> Results => _results;

    public IReadOnlyList<Fine> Fines => _fines;

    /// <summary>The compliance year the simulation is in, or the last one that ended.</summary>
    public int CurrentYear => _simulation.CurrentYear;

    /// <summary>Reconciles one company for one year.</summary>
    public CompanyCompliance Reconcile(Company company, int year)
    {
        ArgumentNullException.ThrowIfNull(company);
        RequireYear(year);

        if (!_reconciled.Add((company.Id, year)))
        {
            throw new InvalidOperationException(
                $"Company '{company.Name}' has already been reconciled for year {year}.");
        }

        Parameters parameters = _simulation.TradingSystems.Single().Parameters;
        decimal obligation = new CompliancePosition(_simulation).EmissionsFor(company, year);

        (decimal offsetsSurrendered, decimal allowancesSurrendered, decimal shortfall) = Cover(company, year, obligation, parameters);

        decimal penaltyCash = shortfall * parameters.PenaltyPerTonne;
        decimal penaltyAllowanceDebit = year < _simulation.Allocation.Years.Count
            ? shortfall * parameters.PenaltyAllowanceDebit
            : 0m;

        if (penaltyCash > 0m)
        {
            // Penalties are taken in full: a company cannot dodge one by having no credit left.
            _simulation.Cash.Charge(
                company,
                penaltyCash,
                CashCategory.Penalty,
                $"Shortfall of {shortfall} tonnes in year {year}");
        }

        (decimal banked, decimal forfeited) = Bank(company, year, obligation, parameters);

        CompanyCompliance result = new(
            year,
            company,
            obligation,
            offsetsSurrendered,
            allowancesSurrendered,
            banked,
            forfeited,
            shortfall,
            penaltyCash,
            penaltyAllowanceDebit);

        _results.Add(result);

        return result;
    }

    /// <summary>Reconciles every company for a year.</summary>
    public IReadOnlyList<CompanyCompliance> Reconcile(int year)
    {
        RequireYear(year);

        return [.. _simulation.Companies.Select(company => Reconcile(company, year))];
    }

    /// <summary>Allowances a company still owes the regulator out of its allocation for a year.</summary>
    public decimal PenaltyAllowanceDebitFor(Company company, int year)
    {
        ArgumentNullException.ThrowIfNull(company);

        return _results
            .Where(result => ReferenceEquals(result.Company, company) && result.Year == year - 1)
            .Sum(result => result.PenaltyAllowanceDebit);
    }

    /// <summary>Imposes a fine on a unit, taking the money from its company straight away.</summary>
    public Fine IssueFine(Unit unit, decimal amount, string description)
    {
        ArgumentNullException.ThrowIfNull(unit);

        if (amount <= 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(amount), amount, "A fine amount must be greater than zero.");
        }

        if (string.IsNullOrWhiteSpace(description))
        {
            throw new ArgumentException("A fine needs a description.", nameof(description));
        }

        _simulation.Cash.Charge(unit.Company, amount, CashCategory.Fine, $"{description.Trim()} ({unit.Name})");

        Fine fine = new(_fines.Count + 1, unit, unit.Company, amount, description.Trim(), CurrentYear);
        _fines.Add(fine);

        return fine;
    }

    private (decimal Offsets, decimal Allowances, decimal Shortfall) Cover(
        Company company,
        int year,
        decimal obligation,
        Parameters parameters)
    {
        decimal offsetLimit = Round(obligation * parameters.OffsetUsageLimit);
        decimal offsets = Math.Min(_simulation.Ledger.Available(company, Product.Offset), offsetLimit);

        if (offsets > 0m)
        {
            _simulation.Ledger.Consume(company, Product.Offset, offsets);
        }

        decimal needed = obligation - offsets;
        decimal allowances = 0m;

        foreach (Product product in UsableVintages(year))
        {
            if (needed <= 0m)
            {
                break;
            }

            decimal available = _simulation.Ledger.Available(company, product);
            decimal take = Math.Min(available, needed);

            if (take <= 0m)
            {
                continue;
            }

            _simulation.Ledger.Consume(company, product, take);
            allowances += take;
            needed -= take;
        }

        return (offsets, allowances, needed < 0m ? 0m : needed);
    }

    private (decimal Banked, decimal Forfeited) Bank(
        Company company,
        int year,
        decimal obligation,
        Parameters parameters)
    {
        decimal held = UsableVintages(year).Sum(product => _simulation.Ledger.Available(company, product));

        if (held <= 0m)
        {
            return (0m, 0m);
        }

        // A company with no emissions has no obligation to measure a banking cap against.
        decimal cap = obligation <= 0m ? held : Round(obligation * parameters.BankingLimit);
        decimal banked = Math.Min(held, cap);
        decimal forfeited = held - banked;

        if (forfeited <= 0m)
        {
            return (banked, 0m);
        }

        decimal remaining = forfeited;

        foreach (Product product in UsableVintages(year).Reverse())
        {
            if (remaining <= 0m)
            {
                break;
            }

            decimal available = _simulation.Ledger.Available(company, product);
            decimal take = Math.Min(available, remaining);

            if (take <= 0m)
            {
                continue;
            }

            _simulation.Ledger.Consume(company, product, take);
            remaining -= take;
        }

        return (banked, forfeited);
    }

    /// <summary>The compliance year's vintage first, then older vintages oldest first.</summary>
    private static IEnumerable<Product> UsableVintages(int year)
    {
        yield return Product.Allowance(year);

        for (int vintage = 0; vintage < year; vintage++)
        {
            yield return Product.Allowance(vintage);
        }
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

    private static decimal Round(decimal value) => Math.Round(value, 0, MidpointRounding.AwayFromZero);
}
