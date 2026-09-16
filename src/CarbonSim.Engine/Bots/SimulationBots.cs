using CarbonSim.Engine.Clock;
using CarbonSim.Engine.Domain;
using CarbonSim.Engine.Market;
using CarbonSim.Engine.Market.Exchange;
using CarbonSim.Engine.Market.Otc;

namespace CarbonSim.Engine.Bots;

/// <summary>
/// The bots of one simulation. An AI company is run entirely by its bot; a human company's
/// units are only run automatically where the trainer has switched AutoTrade on for that unit,
/// which is how the original's "trade assistance" works.
/// </summary>
public sealed class SimulationBots
{
    private readonly List<ComplianceBot> _bots;

    private SimulationBots(List<ComplianceBot> bots)
    {
        _bots = bots;
    }

    public IReadOnlyList<ComplianceBot> Bots => _bots;

    /// <summary>Builds one bot per company that has anything to automate.</summary>
    public static SimulationBots Create(Simulation simulation, BotSettings? settings = null)
    {
        ArgumentNullException.ThrowIfNull(simulation);

        settings ??= BotSettings.For(BotDifficulty.Normal);
        decimal referencePrice = simulation.TradingSystems.Single().Parameters.AuctionFloorPrice;
        List<ComplianceBot> bots = [];

        foreach (Company company in simulation.Companies)
        {
            List<Unit> units =
            [
                .. company.Units.Where(unit => company.Owner.Kind == PlayerKind.Ai || unit.AutoTrade),
            ];

            if (units.Count > 0)
            {
                bots.Add(new ComplianceBot(company, units, settings, simulation.Random, referencePrice));
            }
        }

        return new SimulationBots(bots);
    }

    /// <summary>Gives every bot its turn for the time the clock is showing.</summary>
    public IReadOnlyList<BotAction> Act(Simulation simulation, SimulationClock clock, BotMarkets markets)
    {
        ArgumentNullException.ThrowIfNull(simulation);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(markets);

        List<BotAction> actions = [];

        foreach (ComplianceBot bot in _bots)
        {
            actions.AddRange(bot.Act(simulation, clock, markets));
        }

        return actions;
    }

    /// <summary>The markets the bots in this simulation may use.</summary>
    public static BotMarkets Markets(Simulation simulation, Exchange? exchange = null, OtcMarket? otc = null, AuctionSchedule? schedule = null)
    {
        ArgumentNullException.ThrowIfNull(simulation);

        return new BotMarkets(
            exchange ?? new Exchange(simulation),
            otc ?? new OtcMarket(simulation),
            schedule ?? AuctionSchedule.Build(simulation));
    }
}
