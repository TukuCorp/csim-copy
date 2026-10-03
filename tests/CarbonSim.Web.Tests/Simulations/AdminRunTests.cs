using CarbonSim.Engine.Domain;
using CarbonSim.Engine.Market;
using CarbonSim.Engine.Scenarios;
using CarbonSim.Web.Simulations;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace CarbonSim.Web.Tests.Simulations;

/// <summary>
/// The administrator's run-time controls, driven through the same server-side service the console
/// calls. A run is started for each test under a seed of its own, so no test meets another's state.
/// </summary>
public sealed class AdminRunTests : IClassFixture<SimulationTestHost>
{
    private readonly SimulationTestHost _host;

    public AdminRunTests(SimulationTestHost host)
    {
        _host = host;
    }

    [Fact]
    public async Task The_clock_can_be_started_paused_resumed_halted_closed_and_reopened()
    {
        Guid id = StartRun(101);
        SimulationClockService driver = _host.CreateDriver();
        HostedRun run = Run(id);

        await driver.StartAsync(id);
        run.Simulation.State.Should().Be(SimulationState.Running);
        run.Simulation.CurrentYear.Should().Be(1);

        await driver.PauseAsync(id);
        run.Simulation.State.Should().Be(SimulationState.Paused);

        await driver.ResumeAsync(id);
        run.Simulation.State.Should().Be(SimulationState.Running);

        await driver.RequestHaltAsync(id);
        run.Simulation.State.Should().Be(SimulationState.TradingHalted);

        await driver.EndYearAsync(id);
        run.Simulation.State.Should().Be(SimulationState.YearEnded);

        await driver.BeginNextYearAsync(id);
        run.Simulation.State.Should().Be(SimulationState.Running);
        run.Simulation.CurrentYear.Should().Be(2);
    }

    [Fact]
    public async Task A_fine_takes_money_from_the_units_company()
    {
        Guid id = StartRun(102);
        SimulationClockService driver = _host.CreateDriver();
        HostedRun run = Run(id);
        Company company = run.Simulation.FindCompany(1);
        decimal before = company.Capital;

        await driver.StartAsync(id);
        await driver.IssueFineAsync(id, 1, 25_000m, "Late monitoring report");

        run.Simulation.Compliance.Fines.Should().ContainSingle();
        run.Simulation.Compliance.Fines[0].Amount.Should().Be(25_000m);
        company.Capital.Should().Be(before - 25_000m);
    }

    [Fact]
    public async Task Offsets_are_disbursed_to_the_named_company()
    {
        Guid id = StartRun(103);
        SimulationClockService driver = _host.CreateDriver();
        HostedRun run = Run(id);
        Company company = run.Simulation.FindCompany(1);

        await driver.DisburseOffsetsAsync(id, 1, 2_500m);

        run.Simulation.Ledger.Available(company, Product.Offset).Should().Be(2_500m);
    }

    [Fact]
    public async Task Abatement_can_be_added_for_a_unit_in_the_current_year()
    {
        Guid id = StartRun(104);
        SimulationClockService driver = _host.CreateDriver();
        HostedRun run = Run(id);
        Unit unit = run.Simulation.FindUnit(1);

        await driver.StartAsync(id);
        await driver.AddAbatementAsync(id, 1, 0);

        run.Simulation.Abatements.For(unit).Should().ContainSingle();
        run.Simulation.Abatements.For(unit)[0].ImplementedIn.Should().Be(1);
        run.Simulation.Abatements.For(unit)[0].Option.Code.Should().Be("P1");
    }

    [Fact]
    public async Task An_end_of_year_modification_changes_the_units_emissions_for_that_year()
    {
        Guid id = StartRun(105);
        SimulationClockService driver = _host.CreateDriver();
        HostedRun run = Run(id);
        Unit unit = run.Simulation.FindUnit(1);
        decimal before = run.Simulation.Allocation.BausEmissionsFor(unit, 1);

        await driver.ApplyShockAsync(id, 1, 1, 40_000m);

        run.Simulation.Allocation.BausEmissionsFor(unit, 1).Should().Be(before + 40_000m);
    }

    [Fact]
    public async Task The_AutoTrade_switch_rebuilds_the_runs_bot_fleet()
    {
        Guid id = StartRun(106);
        SimulationClockService driver = _host.CreateDriver();
        HostedRun run = Run(id);

        run.Bots.Should().BeNull("both companies are human and neither unit trades automatically");

        await driver.SetAutoTradeAsync(id, 1, enabled: true);

        run.Simulation.FindUnit(1).AutoTrade.Should().BeTrue();
        run.Bots.Should().NotBeNull();
        run.Bots!.Bots.Should().ContainSingle();

        await driver.SetAutoTradeAsync(id, 1, enabled: false);

        run.Bots.Should().BeNull();
    }

    [Fact]
    public async Task Turning_messaging_off_refuses_a_players_message()
    {
        Guid id = StartRun(107);
        SimulationClockService driver = _host.CreateDriver();
        SimulationRegistry runs = Registry();
        SignedIn delta = await _host.SignInAsync(TestRunFactory.DeltaCompany);

        await driver.StartAsync(id);
        await driver.SetMessagingAsync(id, enabled: false);

        Run(id).MessagingEnabled.Should().BeFalse();

        Func<Task> post = () => runs.PostMessageAsync(id, delta.Actor, "hello");

        await post.Should().ThrowAsync<InvalidOperationException>();

        await driver.SetMessagingAsync(id, enabled: true);
        await runs.PostMessageAsync(id, delta.Actor, "hello");
    }

    [Fact]
    public async Task A_run_created_from_a_scenario_is_saved_and_comes_back_after_a_load()
    {
        SimulationClockService driver = _host.CreateDriver();
        SimulationRegistry runs = Registry();

        Guid created = await driver.CreateRunAsync(TestRunFactory.ScenarioJson, "admin-setup-check");

        runs.Contains(created).Should().BeTrue();
        runs.Run(created).Simulation.State.Should().Be(SimulationState.Pending);

        runs.Remove(created).Should().BeTrue();
        runs.Contains(created).Should().BeFalse();

        Guid loaded = await driver.LoadRunAsync(created);

        loaded.Should().Be(created);
        runs.Contains(created).Should().BeTrue();
        runs.Run(created).Simulation.State.Should().Be(SimulationState.Pending);
        runs.Run(created).Simulation.Name.Should().Be("Hub test run");
    }

    private SimulationRegistry Registry() => _host.Server.Services.GetRequiredService<SimulationRegistry>();

    private HostedRun Run(Guid id) => Registry().Run(id);

    private Guid StartRun(int seed)
    {
        string json = TestRunFactory.ScenarioJson.Replace("\"seed\": 7", $"\"seed\": {seed}", StringComparison.Ordinal);

        return Registry().Start(ScenarioLoader.LoadJson(json, "admin-test"));
    }
}
