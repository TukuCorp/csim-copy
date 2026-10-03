using System.Collections.Concurrent;
using CarbonSim.Engine.Domain;
using CarbonSim.Web.Simulations;
using FluentAssertions;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;

namespace CarbonSim.Web.Tests.Simulations;

/// <summary>
/// Two runs under one host. A control aimed at one must leave the other where it was and a
/// broadcast must reach only the watchers of the run it belongs to, which is what makes more than
/// one exercise at a time safe.
/// </summary>
public sealed class RunIsolationTests : IClassFixture<SimulationTestHost>, IAsyncLifetime
{
    private readonly SimulationTestHost _host;
    private readonly ConcurrentQueue<(string State, int Year)> _runA = new();
    private readonly ConcurrentQueue<(string State, int Year)> _runB = new();
    private HubConnection? _watchA;
    private HubConnection? _watchB;
    private Guid _a;
    private Guid _b;

    public RunIsolationTests(SimulationTestHost host)
    {
        _host = host;
    }

    public async Task InitializeAsync()
    {
        SimulationRegistry runs = _host.Server.Services.GetRequiredService<SimulationRegistry>();
        _a = runs.Start(TestRunFactory.Build());
        _b = runs.Start(TestRunFactory.BuildOther());

        SignedIn first = await _host.SignInAsync(TestRunFactory.DeltaCompany);
        SignedIn second = await _host.SignInAsync(TestRunFactory.RedRiverCompany);

        _watchA = Watch(first.Cookie, _runA);
        _watchB = Watch(second.Cookie, _runB);
        await _watchA.StartAsync();
        await _watchB.StartAsync();
        await _watchA.InvokeAsync("JoinSimulation", _a);
        await _watchB.InvokeAsync("JoinSimulation", _b);
    }

    public async Task DisposeAsync()
    {
        if (_watchA is not null)
        {
            await _watchA.DisposeAsync();
        }

        if (_watchB is not null)
        {
            await _watchB.DisposeAsync();
        }
    }

    [Fact]
    public async Task Two_runs_are_separate_exercises_with_their_own_state()
    {
        SimulationRegistry runs = _host.Server.Services.GetRequiredService<SimulationRegistry>();

        _a.Should().NotBe(_b);
        runs.Run(_a).Simulation.Id.Should().Be(_a);
        runs.Run(_b).Simulation.Id.Should().Be(_b);
        runs.Run(_a).Simulation.Name.Should().NotBe(runs.Run(_b).Simulation.Name);

        SimulationClockService driver = _host.CreateDriver();
        await driver.StartAsync(_a);
        await driver.PauseAsync(_a);

        runs.Run(_a).Simulation.State.Should().Be(SimulationState.Paused);
        runs.Run(_b).Simulation.State.Should().Be(SimulationState.Pending, "nothing touched the other run");
        runs.Run(_b).Simulation.CurrentYear.Should().Be(0);
    }

    [Fact]
    public async Task A_control_broadcasts_to_one_run_and_not_the_other()
    {
        SimulationClockService driver = _host.CreateDriver();

        await driver.StartAsync(_a);

        for (int attempt = 0; attempt < 50 && _runA.IsEmpty; attempt++)
        {
            await Task.Delay(TimeSpan.FromMilliseconds(100));
        }

        _runA.Should().Contain(("Running", 1), "the watcher of the started run hears its state change");
        _runB.Should().BeEmpty("the other run's watcher hears nothing about a run it is not in");

        await driver.PauseAsync(_a);

        for (int attempt = 0; attempt < 50 && !_runA.Contains(("Paused", 1)); attempt++)
        {
            await Task.Delay(TimeSpan.FromMilliseconds(100));
        }

        _runA.Should().Contain(("Paused", 1));
        _runB.Should().BeEmpty();
    }

    private HubConnection Watch(string cookie, ConcurrentQueue<(string State, int Year)> seen)
    {
        HubConnection connection = _host.CreateHubBuilder(cookie).Build();

        connection.On<string, int>("SimulationStateChanged", (state, year) => seen.Enqueue((state, year)));

        return connection;
    }
}
