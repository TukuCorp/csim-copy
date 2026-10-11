using CarbonSim.Engine.Market;
using CarbonSim.Engine.Market.Exchange;

namespace CarbonSim.Engine.Run;

/// <summary>
/// Why an order was refused, and in whose words: the window that was shut, so a screen can say it in
/// the reader's language, and the market's own message for anything else.
/// </summary>
public sealed record OrderRefusal(TradeRefusal? Window, string Message)
{
    /// <summary>A refusal by a shut window, with the engine's own message as a fallback.</summary>
    public static OrderRefusal Of(TradeRefusal window, string message) => new(window, message);

    /// <summary>A refusal the market explained itself, with no window to name.</summary>
    public static OrderRefusal Of(string message) => new(null, message);
}

/// <summary>
/// What became of one exchange order, read from the run's own result rather than guessed from the
/// next snapshot: the id, how much filled and at what average price, how much is left working on
/// the book, and the refusal when the market would not take it at all.
/// </summary>
public sealed record OrderOutcome(
    long OrderId,
    decimal Filled,
    decimal AveragePrice,
    decimal Resting,
    OrderRefusal? Refusal)
{
    /// <summary>The outcome of an order the book took, read off the run's result.</summary>
    public static OrderOutcome From(RunResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        Order order = result.Order ?? throw new InvalidOperationException("The exchange did not report the order it placed.");
        decimal volume = result.Fills.Sum(fill => fill.Volume);
        decimal averagePrice = volume > 0m ? result.Fills.Sum(fill => fill.Consideration) / volume : 0m;
        decimal resting = order.Status is OrderStatus.Open or OrderStatus.PartiallyFilled ? order.Remaining : 0m;

        return new OrderOutcome(order.Id, order.FilledVolume, averagePrice, resting, Refusal: null);
    }

    /// <summary>An order the market refused before the book saw it.</summary>
    public static OrderOutcome Refused(OrderRefusal refusal)
    {
        ArgumentNullException.ThrowIfNull(refusal);

        return new OrderOutcome(0, 0m, 0m, 0m, refusal);
    }
}

