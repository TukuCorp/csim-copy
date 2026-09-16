using CarbonSim.Engine.Market;
using CarbonSim.Engine.Market.Exchange;
using CarbonSim.Engine.Market.Otc;

namespace CarbonSim.Engine.Bots;

/// <summary>What a bot decided to do, for the logs and for the tests.</summary>
public enum BotTrigger
{
    /// <summary>Early in the year: implement abatement and put a bid into the auction.</summary>
    Abatement,

    /// <summary>Each time an auction opens: bid for part of what it still needs.</summary>
    Auction,

    /// <summary>Around the middle of the year: work the secondary markets.</summary>
    Trade,
}

/// <summary>One thing a bot did, or tried to do and could not.</summary>
public sealed record BotAction(string Company, int Year, BotTrigger Trigger, string Detail)
{
    public override string ToString() => $"Year {Year} {Company} {Trigger}: {Detail}";
}

/// <summary>The markets a bot may use.</summary>
public sealed record BotMarkets(Exchange Exchange, OtcMarket Otc, AuctionSchedule Schedule);
