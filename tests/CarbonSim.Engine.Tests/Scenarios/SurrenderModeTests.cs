using CarbonSim.Engine.Domain;
using CarbonSim.Engine.Market;
using CarbonSim.Engine.Run;
using CarbonSim.Engine.Scenarios;
using CarbonSim.Engine.Snapshot;
using FluentAssertions;

namespace CarbonSim.Engine.Tests.Scenarios;

/// <summary>
/// Manual surrender and the banking override are exercise settings: a scenario names them, leaving
/// them out keeps the automatic year end the calibration was measured on, and a save keeps both the
/// settings and every surrender a company has set.
/// </summary>
public sealed class SurrenderModeTests
{
    private static Simulation Load(string settings)
    {
        string json = File.ReadAllText(Path.Combine("scenarios", "vietnam-2024.json"))
            .Replace("\"forwardVintageShare\":", $"{settings}, \"forwardVintageShare\":", StringComparison.Ordinal);

        return ScenarioLoader.LoadJson(json, "surrender-mode");
    }

    [Fact]
    public void A_scenario_that_never_mentions_them_surrenders_automatically_under_the_banking_limit()
    {
        Simulation simulation = ScenarioLoader.LoadFile(Path.Combine("scenarios", "vietnam-2024.json"));

        simulation.Rules.AutomaticSurrender.Should().BeTrue();
        simulation.Rules.UnlimitedBanking.Should().BeFalse();
    }

    [Fact]
    public void A_scenario_can_ask_for_manual_surrender_and_unlimited_banking_and_a_save_keeps_them()
    {
        Simulation simulation = Load("\"automaticSurrender\": false, \"unlimitedBanking\": true");
        SimulationRun run = SimulationRun.Create(simulation, new TestTimeProvider());
        run.Start();
        Company company = simulation.Companies.First(candidate => candidate.Owner.Kind == PlayerKind.Human);
        decimal held = simulation.Ledger.Available(company, Product.Allowance(1));
        simulation.Compliance.SetSurrender(company, 1, allowances: held / 2m, offsets: 0m);

        simulation.Rules.AutomaticSurrender.Should().BeFalse();
        simulation.Rules.UnlimitedBanking.Should().BeTrue();

        Simulation restored = SimulationSnapshots.Restore(
            SimulationSnapshots.Capture(simulation, run.Clock, run.Exchange, run.Otc, run.Auctions),
            new TestTimeProvider()).Simulation;

        restored.Rules.AutomaticSurrender.Should().BeFalse();
        restored.Rules.UnlimitedBanking.Should().BeTrue();
        restored.Compliance.InstructionFor(restored.FindCompany(company.Id), 1)!.Allowances.Should().Be(held / 2m);
    }
}
