using CarbonSim.Engine.Abatement;
using CarbonSim.Engine.Allocation;
using CarbonSim.Engine.Compliance;
using CarbonSim.Engine.Finance;
using CarbonSim.Engine.Market;
using CarbonSim.Engine.Randomness;
using CarbonSim.Engine.Reporting;
using CarbonSim.Engine.Snapshot;

namespace CarbonSim.Engine.Domain;

/// <summary>
/// The root of one training run: the trading systems being played, the players taking part,
/// and the clock state. Everything the engine later mutates (the clock, markets, allocation)
/// hangs off this object, so it is the single place a hosted service holds.
/// </summary>
public sealed class Simulation
{
    private readonly Dictionary<int, Unit> _unitsById;
    private readonly Dictionary<int, Company> _companiesById;
    private readonly Dictionary<int, Player> _playersById;
    private readonly Dictionary<string, Sector> _sectorsByName;

    private Simulation(
        Guid id,
        string name,
        ulong seed,
        SimulationRandom random,
        IReadOnlyList<TradingSystem> tradingSystems,
        IReadOnlyList<Player> players)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("A simulation needs a name.", nameof(name));
        }

        ArgumentNullException.ThrowIfNull(tradingSystems);
        ArgumentNullException.ThrowIfNull(players);
        ArgumentNullException.ThrowIfNull(random);

        if (tradingSystems.Count == 0)
        {
            throw new ArgumentException("A simulation needs at least one trading system.", nameof(tradingSystems));
        }

        Id = id;
        Name = name.Trim();
        Seed = seed;
        Random = random;
        TradingSystems = tradingSystems;
        Players = players;
        Companies = [.. tradingSystems.SelectMany(system => system.Companies)];
        Units = [.. tradingSystems.SelectMany(system => system.Units)];
        Sectors = [.. tradingSystems.SelectMany(system => system.Sectors).DistinctBy(sector => sector.Name)];

        _unitsById = Index(Units, unit => unit.Id, "unit");
        _companiesById = Index(Companies, company => company.Id, "company");
        _playersById = Index(Players, player => player.Id, "player");
        _sectorsByName = Sectors.ToDictionary(sector => sector.Name, StringComparer.Ordinal);

        AttachPlayersToCompanies();

        Allocation = new AllocationPlan(this);
        Abatements = new AbatementPortfolio(this);
        Compliance = new ComplianceRegister(this);
        Ledger = new AllowanceLedger(this);
        Government = new GovernmentAccount(Allocation);
        Cash = new CashLedger(this);
        Finance = new CompanyFinance(this);
        Journal = new MarketJournal(this);
    }

    /// <summary>
    /// Starts a simulation from its seed. The identifier is derived from the seed, so the same
    /// seed always produces the same simulation and every draw in it comes from the one stream.
    /// </summary>
    public static Simulation Create(
        string name,
        ulong seed,
        IReadOnlyList<TradingSystem> tradingSystems,
        IReadOnlyList<Player> players)
    {
        return new Simulation(
            SimulationRandom.IdFromSeed(seed),
            name,
            seed,
            new SimulationRandom(seed),
            tradingSystems,
            players);
    }

    public Guid Id { get; private set; }

    /// <summary>The seed this run was created from; reporting it makes a run reproducible.</summary>
    public ulong Seed { get; private set; }

    /// <summary>The single random stream of the simulation; every draw in the engine comes from here.</summary>
    public SimulationRandom Random { get; }

    /// <summary>Caps, free allocation and business-as-usual emissions for every year, fixed at creation.</summary>
    public AllocationPlan Allocation { get; }

    /// <summary>What each unit has committed to: implemented projects and temporary shutdowns.</summary>
    public AbatementPortfolio Abatements { get; }

    /// <summary>Year-end reconciliation: what each company owed, surrendered, banked and lost.</summary>
    public ComplianceRegister Compliance { get; }

    /// <summary>What each company holds, per vintage and for offsets.</summary>
    public AllowanceLedger Ledger { get; }

    /// <summary>The allowances the government keeps from the cap, and the money it raises.</summary>
    public GovernmentAccount Government { get; }

    /// <summary>Every movement of money, and the rules about what may be spent.</summary>
    public CashLedger Cash { get; }

    /// <summary>The company books: operating profit, abatement result, interest.</summary>
    public CompanyFinance Finance { get; }

    /// <summary>Every trade that has happened, for the reports and the price charts.</summary>
    public MarketJournal Journal { get; }

    public string Name { get; }

    public IReadOnlyList<TradingSystem> TradingSystems { get; }

    public IReadOnlyList<Player> Players { get; }

    public IReadOnlyList<Company> Companies { get; }

    public IReadOnlyList<Unit> Units { get; }

    public IReadOnlyList<Sector> Sectors { get; }

    /// <summary>Where the clock has the simulation; <see cref="SimulationState.Pending"/> until year 1 begins.</summary>
    public SimulationState State { get; internal set; } = SimulationState.Pending;

    /// <summary>Virtual year the simulation is in, or 0 before year 1 starts.</summary>
    public int CurrentYear { get; internal set; }

    public Unit FindUnit(int id) => Find(_unitsById, id, "unit");

    /// <summary>
    /// Puts back the identity and clock state a snapshot was taken with. A restored simulation
    /// is built from the same seed, so everything the seed derives is already right; the
    /// identifier and the year are set explicitly rather than assumed, so a snapshot stays the
    /// source of truth even if the way they are derived ever changes.
    /// </summary>
    internal void Restore(SimulationSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        Id = snapshot.Id;
        Seed = snapshot.Seed;
        State = snapshot.State;
        CurrentYear = snapshot.CurrentYear;
    }

    public Company FindCompany(int id) => Find(_companiesById, id, "company");

    public Player FindPlayer(int id) => Find(_playersById, id, "player");

    public Sector FindSector(string name)
    {
        ArgumentNullException.ThrowIfNull(name);

        return _sectorsByName.TryGetValue(name, out Sector? sector)
            ? sector
            : throw new KeyNotFoundException($"This simulation has no sector named '{name}'.");
    }

    private static Dictionary<TKey, T> Index<T, TKey>(IReadOnlyList<T> items, Func<T, TKey> key, string kind)
        where TKey : notnull
    {
        Dictionary<TKey, T> index = [];

        foreach (T item in items)
        {
            TKey itemKey = key(item);

            if (!index.TryAdd(itemKey, item))
            {
                throw new ArgumentException(
                    $"Two {kind}s share the same identifier {itemKey}: '{index[itemKey]}' and '{item}'.",
                    kind);
            }
        }

        return index;
    }

    private static T Find<T>(Dictionary<int, T> index, int id, string kind)
        where T : class
    {
        return index.TryGetValue(id, out T? item)
            ? item
            : throw new KeyNotFoundException($"This simulation has no {kind} with id {id}.");
    }

    private void AttachPlayersToCompanies()
    {
        foreach (Company company in Companies)
        {
            if (!ReferenceEquals(_playersById.GetValueOrDefault(company.Owner.Id), company.Owner))
            {
                throw new ArgumentException(
                    $"Company '{company.Name}' is owned by '{company.Owner.Name}', who is not a player in this simulation.",
                    nameof(Companies));
            }

            company.Owner.AddCompany(company);
        }
    }

    public override string ToString() => Name;
}
