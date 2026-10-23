using CarbonSim.Engine.Bots;
using CarbonSim.Engine.Domain;
using CarbonSim.Engine.Run;
using CarbonSim.Engine.Snapshot;
using FluentAssertions;

namespace CarbonSim.Engine.Tests.Bots;

/// <summary>
/// A trainer flipping one unit's AutoTrade switch changes one company's bot. Every other bot keeps
/// its progress, its trigger times and its settings, because rebuilding them would re-draw the
/// fleet from the seeded stream and change what the whole room's AI does for the rest of the run.
/// </summary>
public sealed class FleetRefreshTests
{
    [Fact]
    public void Switching_a_human_unit_on_adds_one_bot_and_keeps_the_rest()
    {
        Simulation simulation = TestSimulation.Build();
        SimulationBots fleet = SimulationBots.Create(simulation, BotSettings.For(BotDifficulty.Hard));
        ComplianceBot aiBot = fleet.Bots.Single();

        TestSimulation.Unit(simulation, 1).AutoTrade = true;
        SimulationBots refreshed = SimulationBots.Refresh(simulation, fleet);

        refreshed.Bots.Should().HaveCount(2);
        refreshed.Bots.Should().Contain(bot => ReferenceEquals(bot, aiBot), "the AI company's bot is untouched");

        ComplianceBot added = refreshed.Bots.Single(bot => !ReferenceEquals(bot, aiBot));
        added.Company.Id.Should().Be(1);
        added.Units.Select(unit => unit.Id).Should().Equal(1);
        added.Settings.Should().Be(aiBot.Settings, "the new bot plays at the fleet's difficulty");
    }

    [Fact]
    public void Switching_it_off_again_removes_only_that_bot()
    {
        Simulation simulation = TestSimulation.Build();
        TestSimulation.Unit(simulation, 1).AutoTrade = true;
        SimulationBots fleet = SimulationBots.Create(simulation);
        ComplianceBot aiBot = fleet.Bots.Single(bot => bot.Company.Owner.Kind == PlayerKind.Ai);

        TestSimulation.Unit(simulation, 1).AutoTrade = false;
        SimulationBots refreshed = SimulationBots.Refresh(simulation, fleet);

        refreshed.Bots.Should().ContainSingle().Which.Should().BeSameAs(aiBot);
    }

    [Fact]
    public void A_second_unit_of_the_same_company_replaces_that_companys_bot_only()
    {
        Simulation simulation = TestSimulation.Build();
        TestSimulation.Unit(simulation, 1).AutoTrade = true;
        SimulationBots fleet = SimulationBots.Create(simulation);
        ComplianceBot aiBot = fleet.Bots.Single(bot => bot.Company.Owner.Kind == PlayerKind.Ai);

        TestSimulation.Unit(simulation, 2).AutoTrade = true;
        SimulationBots refreshed = SimulationBots.Refresh(simulation, fleet);

        refreshed.Bots.Should().Contain(bot => ReferenceEquals(bot, aiBot));
        refreshed.Bots.Single(bot => bot.Company.Id == 1).Units.Select(unit => unit.Id).Should().Equal(1, 2);
    }

    [Fact]
    public void A_bot_saved_before_the_final_pass_existed_restores_with_one_in_the_middle_of_its_band()
    {
        Simulation simulation = TestSimulation.Build();
        SimulationRun run = SimulationRun.Create(simulation, new TestTimeProvider(), SimulationBots.Create(simulation));
        SimulationSnapshot captured = SimulationSnapshots.Capture(simulation, run.Clock, run.Exchange, run.Otc, run.Auctions, run.Bots);
        SimulationSnapshot older = captured with
        {
            Bots = new BotsSnapshot(
            [
                .. captured.Bots!.Bots.Select(bot => bot with
                {
                    TriggerTimes = [.. bot.TriggerTimes.Where(time => time.Trigger != BotTrigger.FinalPass)],
                }),
            ]),
        };

        ComplianceBot restored = SimulationSnapshots.Restore(older, new TestTimeProvider()).Bots!.Bots.Single();

        restored.TriggerTime(BotTrigger.FinalPass).Should().Be(0.92m);
        run.Bots!.Bots.Single().TriggerTime(BotTrigger.FinalPass).Should().BeInRange(0.88m, 0.96m, "a new bot draws its own");
    }

    [Fact]
    public void A_run_with_no_fleet_gets_one_when_a_unit_is_switched_on()
    {
        Simulation simulation = TestSimulation.Build();
        TestSimulation.Unit(simulation, 4).AutoTrade = true;

        SimulationBots refreshed = SimulationBots.Refresh(simulation, current: null);

        refreshed.Bots.Select(bot => bot.Company.Id).Should().Equal(2, 3);
    }
}
