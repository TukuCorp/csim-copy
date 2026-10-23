namespace CarbonSim.Engine.Market.Exchange;

public enum OrderSide
{
    Buy,
    Sell,
}

/// <summary>
/// How an order works the book: a limit order names its price, a market order takes whatever
/// is there, and a stop order waits for the price to cross it before going to market.
/// </summary>
public enum OrderKind
{
    Limit,
    Market,
    StopLoss,
}

/// <summary>
/// Whether an order may be partly served ("Partial fills accepted" in the original) or has to
/// be served in full at once, in which case it is killed when the book cannot cover it; and
/// whether what is not served at once may rest ("Immediate or cancel" says it may not).
/// </summary>
public enum FillPolicy
{
    AllowPartial,
    FillOrKill,

    /// <summary>Partial fills accepted, and whatever does not cross at once is cancelled.</summary>
    ImmediateOrCancel,
}

public enum OrderStatus
{
    Open,
    PartiallyFilled,
    Filled,
    Cancelled,
}
