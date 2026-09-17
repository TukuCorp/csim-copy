using CarbonSim.Engine.Domain;
using CarbonSim.Engine.Finance;
using CarbonSim.Engine.Reporting;
using CarbonSim.Engine.Snapshot;

namespace CarbonSim.Engine.Market;

/// <summary>
/// One sealed-bid, single-round, uniform-price auction. Players may bid as often as they like,
/// from as many units as they have, and cannot see anyone else's bids. When the window closes
/// the bids are ranked on price and then on arrival, the offer is filled down that list, and
/// every served bidder pays the same price: the marginal bid when demand met the offer, or the
/// floor when demand fell short of it. Each vintage in the auction clears on its own, so a
/// forward lot for next year's vintage has its own price.
/// </summary>
public sealed class Auction
{
    private readonly Simulation _simulation;
    private readonly Parameters _parameters;
    private readonly List<AuctionBid> _bids = [];
    private readonly List<AuctionResult> _results = [];

    internal Auction(Simulation simulation, int year, int sequence, IReadOnlyList<AuctionLot> lots)
        : this(simulation, year, sequence, lots, offersLots: true)
    {
    }

    /// <summary>
    /// Builds an auction. <paramref name="offersLots"/> is false only when a snapshot is being
    /// restored: a restored auction must not take its lots out of the government's reserve a
    /// second time, because the reserve is put back as it was.
    /// </summary>
    private Auction(Simulation simulation, int year, int sequence, IReadOnlyList<AuctionLot> lots, bool offersLots)
    {
        ArgumentNullException.ThrowIfNull(simulation);
        ArgumentNullException.ThrowIfNull(lots);

        if (lots.Count == 0)
        {
            throw new ArgumentException("An auction needs at least one lot on offer.", nameof(lots));
        }

        _simulation = simulation;
        _parameters = simulation.TradingSystems.Single().Parameters;
        Year = year;
        Sequence = sequence;
        Lots = lots;

        if (offersLots)
        {
            foreach (AuctionLot lot in lots)
            {
                simulation.Government.Offer(lot.Vintage, lot.Volume);
            }
        }

        UnsoldVolume = OfferedVolume;
    }

    public int Year { get; }

    /// <summary>Which auction of the year this is, 1-based.</summary>
    public int Sequence { get; }

    public IReadOnlyList<AuctionLot> Lots { get; }

    /// <summary>The bids placed so far, in the order they arrived.</summary>
    public IReadOnlyList<AuctionBid> Bids => _bids;

    /// <summary>
    /// What each vintage did when the auction cleared, empty while it is open. The uniform
    /// price is worked out once, at clearing, and nothing else in the run keeps it — the journal
    /// records the trades it produced but not the offer they came out of — so it is kept here
    /// for the administrator's screens and for a snapshot.
    /// </summary>
    internal IReadOnlyList<AuctionResult> Results => _results;

    public bool IsCleared { get; private set; }

    /// <summary>Total volume on offer across every lot, as the auction screen shows it.</summary>
    public decimal OfferedVolume => Lots.Sum(lot => lot.Volume);

    /// <summary>
    /// Volume the auction is still holding: everything on offer while it is open, and nothing
    /// once it has cleared, because unsold lots go back to the government's reserve.
    /// </summary>
    public decimal UnsoldVolume { get; private set; }

    /// <summary>Places a sealed bid for a vintage this auction offers.</summary>
    public void PlaceBid(Unit unit, int vintage, decimal price, decimal volume)
    {
        ArgumentNullException.ThrowIfNull(unit);

        if (IsCleared)
        {
            throw new InvalidOperationException($"Auction {Sequence} of year {Year} is already closed.");
        }

        if (!Lots.Any(lot => lot.Vintage == vintage))
        {
            throw new ArgumentException(
                $"Vintage {vintage} is not on offer in auction {Sequence} of year {Year}.",
                nameof(vintage));
        }

        if (price < _parameters.AuctionFloorPrice)
        {
            throw new ArgumentOutOfRangeException(nameof(price), price, $"A bid cannot be below the price floor of {_parameters.AuctionFloorPrice}.");
        }

        if (price > _parameters.AuctionCeilingPrice)
        {
            throw new ArgumentOutOfRangeException(nameof(price), price, $"A bid cannot be above the price ceiling of {_parameters.AuctionCeilingPrice}.");
        }

        if (volume <= 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(volume), volume, "A bid volume must be greater than zero.");
        }

        if (unit.Company.Units.All(candidate => !ReferenceEquals(candidate, unit)))
        {
            throw new ArgumentException($"Unit '{unit.Name}' is not part of this simulation.", nameof(unit));
        }

        // A bid is a promise to pay at a price nobody knows yet, so the money is set aside now.
        _simulation.Cash.Escrow(unit.Company, price * volume);

