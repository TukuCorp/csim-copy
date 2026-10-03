namespace CarbonSim.Web.Player;

/// <summary>
/// What a screen asks of the host: a snapshot of the run, and the owner-checked actions a player
/// may take. The implementation reads the engine behind the run's gate and sends every action
/// through the same registry the SignalR hub uses, so a component never mutates the engine itself.
/// </summary>
public interface IPlayerSession
{
    /// <summary>The runs on offer, newest first, for the waiting screen.</summary>
    Task<IReadOnlyList<RunSummary>> RunsAsync(CancellationToken cancellationToken = default);

    /// <summary>The signed-in account's identity, or null when nobody is signed in.</summary>
    Task<string?> CurrentActorAsync(CancellationToken cancellationToken = default);

    /// <summary>The view for the signed-in player's company, or null when there is nothing to show.</summary>
    Task<PlayerView?> ForPlayerAsync(Guid simulationId, CancellationToken cancellationToken = default);

    Task PlaceBidAsync(Guid simulationId, int unitId, int vintage, decimal price, decimal volume, CancellationToken cancellationToken = default);

    Task PlaceOrderAsync(
        Guid simulationId,
        int unitId,
        string product,
        string side,
        string kind,
        decimal volume,
        decimal? price,
        decimal? stopPrice,
        string fillPolicy,
        CancellationToken cancellationToken = default);

    Task CancelOrderAsync(Guid simulationId, int unitId, long orderId, CancellationToken cancellationToken = default);

    Task ImplementAbatementAsync(Guid simulationId, int unitId, int optionIndex, CancellationToken cancellationToken = default);

    Task SendOtcOfferAsync(Guid simulationId, int sellerUnitId, int buyerUnitId, string product, decimal price, decimal volume, CancellationToken cancellationToken = default);

    Task AnswerOtcOfferAsync(Guid simulationId, int buyerUnitId, long offerId, bool accept, CancellationToken cancellationToken = default);

    Task PostMessageAsync(Guid simulationId, string text, CancellationToken cancellationToken = default);
}
