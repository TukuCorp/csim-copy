namespace CarbonSim.Engine.Market.Otc;

/// <summary>Where a trade offer stands.</summary>
public enum OtcOfferState
{
    /// <summary>Sent to the named buyer, instruments already set aside.</summary>
    Pending,

    Accepted,
    Rejected,

    /// <summary>Withdrawn by the seller before the buyer answered.</summary>
    Cancelled,
}
