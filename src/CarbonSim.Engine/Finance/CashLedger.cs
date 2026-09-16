using CarbonSim.Engine.Domain;

namespace CarbonSim.Engine.Finance;

/// <summary>
/// Every movement of money in the simulation, in one place. Spending is refused when it would
/// take a company past its credit limit; money set aside for an order or a bid is marked
/// unavailable until the order is cancelled or the trade settles, which is what makes
/// "cannot spend what you have already promised" true rather than merely documented.
/// </summary>
public sealed class CashLedger
{
    private readonly Simulation _simulation;
    private readonly List<CashMovement> _movements = [];

    internal CashLedger(Simulation simulation)
    {
        _simulation = simulation;
    }

    public IReadOnlyList<CashMovement> Movements => _movements;

    /// <summary>Money the company could spend right now.</summary>
    public decimal Available(Company company)
    {
        ArgumentNullException.ThrowIfNull(company);

        return company.Capital - company.EscrowedCash;
    }

    public decimal Escrowed(Company company)
    {
        ArgumentNullException.ThrowIfNull(company);

        return company.EscrowedCash;
    }

    /// <summary>True when the company could pay this without breaking its credit limit.</summary>
    public bool CanAfford(Company company, decimal amount)
    {
        ArgumentNullException.ThrowIfNull(company);

        return Available(company) - amount >= -company.OverdraftLimit;
    }

    /// <summary>Money in, from any source.</summary>
    public void Deposit(Company company, decimal amount, CashCategory category, string description)
    {
        RequireMovement(company, amount, description);

        company.AdjustCapital(amount);
        Record(company, category, amount, description);
    }

    /// <summary>Money out, refused if it would take the company past its credit limit.</summary>
    public void Withdraw(Company company, decimal amount, CashCategory category, string description)
    {
        RequireMovement(company, amount, description);

        if (!CanAfford(company, amount))
        {
            throw new InvalidOperationException(
                $"Company '{company.Name}' cannot pay {amount}: it would go past its credit limit "
                + $"({Available(company)} available against a limit of {company.OverdraftLimit}).");
        }

        company.AdjustCapital(-amount);
        Record(company, category, -amount, description);
    }

    /// <summary>
    /// Money out that nobody may refuse: a penalty or a fine. The regulator does not extend
    /// credit, so this is the one movement that can take a company past its limit.
    /// </summary>
    public void Charge(Company company, decimal amount, CashCategory category, string description)
    {
        RequireMovement(company, amount, description);

        company.AdjustCapital(-amount);
        Record(company, category, -amount, description);
    }

    /// <summary>Sets money aside for an order or a bid, so it cannot be spent twice.</summary>
    public void Escrow(Company company, decimal amount)
    {
        RequireMovement(company, amount, "escrow");

        if (!CanAfford(company, amount))
        {
            throw new InvalidOperationException(
                $"Company '{company.Name}' cannot set aside {amount}: it would go past its credit limit "
                + $"({Available(company)} available against a limit of {company.OverdraftLimit}).");
        }

        company.AdjustEscrow(amount);
    }

    /// <summary>Hands set-aside money back when an order is cancelled or a bid loses.</summary>
    public void ReleaseEscrow(Company company, decimal amount)
    {
        ArgumentNullException.ThrowIfNull(company);

        if (amount <= 0m)
        {
            return;
        }

        company.AdjustEscrow(-amount);
    }

    /// <summary>Completes a purchase: the buyer's set-aside money becomes the seller's.</summary>
    public void SettleEscrow(Company buyer, Company seller, decimal amount, CashCategory category, string description)
    {
        ArgumentNullException.ThrowIfNull(buyer);
        ArgumentNullException.ThrowIfNull(seller);

        if (amount <= 0m)
        {
            return;
        }

        if (buyer.EscrowedCash < amount)
        {
            throw new InvalidOperationException(
                $"Company '{buyer.Name}' has only {buyer.EscrowedCash} set aside, which does not cover {amount}.");
        }

        buyer.AdjustEscrow(-amount);
        buyer.AdjustCapital(-amount);
        seller.AdjustCapital(amount);

        Record(buyer, category, -amount, description);
        Record(seller, CashCategory.InstrumentSale, amount, description);
    }

    /// <summary>
    /// Completes a purchase where the money leaves the companies: auction revenue goes to the
    /// government, which keeps its own account rather than a company's books.
    /// </summary>
    public void SettleEscrow(Company buyer, decimal amount, CashCategory category, string description)
    {
        ArgumentNullException.ThrowIfNull(buyer);

        if (amount <= 0m)
        {
            return;
        }

        if (buyer.EscrowedCash < amount)
        {
            throw new InvalidOperationException(
                $"Company '{buyer.Name}' has only {buyer.EscrowedCash} set aside, which does not cover {amount}.");
        }

        buyer.AdjustEscrow(-amount);
        buyer.AdjustCapital(-amount);

        Record(buyer, category, -amount, description);
    }

    /// <summary>What a company's money did in one year: money in less money out.</summary>
    public decimal MovementsFor(Company company, int year, params CashCategory[] categories)
    {
        ArgumentNullException.ThrowIfNull(company);

        return _movements
            .Where(movement => ReferenceEquals(movement.Company, company) && movement.Year == year)
            .Where(movement => categories.Length == 0 || categories.Contains(movement.Category))
            .Sum(movement => movement.Amount);
    }

    private void Record(Company company, CashCategory category, decimal amount, string description)
    {
        _movements.Add(new CashMovement(_movements.Count + 1, _simulation.CurrentYear, company, category, amount, description));
    }

    private static void RequireMovement(Company company, decimal amount, string description)
    {
        ArgumentNullException.ThrowIfNull(company);

        if (amount <= 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(amount), amount, "A money movement must be greater than zero.");
        }

        if (string.IsNullOrWhiteSpace(description))
        {
            throw new ArgumentException("A money movement needs a description.", nameof(description));
        }
    }
}
