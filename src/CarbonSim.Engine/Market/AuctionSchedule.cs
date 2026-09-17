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
    private readonly Dictionary<(int Year, int Sequence), Auction> _bySection;

    private AuctionSchedule(IReadOnlyList<Auction> auctions)
    {
        Auctions = auctions;
        _bySection = auctions.ToDictionary(auction => (auction.Year, auction.Sequence));
    }

    public IReadOnlyList<Auction> Auctions { get; }

    /// <summary>
    /// Rebuilds a schedule from auctions a snapshot already holds. The auctions are taken as
    /// they are rather than rebuilt through <see cref="Build"/>: building offers lots out of the
    /// government's reserve, and a restore puts the reserve back exactly as it was.
    /// </summary>
    internal static AuctionSchedule Restore(IReadOnlyList<Auction> auctions)
    {
        ArgumentNullException.ThrowIfNull(auctions);

        return new AuctionSchedule(auctions);
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

        return new AuctionSchedule(auctions);
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
