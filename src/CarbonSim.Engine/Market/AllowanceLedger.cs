using CarbonSim.Engine.Domain;
using CarbonSim.Engine.Snapshot;

namespace CarbonSim.Engine.Market;

/// <summary>
/// What each company holds, per product. Instruments are either available or escrowed against
/// a resting order or a trade offer, which is what stops the same allowances being promised
/// twice. Emission reductions, transfers and settlements all move balances here.
/// </summary>
public sealed class AllowanceLedger
{
    private readonly Simulation _simulation;
    private readonly Dictionary<(int CompanyId, Product Product), decimal> _available = [];
    private readonly Dictionary<(int CompanyId, Product Product), decimal> _escrowed = [];
    private readonly HashSet<int> _grantedYears = [];

    internal AllowanceLedger(Simulation simulation)
    {
        _simulation = simulation;
    }

    /// <summary>
    /// Every balance the ledger is holding, free and escrowed, plus the years whose free
    /// allocation has already been handed out. Escrow is part of the picture: it is what a
    /// restored order book and offer list are still counting on.
    /// </summary>
    internal AllowanceLedgerSnapshot ToSnapshot()
    {
        return new AllowanceLedgerSnapshot(
            Balances(_available),
            Balances(_escrowed),
            [.. _grantedYears.Order()]);
    }

    /// <summary>Puts back the balances and the granted years a snapshot was taken with.</summary>
    internal void Restore(AllowanceLedgerSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        _available.Clear();
        _escrowed.Clear();
        _grantedYears.Clear();

        foreach (LedgerBalanceSnapshot balance in snapshot.Available)
        {
            _available[(balance.CompanyId, balance.Product.ToProduct())] = balance.Volume;
        }

        foreach (LedgerBalanceSnapshot balance in snapshot.Escrowed)
        {
            _escrowed[(balance.CompanyId, balance.Product.ToProduct())] = balance.Volume;
        }

        foreach (int year in snapshot.GrantedYears)
        {
            _grantedYears.Add(year);
        }
    }

    public decimal Available(Company company, Product product) => Balance(_available, company, product);

    public decimal Escrowed(Company company, Product product) => Balance(_escrowed, company, product);

    public decimal Held(Company company, Product product) => Available(company, product) + Escrowed(company, product);

    /// <summary>
    /// Hands every company the free allocation its units are due for a year, as allowances of
    /// that vintage, less anything it still owes the regulator from the previous year's
    /// shortfall. Each year can only be granted once.
    /// </summary>
    public void GrantFreeAllocation(int year)
    {
        if (!_simulation.Allocation.Years.Contains(year))
        {
            throw new ArgumentOutOfRangeException(
                nameof(year),
                year,
                $"This simulation runs for {_simulation.Allocation.Years.Count} years.");
        }

        if (!_grantedYears.Add(year))
        {
            throw new InvalidOperationException($"The free allocation for year {year} has already been granted.");
        }

        foreach (Company company in _simulation.Companies)
        {
            decimal entitlement = _simulation.Allocation.FreeAllocationFor(company, year)
                - _simulation.Compliance.PenaltyAllowanceDebitFor(company, year);

            if (entitlement > 0m)
            {
                Grant(company, Product.Allowance(year), entitlement);
            }
        }
    }

    /// <summary>Puts instruments into a company's hands; used for allocation, offsets and fines.</summary>
    public void Grant(Company company, Product product, decimal volume)
    {
        ArgumentNullException.ThrowIfNull(company);
        RequireVolume(volume);

        Add(_available, company, product, volume);
    }

    /// <summary>Moves instruments between companies at once.</summary>
    public void Transfer(Company from, Company to, Product product, decimal volume)
    {
        ArgumentNullException.ThrowIfNull(from);
        ArgumentNullException.ThrowIfNull(to);
        RequireVolume(volume);

        Take(_available, from, product, volume);
        Add(_available, to, product, volume);
    }

    /// <summary>Takes instruments out of circulation for good: surrendered or forfeited.</summary>
    internal void Consume(Company company, Product product, decimal volume)
    {
        ArgumentNullException.ThrowIfNull(company);
        RequireVolume(volume);

        Take(_available, company, product, volume);
    }

    /// <summary>Sets instruments aside for a resting order or a trade offer.</summary>
    internal void Escrow(Company company, Product product, decimal volume)
    {
        ArgumentNullException.ThrowIfNull(company);
        RequireVolume(volume);

        Take(_available, company, product, volume);
        Add(_escrowed, company, product, volume);
    }

    /// <summary>Hands escrowed instruments back, for a cancelled order or a refused offer.</summary>
    internal void ReleaseEscrow(Company company, Product product, decimal volume)
    {
        ArgumentNullException.ThrowIfNull(company);
        RequireVolume(volume);

        Take(_escrowed, company, product, volume);
        Add(_available, company, product, volume);
    }

    /// <summary>Completes a trade: the seller's escrow becomes the buyer's holding.</summary>
    internal void SettleEscrow(Company seller, Company buyer, Product product, decimal volume)
    {
        ArgumentNullException.ThrowIfNull(seller);
        ArgumentNullException.ThrowIfNull(buyer);
        RequireVolume(volume);

        Take(_escrowed, seller, product, volume);
        Add(_available, buyer, product, volume);
    }

    private static decimal Balance(
        Dictionary<(int CompanyId, Product Product), decimal> balances,
        Company company,
        Product product)
    {
        ArgumentNullException.ThrowIfNull(company);

        return balances.TryGetValue((company.Id, product), out decimal volume) ? volume : 0m;
    }

    /// <summary>
    /// Reads a bucket out in company, kind and vintage order, so a snapshot of the same state
    /// always comes out the same way round and rows can be compared or diffed.
    /// </summary>
    private static IReadOnlyList<LedgerBalanceSnapshot> Balances(
        Dictionary<(int CompanyId, Product Product), decimal> balances)
    {
        return
        [
            .. balances
                .Select(entry => new LedgerBalanceSnapshot(
                    entry.Key.CompanyId,
                    new ProductSnapshot(entry.Key.Product.Kind, entry.Key.Product.Vintage),
                    entry.Value))
                .OrderBy(row => row.CompanyId)
                .ThenBy(row => row.Product.Kind)
                .ThenBy(row => row.Product.Vintage),
        ];
    }

    private static void Add(
        Dictionary<(int CompanyId, Product Product), decimal> balances,
        Company company,
        Product product,
        decimal volume)
    {
        balances[(company.Id, product)] = Balance(balances, company, product) + volume;
    }

    private static void Take(
        Dictionary<(int CompanyId, Product Product), decimal> balances,
        Company company,
        Product product,
        decimal volume)
    {
        decimal held = Balance(balances, company, product);

        if (held < volume)
        {
            throw new InvalidOperationException(
                $"Company '{company.Name}' has insufficient {product} instruments ({held} against {volume}).");
        }

        balances[(company.Id, product)] = held - volume;
    }

    private static void RequireVolume(decimal volume)
    {
        if (volume <= 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(volume), volume, "A volume must be greater than zero.");
        }
    }
}
