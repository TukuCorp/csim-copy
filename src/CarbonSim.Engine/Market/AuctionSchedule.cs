using CarbonSim.Engine.Domain;

namespace CarbonSim.Engine.Market;

/// <summary>
/// The year's auctions, laid out up front from the cap: each year's auctionable volume is
/// divided into as many lots as there are auctions that year, and the leftover tonnes go to
/// the year's last auction so nothing is lost to rounding. When forward sales are wanted, the
/// last auction of a year also offers part of the next year's vintage.
/// </summary>
public sealed class AuctionSchedule
{
    private readonly Simulation _simulation;
    private readonly List<Auction> _order;
    private readonly Dictionary<(int Year, int Sequence), Auction> _bySection;

    private AuctionSchedule(Simulation simulation, List<Auction> auctions)
    {
        _simulation = simulation;
        _order = auctions;
        _bySection = auctions.ToDictionary(auction => (auction.Year, auction.Sequence));

        foreach (Auction auction in auctions)
        {
            auction.Schedule = this;
        }
    }

    public IReadOnlyList<Auction> Auctions => _order;

    /// <summary>
    /// Rebuilds a schedule from auctions a snapshot already holds. The auctions are taken as
    /// they are rather than rebuilt through <see cref="Build"/>: building offers lots out of the
    /// government's reserve, and a restore puts the reserve back exactly as it was.
    /// </summary>
    internal static AuctionSchedule Restore(Simulation simulation, IReadOnlyList<Auction> auctions)
    {
        ArgumentNullException.ThrowIfNull(simulation);
        ArgumentNullException.ThrowIfNull(auctions);

        return new AuctionSchedule(simulation, [.. auctions]);
    }

    /// <summary>
    /// Builds the whole run's auctions and offers their lots out of the government's issue.
    /// <paramref name="forwardVintageShare"/> is the share of a later year's volume sold early
    /// in the previous year's last auction (0 = no forward sales).
    /// </summary>
    public static AuctionSchedule Build(Simulation simulation, decimal forwardVintageShare = 0m)
    {
        ArgumentNullException.ThrowIfNull(simulation);

        if (forwardVintageShare is < 0m or > 1m)
        {
            throw new ArgumentOutOfRangeException(
                nameof(forwardVintageShare),
                forwardVintageShare,
                "The forward vintage share is a fraction of a year's auctionable volume.");
        }

        Parameters parameters = simulation.TradingSystems.Single().Parameters;
        simulation.Government.IssueAll();

        List<Auction> auctions = [];
        Dictionary<int, decimal> soldForward = [];

        foreach (int year in simulation.Allocation.Years)
        {
            decimal available = simulation.Allocation.AuctionableVolumeForYear(year) - soldForward.GetValueOrDefault(year);
            decimal forward = 0m;

            if (forwardVintageShare > 0m && year < simulation.Allocation.Years.Count)
            {
                forward = Round(forwardVintageShare * simulation.Allocation.AuctionableVolumeForYear(year + 1));
                soldForward[year + 1] = soldForward.GetValueOrDefault(year + 1) + forward;
            }

            decimal[] lots = SplitEvenly(available, parameters.AuctionsPerYear);

            for (int sequence = 1; sequence <= parameters.AuctionsPerYear; sequence++)
            {
                List<AuctionLot> onOffer = [new AuctionLot(year, lots[sequence - 1], false)];

                if (forward > 0m && sequence == parameters.AuctionsPerYear)
                {
                    onOffer.Add(new AuctionLot(year + 1, forward, true));
                }

                auctions.Add(new Auction(simulation, year, sequence, onOffer));
            }
        }

        return new AuctionSchedule(simulation, auctions);
    }

    /// <summary>
    /// Offers the government's reserve again once an auction has closed: the administrator's
    /// share of everything the government still holds, including the volume this auction could
    /// not sell, goes into the next auction that has not closed, oldest vintage first. With the
    /// share at zero this does nothing and the reserve stays where it was.
    /// </summary>
    internal void ReofferReserve(Auction closed)
    {
        ArgumentNullException.ThrowIfNull(closed);

        Parameters parameters = _simulation.TradingSystems.Single().Parameters;
        decimal percent = parameters.GovernmentReserveToAuctionPercent;

        // The reserve is reviewed once a year, when the last auction of the year closes: its
        // share is offered in the next year's auctions. Doing it at every close instead would
        // push each auction's unsold volume straight into the next one and leave the whole run
        // permanently over-supplied, with no price ever above the floor.
        if (percent <= 0m || closed.Sequence != parameters.AuctionsPerYear)
        {
            return;
        }

        int index = _order.IndexOf(closed);
        Auction? target = index >= 0 && index + 1 < _order.Count ? _order[index + 1] : null;

        if (target is null || target.IsCleared)
        {
            return;
        }

        decimal eligible = 0m;

        for (int vintage = 1; vintage <= target.Year; vintage++)
        {
            eligible += _simulation.Government.Held(vintage);
        }

        decimal toOffer = Round(percent * eligible);

        for (int vintage = 1; vintage <= target.Year && toOffer > 0m; vintage++)
        {
            decimal take = Math.Min(toOffer, _simulation.Government.Held(vintage));

            if (take > 0m)
            {
                target.AddLot(vintage, take);
                toOffer -= take;
            }
        }
    }

    public Auction ForSection(int year, int sequence)
    {
        return _bySection.TryGetValue((year, sequence), out Auction? auction)
            ? auction
            : throw new KeyNotFoundException($"There is no auction {sequence} in year {year}.");
    }

    /// <summary>Splits a volume into equal whole-tonne lots, the remainder going to the last lot.</summary>
    private static decimal[] SplitEvenly(decimal total, int parts)
    {
        decimal[] lots = new decimal[parts];
        decimal each = decimal.Floor(total / parts);

        for (int index = 0; index < parts; index++)
        {
            lots[index] = each;
        }

        lots[parts - 1] += total - (each * parts);

        return lots;
    }

    private static decimal Round(decimal value) => Math.Round(value, 0, MidpointRounding.AwayFromZero);
}
