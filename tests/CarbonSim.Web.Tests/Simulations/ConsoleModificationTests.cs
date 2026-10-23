using CarbonSim.Engine.Domain;
using CarbonSim.Engine.Scenarios;
using CarbonSim.Web.Simulations;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace CarbonSim.Web.Tests.Simulations;

/// <summary>
/// The trainer's end-of-year system modifications and the auction pause that resumes itself, driven
/// through the host the console calls.
/// </summary>
public sealed class ConsoleModificationTests : IClassFixture<SimulationTestHost>
{
    private readonly SimulationTestHost _host;

    public ConsoleModificationTests(SimulationTestHost host) => _host = host;

    [Fact]
    public async Task The_trainer_changes_the_compliance_rules_and_the_room_is_told()
    {
        Guid id = Registry().Start(Build(seed: 8101, extra: string.Empty));
        SimulationClockService driver = _host.CreateDriver();

        await driver.ChangeComplianceRulesAsync(id, offsetUsageLimit: 0.05m, bankingLimit: 0.5m, penaltyPerTonne: 450m);

        HostedRun run = Registry().Run(id);
        Parameters rules = run.Simulation.Rules;

        rules.OffsetUsageLimit.Should().Be(0.05m);
        rules.BankingLimit.Should().Be(0.5m);
        rules.PenaltyPerTonne.Should().Be(450m);
        run.Chat.Should().ContainSingle(line => line.From == "Administrator" && line.To == null && line.Text.Contains("5", StringComparison.Ordinal));
    }

    [Fact]
    public async Task An_auction_pause_resumes_itself_unless_the_trainer_calls_it_off()
    {
        SimulationClockService driver = _host.CreateDriver();
        Guid resumes = Registry().Start(Build(seed: 8102, extra: "\"pauseAfterAuction\": true, \"autoResumeAfterAuctionSeconds\": 30,"));
        Guid held = Registry().Start(Build(seed: 8103, extra: "\"pauseAfterAuction\": true, \"autoResumeAfterAuctionSeconds\": 30,"));
        await driver.StartAsync(resumes);
        await driver.StartAsync(held);

        // Auction 1's window is the first 3 minutes of the year.
        _host.FakeTime.Advance(TimeSpan.FromMinutes(3));
        await driver.TickAsync(resumes);
        await driver.TickAsync(held);

        Registry().Run(resumes).Clock.State.Should().Be(SimulationState.PausedAfterAuction);
        Registry().Run(held).Clock.TimeToAutoResume.Should().Be(TimeSpan.FromSeconds(30));

        await driver.CancelAutoResumeAsync(held);
        _host.FakeTime.Advance(TimeSpan.FromSeconds(31));
        await driver.TickAsync(resumes);
        await driver.TickAsync(held);

        Registry().Run(resumes).Clock.State.Should().Be(SimulationState.Running);
        Registry().Run(held).Clock.State.Should().Be(SimulationState.PausedAfterAuction, "the trainer called the resume off");
    }

    private SimulationRegistry Registry() => _host.Server.Services.GetRequiredService<SimulationRegistry>();

    private static Simulation Build(int seed, string extra) => ScenarioLoader.LoadJson(
        TestRunFactory.ScenarioJson
            .Replace("\"seed\": 7", $"\"seed\": {seed}", StringComparison.Ordinal)
            .Replace("\"volatilityBand\":", extra + " \"volatilityBand\":", StringComparison.Ordinal),
        "console-modifications");
}
