using CarbonSim.Engine.Snapshot;

namespace CarbonSim.Engine.Domain;

/// <summary>A participating company: one owner and one or more emitting units.</summary>
public sealed class Company
{
    private readonly List<Unit> _units = [];

    public Company(int id, string name, Sector sector, Player owner, decimal capital, decimal overdraftLimit = 0m)
        : this(id, name, sector, owner, overdraftLimit)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("A company needs a name.", nameof(name));
        }

        if (capital <= 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(capital), capital, "Capital value must be greater than zero.");
        }

        if (overdraftLimit < 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(overdraftLimit), overdraftLimit, "A credit limit cannot be negative.");
        }

        ArgumentNullException.ThrowIfNull(sector);
        ArgumentNullException.ThrowIfNull(owner);

        Name = name.Trim();
        Capital = capital;
    }

    /// <summary>
    /// Builds a company from stored state. A run can end a year with a negative balance, so the
    /// opening-capital rule does not apply to a company being reloaded; the balances are left
    /// for <see cref="Restore"/> to put back exactly as they were.
    /// </summary>
    private Company(int id, string name, Sector sector, Player owner, decimal overdraftLimit)
    {
        Id = id;
        Name = name;
        Sector = sector;
        Owner = owner;
        OverdraftLimit = overdraftLimit;
    }

    public int Id { get; }

    public string Name { get; }

    public Sector Sector { get; }

    public Player Owner { get; }

    /// <summary>Cash the company owns; abatement, trades and penalties move it.</summary>
    public decimal Capital { get; private set; }

    /// <summary>How far below zero the balance may go, in simulation currency.</summary>
    public decimal OverdraftLimit { get; }

    /// <summary>Cash promised to orders and bids that have not settled yet.</summary>
    public decimal EscrowedCash { get; private set; }

    public IReadOnlyList<Unit> Units => _units;

    /// <summary>
    /// Moves cash by <paramref name="amount"/> (negative to spend). Only <see cref="Finance.CashLedger"/>
    /// calls this: it decides whether the movement is allowed and records why it happened.
    /// </summary>
    internal void AdjustCapital(decimal amount) => Capital += amount;

    internal void AdjustEscrow(decimal amount) => EscrowedCash += amount;

    internal void AddUnit(Unit unit)
    {
        ArgumentNullException.ThrowIfNull(unit);

        if (!ReferenceEquals(unit.Company, this))
        {
            throw new ArgumentException($"Unit '{unit.Name}' belongs to '{unit.Company.Name}', not to '{Name}'.", nameof(unit));
        }

        _units.Add(unit);
    }

    /// <summary>Rebuilds a company with the balances a snapshot found it holding.</summary>
    internal static Company Restore(CompanySnapshot snapshot, Sector sector, Player owner)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        return new Company(snapshot.Id, snapshot.Name, sector, owner, snapshot.OverdraftLimit)
        {
            Capital = snapshot.Capital,
            EscrowedCash = snapshot.EscrowedCash,
        };
    }

    public override string ToString() => Name;
}
