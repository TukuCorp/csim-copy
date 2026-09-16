using CarbonSim.Engine.Domain;

namespace CarbonSim.Engine.Market;

/// <summary>One vintage's slice of an auction: what is on offer, and whether it is a forward sale.</summary>
public sealed record AuctionLot(int Vintage, decimal Volume, bool IsForward);

/// <summary>A sealed bid. Bids are ranked on price, and ties go to the bid that arrived first.</summary>
public sealed record AuctionBid(int Id, Unit Unit, int Vintage, decimal Price, decimal Volume);

/// <summary>How much of a bid was served, and what it cost at the clearing price.</summary>
public sealed record AuctionAward(AuctionBid Bid, decimal Volume, decimal Cost);

/// <summary>What one vintage of an auction did: who was served, at what price, for how much money.</summary>
public sealed class AuctionResult
{
    internal AuctionResult(
        int year,
        int sequence,
        int vintage,
        decimal offeredVolume,
        decimal? clearingPrice,
        IReadOnlyList<AuctionAward> awards,
        IReadOnlyList<AuctionBid> rejected)
    {
        Year = year;
        Sequence = sequence;
        Vintage = vintage;
        OfferedVolume = offeredVolume;
        ClearingPrice = clearingPrice;
        Awards = awards;
        Rejected = rejected;
    }

    public int Year { get; }

    /// <summary>Which auction of the year this was, 1-based.</summary>
    public int Sequence { get; }

    public int Vintage { get; }

    public decimal OfferedVolume { get; }

    /// <summary>
    /// The single price every served bidder pays, or null when nothing was sold. It is the
    /// marginal bid's price when demand met the offer, and the floor when demand fell short.
    /// </summary>
    public decimal? ClearingPrice { get; }

    public IReadOnlyList<AuctionAward> Awards { get; }

    /// <summary>Valid bids that were left unserved because the offer ran out.</summary>
    public IReadOnlyList<AuctionBid> Rejected { get; }

    public decimal VolumeSold => Awards.Sum(award => award.Volume);

    public decimal Revenue => Awards.Sum(award => award.Cost);

    public override string ToString() =>
        $"Year {Year} auction {Sequence} vintage {Vintage}: {VolumeSold} of {OfferedVolume} at {ClearingPrice}";
}
