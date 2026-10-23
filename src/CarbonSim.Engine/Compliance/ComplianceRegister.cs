using CarbonSim.Engine.Allocation;
using CarbonSim.Engine.Domain;
using CarbonSim.Engine.Finance;
using CarbonSim.Engine.Market;
using CarbonSim.Engine.Snapshot;

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
    private readonly Dictionary<(Company Company, int Year), CompanyCompliance> _byCompanyYear = [];
    private readonly List<Fine> _fines = [];
    private readonly HashSet<(int CompanyId, int Year)> _reconciled = [];
    private readonly Dictionary<(int CompanyId, int Year), SurrenderInstruction> _instructions = [];

    internal ComplianceRegister(Simulation simulation)
    {
        _simulation = simulation;
    }

    public IReadOnlyList<CompanyCompliance> Results => _results;

    /// <summary>
    /// The company's settlement for a year, or null while the year is live. One dictionary, so the
    /// question every caller asks is a lookup rather than a scan of the whole register. A register put
    /// back from a snapshot fills its results in one pass, so the dictionary is built from them the
    /// first time it is asked rather than maintained in two places.
    /// </summary>
    public CompanyCompliance? For(Company company, int year)
    {
        ArgumentNullException.ThrowIfNull(company);

        if (_byCompanyYear.TryGetValue((company, year), out CompanyCompliance? known))
        {
            return known;
        }

        CompanyCompliance? found = _results.FirstOrDefault(candidate =>
            ReferenceEquals(candidate.Company, company) && candidate.Year == year);

        if (found is not null)
        {
            _byCompanyYear[(company, year)] = found;
        }

        return found;
    }

    public IReadOnlyList<Fine> Fines => _fines;

    /// <summary>The compliance year the simulation is in, or the last one that ended.</summary>
    public int CurrentYear => _simulation.CurrentYear;

    /// <summary>
    /// Every reconciliation and fine, plus the set of company-years already reconciled. The set
    /// is captured as well as the results because it is what makes a second reconciliation
    /// throw rather than pay a company's obligation twice.
    /// </summary>
    internal ComplianceRegisterSnapshot ToSnapshot()
    {
        return new ComplianceRegisterSnapshot(
            [.. _results.Select(result => new CompanyComplianceSnapshot(
                result.Year,
                result.Company.Id,
                result.Obligation,
                result.OffsetsSurrendered,
                result.AllowancesSurrendered,
                result.Banked,
                result.Forfeited,
                result.Shortfall,
                result.PenaltyCash,
                result.PenaltyAllowanceDebit))],
            [.. _fines.Select(fine => new FineSnapshot(
                fine.Id,
                fine.Unit.Id,
                fine.Company.Id,
                fine.Amount,
                fine.Description,
                fine.Year))],
            [.. _reconciled
                .OrderBy(pair => pair.CompanyId)
                .ThenBy(pair => pair.Year)
                .Select(pair => new CompanyYearSnapshot(pair.CompanyId, pair.Year))],
            [.. _instructions.Values
                .OrderBy(instruction => instruction.Company.Id)
                .ThenBy(instruction => instruction.Year)
                .Select(instruction => new SurrenderInstructionSnapshot(
                    instruction.Company.Id,
                    instruction.Year,
                    instruction.Allowances,
                    instruction.Offsets))]);
    }

    /// <summary>
    /// Puts back the results, fines and reconciled company-years a snapshot was taken with.
    /// Nothing is reconciled again here: reconciling surrenders instruments and charges cash,
    /// so a restore that replayed it would move the very balances the snapshot already holds.
    /// </summary>
    internal void Restore(ComplianceRegisterSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        _results.Clear();
        _fines.Clear();
        _reconciled.Clear();
        _instructions.Clear();

        foreach (CompanyComplianceSnapshot result in snapshot.Results)
        {
            _results.Add(new CompanyCompliance(
                result.Year,
                _simulation.FindCompany(result.CompanyId),
                result.Obligation,
                result.OffsetsSurrendered,
                result.AllowancesSurrendered,
                result.Banked,
                result.Forfeited,
                result.Shortfall,
                result.PenaltyCash,
                result.PenaltyAllowanceDebit));
        }

        foreach (FineSnapshot fine in snapshot.Fines)
        {
            _fines.Add(new Fine(
                fine.Id,
                _simulation.FindUnit(fine.UnitId),
                _simulation.FindCompany(fine.CompanyId),
                fine.Amount,
                fine.Description,
                fine.Year));
        }

        foreach (CompanyYearSnapshot pair in snapshot.Reconciled)
        {
            _reconciled.Add((pair.CompanyId, pair.Year));
        }

        foreach (SurrenderInstructionSnapshot instruction in snapshot.Instructions ?? [])
        {
            _instructions[(instruction.CompanyId, instruction.Year)] = new SurrenderInstruction(
                _simulation.FindCompany(instruction.CompanyId),
                instruction.Year,
                instruction.Allowances,
                instruction.Offsets);
        }
    }

    /// <summary>The surrender a company has set for a year under manual surrender, or null.</summary>
    public SurrenderInstruction? InstructionFor(Company company, int year)
    {
        ArgumentNullException.ThrowIfNull(company);

        return _instructions.GetValueOrDefault((company.Id, year));
    }

    /// <summary>
    /// A human company's own surrender for a live year under manual surrender, replacing any it set
    /// before. Offsets are held to the usage limit and both volumes to what the company holds now;
    /// what it holds at the year end is checked again then, so a surrender it can no longer meet
    /// surrenders what is there.
    /// </summary>
    public SurrenderInstruction SetSurrender(Company company, int year, decimal allowances, decimal offsets)
    {
        ArgumentNullException.ThrowIfNull(company);
        RequireYear(year);

        if (!IsManual(company))
        {
            throw new InvalidOperationException(
                $"Surrender is automatic for '{company.Name}': the year end surrenders for it.");
        }

        if (_reconciled.Contains((company.Id, year)))
        {
            throw new InvalidOperationException(
                $"Company '{company.Name}' has already been reconciled for year {year}.");
        }

        SurrenderPlan plan = Plan(company, year);

        if (allowances < 0m || allowances > plan.AllowancesAvailable)
        {
            throw new ArgumentOutOfRangeException(
                nameof(allowances),
                allowances,
                $"Allowances to surrender must be between 0 and the {plan.AllowancesAvailable} the company holds for year {year}.");
        }

        if (offsets < 0m || offsets > plan.OffsetsAvailable)
        {
            throw new ArgumentOutOfRangeException(
                nameof(offsets),
                offsets,
                $"Offsets to surrender must be between 0 and {plan.OffsetsAvailable}: what the company holds, up to the usage limit of {new Standing(_simulation).OffsetLimit(company, year)}.");
        }

        SurrenderInstruction instruction = new(company, year, allowances, offsets);
        _instructions[(company.Id, year)] = instruction;

        return instruction;
    }

    /// <summary>
    /// What the year end would surrender for the company now, and the penalty that would follow,
    /// counting what is set aside for resting orders as the company's (it is handed back at the halt).
    /// Nothing moves.
    /// </summary>
    public SurrenderPlan Plan(Company company, int year)
    {
        ArgumentNullException.ThrowIfNull(company);
        RequireYear(year);

        return Plan(company, year, product => _simulation.Ledger.Held(company, product));
    }

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

        Parameters parameters = _simulation.Rules;
        SurrenderPlan plan = Plan(company, year, product => _simulation.Ledger.Available(company, product));
        decimal obligation = plan.Obligation;
        (decimal offsetsSurrendered, decimal allowancesSurrendered) = Surrender(company, year, plan);
        decimal shortfall = plan.Shortfall;

        decimal penaltyCash = plan.EstimatedPenalty;
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

        _byCompanyYear[(result.Company, result.Year)] = result;

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

    /// <summary>
    /// The one place the surrender is worked out: offsets first, never more than the usage limit,
    /// then allowances of the year's vintage and older, never more than the obligation. Automatic
    /// surrender covers as much as it can; a manual company covers what it set, as far as it holds it.
    /// </summary>
    private SurrenderPlan Plan(Company company, int year, Func<Product, decimal> balance)
    {
        Standing standing = new(_simulation);
        decimal obligation = standing.Emissions(company, year);
        decimal offsetsAvailable = Math.Min(balance(Product.Offset), standing.OffsetLimit(company, year));
        decimal allowancesAvailable = Standing.UsableVintages(year).Sum(balance);
        bool manual = IsManual(company);
        SurrenderInstruction? instruction = InstructionFor(company, year);

        decimal offsets = manual
            ? Math.Min(Math.Min(instruction?.Offsets ?? 0m, offsetsAvailable), Math.Max(0m, obligation))
            : offsetsAvailable;
        decimal wanted = manual ? instruction?.Allowances ?? 0m : allowancesAvailable;
        decimal allowances = Math.Min(wanted, Math.Min(allowancesAvailable, Math.Max(0m, obligation - offsets)));
        decimal shortfall = Math.Max(0m, obligation - offsets - allowances);

        return new SurrenderPlan(
            company,
            year,
            manual,
            instruction is not null,
            obligation,
            offsetsAvailable,
            allowancesAvailable,
            offsets,
            allowances,
            shortfall,
            shortfall * _simulation.Rules.PenaltyPerTonne);
    }

    /// <summary>A human company surrenders by hand when the exercise says so; a bot never does.</summary>
    private bool IsManual(Company company) =>
        !_simulation.Rules.AutomaticSurrender && company.Owner.Kind == PlayerKind.Human;

    /// <summary>Takes what the plan surrenders out of the ledger, this year's vintage first.</summary>
    private (decimal Offsets, decimal Allowances) Surrender(Company company, int year, SurrenderPlan plan)
    {
        if (plan.Offsets > 0m)
        {
            _simulation.Ledger.Consume(company, Product.Offset, plan.Offsets);
        }

        decimal needed = plan.Allowances;

        foreach (Product product in Standing.UsableVintages(year))
        {
            if (needed <= 0m)
            {
                break;
            }

            decimal take = Math.Min(_simulation.Ledger.Available(company, product), needed);

            if (take <= 0m)
            {
                continue;
            }

            _simulation.Ledger.Consume(company, product, take);
            needed -= take;
        }

        return (plan.Offsets, plan.Allowances);
    }

    private (decimal Banked, decimal Forfeited) Bank(
        Company company,
        int year,
        decimal obligation,
        Parameters parameters)
    {
        decimal held = Standing
            .UsableVintages(year)
            .Sum(product => _simulation.Ledger.Available(company, product));

        if (held <= 0m)
        {
            return (0m, 0m);
        }

        // The banking override keeps everything; so does a company with no emissions, which has no
        // obligation to measure a banking cap against.
        decimal cap = parameters.UnlimitedBanking || obligation <= 0m ? held : Round(obligation * parameters.BankingLimit);
        decimal banked = Math.Min(held, cap);
        decimal forfeited = held - banked;

        if (forfeited <= 0m)
        {
            return (banked, 0m);
        }

        decimal remaining = forfeited;

        foreach (Product product in Standing.UsableVintages(year).Reverse())
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
