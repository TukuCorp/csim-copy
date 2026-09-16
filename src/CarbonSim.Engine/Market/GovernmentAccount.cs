using CarbonSim.Engine.Allocation;

namespace CarbonSim.Engine.Market;

/// <summary>
/// The government's side of the ledger: the allowances it keeps back from each year's cap
/// instead of handing them out free. It issues that volume up front, an auction schedule
/// offers lots out of it, and whatever is not sold — either because it was never scheduled or
/// because bidders did not turn up — stays here as the reserve. Auctions paid for with real
/// money show up as <see cref="Revenue"/>.
/// </summary>
public sealed class GovernmentAccount
{
    private readonly Dictionary<int, decimal> _reserve = [];
    private readonly AllocationPlan _plan;
    private bool _issued;

    internal GovernmentAccount(AllocationPlan plan)
    {
        _plan = plan;
    }

    /// <summary>Money the government has raised at auction.</summary>
    public decimal Revenue { get; private set; }

    /// <summary>Allowances of a vintage still held back, not counting any already offered in an auction.</summary>
    public decimal Held(int vintage) => _reserve.GetValueOrDefault(vintage);

    /// <summary>Total allowances of a vintage the government was given by the cap.</summary>
    public decimal Issued(int vintage) =>
        vintage >= 1 && vintage <= _plan.Years.Count ? _plan.AuctionableVolumeForYear(vintage) : 0m;

    /// <summary>Hands the government every year's auctionable volume at once; done once per run.</summary>
    internal void IssueAll()
    {
        if (_issued)
        {
            throw new InvalidOperationException("The government has already been issued its allowances for this run.");
        }

        _issued = true;

        foreach (int year in _plan.Years)
        {
            _reserve[year] = _reserve.GetValueOrDefault(year) + _plan.AuctionableVolumeForYear(year);
        }
    }

    /// <summary>Moves volume out of the reserve into an auction's lots.</summary>
    internal void Offer(int vintage, decimal volume)
    {
        decimal held = Held(vintage);

        if (held < volume)
        {
            throw new InvalidOperationException(
                $"The government holds {held} allowances of vintage {vintage}, which is less than the {volume} offered.");
        }

        _reserve[vintage] = held - volume;
    }

    /// <summary>Puts volume that an auction could not sell back into the reserve.</summary>
    internal void Return(int vintage, decimal volume)
    {
        if (volume <= 0m)
        {
            return;
        }

        _reserve[vintage] = Held(vintage) + volume;
    }

    /// <summary>Records auction revenue.</summary>
    internal void Collect(decimal revenue)
    {
        if (revenue < 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(revenue), revenue, "Auction revenue cannot be negative.");
        }

        Revenue += revenue;
    }
}
