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

    /// <summary>Rebuilds the fleet from bots a snapshot already holds, in the same order.</summary>
    internal static SimulationBots Restore(IReadOnlyList<ComplianceBot> bots)
    {
        ArgumentNullException.ThrowIfNull(bots);

        return new SimulationBots([.. bots]);
    }

    /// <summary>Builds one bot per company that has anything to automate.</summary>
    public static SimulationBots Create(Simulation simulation, BotSettings? settings = null)
    {
        ArgumentNullException.ThrowIfNull(simulation);

        settings ??= BotSettings.For(simulation.Rules.AiDifficulty);
        decimal referencePrice = simulation.Rules.AuctionFloorPrice;
        List<ComplianceBot> bots = [];

        foreach (Company company in simulation.Companies)
        {
            List<Unit> units = Automated(company);

            if (units.Count > 0)
            {
                bots.Add(new ComplianceBot(company, units, settings, simulation.Random, referencePrice));
            }
        }

        return new SimulationBots(bots);
    }

    /// <summary>
    /// The fleet after AutoTrade switches have changed which units are automated. A company whose
    /// automated units are unchanged keeps its bot - progress, trigger times and all - so only a
    /// company that was switched draws from the seeded stream. A new bot plays at the fleet's
    /// difficulty and starts from the price the company's old bot expected, if it had one.
    /// </summary>
    public static SimulationBots Refresh(Simulation simulation, SimulationBots? current)
    {
        ArgumentNullException.ThrowIfNull(simulation);

        IReadOnlyList<ComplianceBot> existing = current?.Bots ?? [];
        BotSettings settings = existing.Count > 0 ? existing[0].Settings : BotSettings.For(simulation.Rules.AiDifficulty);
        List<ComplianceBot> bots = [];

        foreach (Company company in simulation.Companies)
        {
            List<Unit> units = Automated(company);
            ComplianceBot? kept = existing.FirstOrDefault(bot => bot.Company.Id == company.Id);

            if (units.Count == 0)
            {
                continue;
            }

            bots.Add(kept is not null && kept.Units.Select(unit => unit.Id).SequenceEqual(units.Select(unit => unit.Id))
                ? kept
                : new ComplianceBot(
                    company,
                    units,
                    kept?.Settings ?? settings,
                    simulation.Random,
                    kept?.ExpectedAllowancePrice ?? simulation.Rules.AuctionFloorPrice));
        }

        return new SimulationBots(bots);
    }

    private static List<Unit> Automated(Company company) =>
        [.. company.Units.Where(unit => company.Owner.Kind == PlayerKind.Ai || unit.AutoTrade)];

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
