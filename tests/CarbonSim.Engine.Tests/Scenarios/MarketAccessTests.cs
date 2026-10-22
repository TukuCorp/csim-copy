using CarbonSim.Engine.Domain;
using CarbonSim.Engine.Run;
using CarbonSim.Engine.Scenarios;
using CarbonSim.Engine.Snapshot;
using FluentAssertions;

namespace CarbonSim.Engine.Tests.Scenarios;

/// <summary>
/// The spec's administrative switches for an exercise that introduces its markets in phases:
/// exchange access, OTC access and whether players see the leaderboard. A scenario names them,
/// leaving one out keeps it open, and a save carries them.
/// </summary>
public sealed class MarketAccessTests
{
    [Fact]
    public void A_scenario_can_close_the_exchange_and_the_otc_market_and_hide_the_leaderboard_and_a_save_keeps_it()
    {
        string json = File.ReadAllText(Path.Combine("scenarios", "vietnam-2024.json"))
            .Replace("\"forwardVintageShare\":", "\"exchangeAccess\": false, \"otcAccess\": false, \"leaderboardVisible\": false, \"forwardVintageShare\":", StringComparison.Ordinal);
        Simulation simulation = ScenarioLoader.LoadJson(json, "market-access");
        SimulationRun run = SimulationRun.Create(simulation, new TestTimeProvider());

        simulation.Rules.ExchangeAccess.Should().BeFalse();
        simulation.Rules.OtcAccess.Should().BeFalse();
        simulation.Rules.LeaderboardVisible.Should().BeFalse();

        Parameters restored = SimulationSnapshots.Restore(
            SimulationSnapshots.Capture(simulation, run.Clock, run.Exchange, run.Otc, run.Auctions),
            new TestTimeProvider()).Simulation.Rules;

        restored.ExchangeAccess.Should().BeFalse();
        restored.OtcAccess.Should().BeFalse();
        restored.LeaderboardVisible.Should().BeFalse();
    }

    [Fact]
    public void A_scenario_that_never_mentions_them_keeps_every_market_open()
    {
        Simulation simulation = ScenarioLoader.LoadFile(Path.Combine("scenarios", "vietnam-2024.json"));

        simulation.Rules.ExchangeAccess.Should().BeTrue();
        simulation.Rules.OtcAccess.Should().BeTrue();
        simulation.Rules.LeaderboardVisible.Should().BeTrue();
    }

    [Fact]
    public void A_scenario_can_ask_for_the_clock_to_pause_after_each_auction()
    {
        // The spec's auction settings include "pause after each".
        string json = File.ReadAllText(Path.Combine("scenarios", "vietnam-2024.json"))
            .Replace("\"forwardVintageShare\":", "\"pauseAfterAuction\": true, \"forwardVintageShare\":", StringComparison.Ordinal);
        Simulation simulation = ScenarioLoader.LoadJson(json, "pause-after-auction");
        TestTimeProvider time = new();
        SimulationRun run = SimulationRun.Create(simulation, time);
        run.Start();

        time.Advance(simulation.Rules.AuctionDuration + TimeSpan.FromSeconds(1));
        run.Advance();

        simulation.State.Should().Be(SimulationState.PausedAfterAuction);
    }
}
