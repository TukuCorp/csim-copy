using Microsoft.AspNetCore.SignalR;

namespace CarbonSim.Web.Simulations;

/// <summary>The group name every connection watching one run shares, so a run's messages reach
/// every player in it and nobody else.</summary>
public static class SimulationGroups
{
    public static string For(Guid simulationId) => $"simulation:{simulationId:N}";
}

/// <summary>The live channel to the browser. One SignalR group carries one simulation: a player
/// joins the group for the run they are watching and hears everything the driver publishes
/// for it. Sending and placing go through here too, so rules (a login owns a company, a sealed
/// bid stays sealed) are enforced server-side rather than hoped for in the browser.</summary>
public sealed class SimulationHub(SimulationRegistry runs) : Hub<ISimulationClient>
{
    private readonly SimulationRegistry _runs = runs ?? throw new ArgumentNullException(nameof(runs));

    /// <summary>Starts hearing everything the named run publishes.</summary>
    public async Task JoinSimulation(Guid simulationId)
    {
        RequireActor();

        if (!_runs.Contains(simulationId))
        {
            throw new HubException($"There is no simulation {simulationId}.");
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, SimulationGroups.For(simulationId)).ConfigureAwait(false);
    }

    /// <summary>Stops hearing the named run.</summary>
    public async Task LeaveSimulation(Guid simulationId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, SimulationGroups.For(simulationId)).ConfigureAwait(false);
    }

    /// <summary>Bids into the open auction of this year's section, sealed from other bidders.</summary>
    public Task PlaceAuctionBid(Guid simulationId, int unitId, int vintage, decimal price, decimal volume)
    {
        string actor = RequireActor();

        return Guard(() => _runs.BidAtAuctionAsync(simulationId, actor, unitId, vintage, price, volume));
    }

    /// <summary>Places an order onto the named order book.</summary>
    public Task PlaceOrder(
        Guid simulationId,
        int unitId,
        string product,
        string side,
        string kind,
        decimal volume,
        decimal? price = null,
        decimal? stopPrice = null,
        string fillPolicy = "AllowPartial")
    {
        string actor = RequireActor();

        return Guard(() => _runs.PlaceOrderAsync(
            simulationId, actor, unitId, product, side, kind, volume, price, stopPrice, fillPolicy));
    }

    /// <summary>Cancels an open order.</summary>
    public Task CancelOrder(Guid simulationId, int unitId, long orderId)
    {
        string actor = RequireActor();

        return Guard(() => _runs.CancelOrderAsync(simulationId, actor, unitId, orderId));
    }

    /// <summary>Sends an over-the-counter offer to a named unit.</summary>
    public Task SendOtcOffer(Guid simulationId, int sellerUnitId, int buyerUnitId, string product, decimal price, decimal volume)
    {
        string actor = RequireActor();

        return Guard(() => _runs.SendOtcOfferAsync(simulationId, actor, sellerUnitId, buyerUnitId, product, price, volume));
    }

    /// <summary>Answers an over-the-counter offer addressed to one of the caller's units.</summary>
    public Task AnswerOtcOffer(Guid simulationId, int buyerUnitId, long offerId, bool accept)
    {
        string actor = RequireActor();

        return Guard(() => _runs.AnswerOtcOfferAsync(simulationId, actor, buyerUnitId, offerId, accept));
    }

    /// <summary>Posts a message to everyone else in the run.</summary>
    public Task SendMessage(Guid simulationId, string text)
    {
        string actor = RequireActor();

        return Guard(() => _runs.PostMessageAsync(simulationId, actor, text));
    }

    /// <summary>The signed-in caller's identity, refusing a connection nobody signed in on.</summary>
    private string RequireActor()
    {
        return Context.UserIdentifier
            ?? throw new HubException("Sign in before joining or acting in a simulation.");
    }

    /// <summary>
    /// Turns a rule refusal into a HubException, which SignalR passes through to the client with
    /// its message intact rather than the generic "unexpected error" it sends for anything else.
    /// </summary>
    private static async Task Guard(Func<Task> action)
    {
        try
        {
            await action().ConfigureAwait(false);
        }
        catch (Exception exception) when (
            exception is UnauthorizedAccessException or ArgumentException or InvalidOperationException or KeyNotFoundException)
        {
            throw new HubException(exception.Message);
        }
    }
}