        _bids.Add(new AuctionBid(_bids.Count + 1, unit, vintage, price, volume));
    }

    /// <summary>
    /// Clears every vintage in the auction, settles the served bids against the ledger and the
    /// government account, and returns what happened. Unsold volume goes back to the reserve.
    /// </summary>
    public IReadOnlyList<AuctionResult> Clear()
    {
        if (IsCleared)
        {
            throw new InvalidOperationException($"Auction {Sequence} of year {Year} has already been cleared.");
        }

        IsCleared = true;

        List<AuctionResult> results = [];

        foreach (AuctionLot lot in Lots)
        {
            AuctionResult result = ClearLot(lot);
            results.Add(result);
            _results.Add(result);
        }

        return results;
    }

    private AuctionResult ClearLot(AuctionLot lot)
    {
        AuctionBid[] ranked = [.. _bids
            .Where(bid => bid.Vintage == lot.Vintage)
            .OrderByDescending(bid => bid.Price)
            .ThenBy(bid => bid.Id)];

        List<AuctionAward> awards = [];
        List<AuctionBid> rejected = [];
        decimal remaining = lot.Volume;
        decimal marginalPrice = _parameters.AuctionFloorPrice;

        foreach (AuctionBid bid in ranked)
        {
            if (remaining <= 0m)
            {
                rejected.Add(bid);
                continue;
            }

            decimal filled = Math.Min(bid.Volume, remaining);
            remaining -= filled;
            marginalPrice = bid.Price;
            awards.Add(new AuctionAward(bid, filled, 0m));
        }

        decimal? clearingPrice = awards.Count == 0
            ? null
            : remaining > 0m ? _parameters.AuctionFloorPrice : marginalPrice;

        if (clearingPrice is { } price)
        {
            awards = [.. awards.Select(award => new AuctionAward(award.Bid, award.Volume, award.Volume * price))];
            Settle(lot.Vintage, awards);
        }

        if (remaining > 0m)
        {
            _simulation.Government.Return(lot.Vintage, remaining);
        }

        UnsoldVolume -= lot.Volume;

        // Anything that did not win its money back, in full or in part.
        foreach (AuctionBid bid in rejected)
        {
            _simulation.Cash.ReleaseEscrow(bid.Unit.Company, bid.Price * bid.Volume);
        }

        foreach (AuctionAward award in awards)
        {
            decimal setAside = award.Bid.Price * award.Bid.Volume;
            _simulation.Cash.ReleaseEscrow(award.Bid.Unit.Company, setAside - award.Cost);
        }

        return new AuctionResult(Year, Sequence, lot.Vintage, lot.Volume, clearingPrice, awards, rejected);
    }

    private void Settle(int vintage, IReadOnlyList<AuctionAward> awards)
    {
        Product product = Product.Allowance(vintage);

        foreach (AuctionAward award in awards)
        {
            Company buyer = award.Bid.Unit.Company;

            // The bid set aside price x volume; the award costs the clearing price, so the
            // difference goes back whether the clearing price came out lower or the bid lost.
            _simulation.Cash.SettleEscrow(
                buyer,
                award.Cost,
                CashCategory.AuctionPurchase,
                $"Auction {Sequence} year {Year}, vintage {vintage}");

            _simulation.Ledger.Grant(buyer, product, award.Volume);
            _simulation.Journal.Record(
                TradeChannel.Auction,
                product,
                price: award.Cost / award.Volume,
                award.Volume,
                buyer,
                seller: null);
        }

        _simulation.Government.Collect(awards.Sum(award => award.Cost));
    }

    public override string ToString() => $"Year {Year} auction {Sequence}";

    /// <summary>
    /// Rebuilds an auction from a snapshot: its lots without offering them again, its bids, how
    /// far clearing got, and what clearing produced. The bids are rebuilt first and the results
    /// are resolved against them, so a restored award still points at the bid that won.
    /// </summary>
    internal static Auction Restore(Simulation simulation, AuctionSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(simulation);
        ArgumentNullException.ThrowIfNull(snapshot);

        Auction auction = new(
            simulation,
            snapshot.Year,
            snapshot.Sequence,
            [.. snapshot.Lots.Select(lot => new AuctionLot(lot.Vintage, lot.Volume, lot.IsForward))],
            offersLots: false);

        foreach (AuctionBidSnapshot bid in snapshot.Bids)
        {
            auction._bids.Add(new AuctionBid(
                bid.Id,
                simulation.FindUnit(bid.UnitId),
                bid.Vintage,
                bid.Price,
                bid.Volume));
        }

        auction.IsCleared = snapshot.IsCleared;
        auction.UnsoldVolume = snapshot.UnsoldVolume;

        foreach (AuctionResultSnapshot result in snapshot.Results)
        {
            auction._results.Add(new AuctionResult(
                auction.Year,
                auction.Sequence,
                result.Vintage,
                result.OfferedVolume,
                result.ClearingPrice,
                [.. result.Awards.Select(award => new AuctionAward(auction.Bid(award.BidId), award.Volume, award.Cost))],
                [.. result.RejectedBidIds.Select(auction.Bid)]));
        }

        return auction;
    }

    private AuctionBid Bid(int id)
    {
        return _bids.FirstOrDefault(bid => bid.Id == id)
            ?? throw new KeyNotFoundException($"Auction {Sequence} of year {Year} has no bid {id}.");
    }
}
