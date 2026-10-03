namespace CarbonSim.Web.Simulations;

/// <summary>Every message the hub speaks to the browser, typed so a renamed method or a
/// mistyped group name fails the build instead of dropping a message.</summary>
public interface ISimulationClient
{
    Task AuctionOpened(int year, int sequence, decimal offeredVolume, CancellationToken cancellationToken = default);

    Task AuctionClosing(int year, int sequence, TimeSpan timeLeft, CancellationToken cancellationToken = default);

    Task AuctionCleared(
        int year,
        int sequence,
        decimal? clearingPrice,
        decimal volumeSold,
        decimal offeredVolume,
        CancellationToken cancellationToken = default);

    Task MarketStateChanged(
        string state,
        int currentYear,
        int currentAuction,
        bool auctionOpen,
        CancellationToken cancellationToken = default);

    Task OrderBookChanged(
        string product,
        decimal? bestBid,
        decimal? bestOffer,
        decimal lastPrice,
        CancellationToken cancellationToken = default);

    Task TradeExecuted(
        string channel,
        string product,
        decimal price,
        decimal volume,
        string buyer,
        string? seller,
        CancellationToken cancellationToken = default);

    Task OtcOfferReceived(long offerId, string seller, string buyer, string product, decimal price, decimal volume, CancellationToken cancellationToken = default);

    Task OtcOfferResolved(long offerId, string resolution, CancellationToken cancellationToken = default);

    Task MessageReceived(string from, string text, int year, CancellationToken cancellationToken = default);

    Task SimulationStateChanged(string state, int currentYear, CancellationToken cancellationToken = default);

    Task ParametersChanged(CancellationToken cancellationToken = default);

    /// <summary>The administrator switched player messaging on or off for the run.</summary>
    Task MessagingStateChanged(bool enabled, CancellationToken cancellationToken = default);
}
