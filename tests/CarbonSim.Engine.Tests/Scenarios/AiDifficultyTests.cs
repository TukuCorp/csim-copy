using CarbonSim.Engine.Bots;
using CarbonSim.Engine.Domain;
using CarbonSim.Engine.Run;
using CarbonSim.Engine.Scenarios;
using CarbonSim.Engine.Snapshot;
using FluentAssertions;

namespace CarbonSim.Engine.Tests.Scenarios;

/// <summary>
/// The original's "AI Difficulty" is an exercise setting the trainer chooses before the run. A
/// scenario names it, leaving it out plays at Normal, the fleet is built at it, and a save keeps it,
/// so a bot added later by an AutoTrade switch plays at the same level as the rest.
/// </summary>
public sealed class AiDifficultyTests
{
    private static Simulation Load(string difficulty)
    {
        string json = File.ReadAllText(Path.Combine("scenarios", "vietnam-2024.json"))
            .Replace("\"forwardVintageShare\":", $"\"aiDifficulty\": \"{difficulty}\", \"forwardVintageShare\":", StringComparison.Ordinal);

        return ScenarioLoader.LoadJson(json, "ai-difficulty");
    }

    [Fact]
    public void A_scenario_can_set_the_difficulty_the_fleet_plays_at_and_a_save_keeps_it()
    {
        Simulation simulation = Load("hard");
        SimulationRun run = SimulationRun.Create(simulation, new TestTimeProvider());

        simulation.Rules.AiDifficulty.Should().Be(BotDifficulty.Hard);
        SimulationBots.Create(simulation).Bots.Should().OnlyContain(bot => bot.Settings.Difficulty == BotDifficulty.Hard);

        Parameters restored = SimulationSnapshots.Restore(
            SimulationSnapshots.Capture(simulation, run.Clock, run.Exchange, run.Otc, run.Auctions),
            new TestTimeProvider()).Simulation.Rules;

        restored.AiDifficulty.Should().Be(BotDifficulty.Hard);
    }

    [Fact]
    public void A_scenario_that_never_mentions_it_plays_at_normal()
    {
        Simulation simulation = ScenarioLoader.LoadFile(Path.Combine("scenarios", "vietnam-2024.json"));

        simulation.Rules.AiDifficulty.Should().Be(BotDifficulty.Normal);
    }

    [Fact]
    public void An_unknown_difficulty_is_refused_by_name()
    {
        Action load = () => Load("impossible");

        load.Should().Throw<ScenarioLoadException>()
            .Which.Errors.Should().Contain(error => error.Contains("aiDifficulty", StringComparison.Ordinal));
    }

    [Fact]
    public void A_bot_added_to_a_run_with_no_fleet_plays_at_the_exercise_difficulty()
    {
        Simulation simulation = Load("easy");
        simulation.Units[0].AutoTrade = true;

        SimulationBots refreshed = SimulationBots.Refresh(simulation, current: null);

        refreshed.Bots.Should().OnlyContain(bot => bot.Settings.Difficulty == BotDifficulty.Easy);
    }
}
