using System.Collections.Concurrent;
using CarbonSim.Engine.Domain;
using CarbonSim.Web.Simulations;
using FluentAssertions;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;

namespace CarbonSim.Web.Tests.Simulations;

/// <summary>
/// A change the run has already made is real whether or not the save after it worked. If the save
/// fails the trainer is told, but the room still has to hear the change, or every screen goes on
/// showing a state the run has left.
/// </summary>
public sealed class SaveFailureBroadcastTests : IClassFixture<SimulationTestHost>, IAsyncLifetime
{
    private readonly SimulationTestHost _host;
    private readonly ConcurrentQueue<string> _states = new();
    private HubConnection? _connection;
    private Guid _simulationId;

    public SaveFailureBroadcastTests(SimulationTestHost host)
    {
        _host = host;
    }

    public async Task InitializeAsync()
    {
        Simulation simulation = TestRunFactory.Build();
        _simulationId = _host.Server.Services.GetRequiredService<SimulationRegistry>().Start(simulation);
        SignedIn delta = await _host.SignInAsync(TestRunFactory.DeltaCompany);
        _connection = _host.CreateHubBuilder(delta.Cookie).Build();
        _connection.On<string, int, int, bool>("MarketStateChanged", (state, _, _, _) => _states.Enqueue(state));
        await _connection.StartAsync();
        await _connection.InvokeAsync("JoinSimulation", _simulationId);
    }

    public async Task DisposeAsync()
    {
        if (_connection is not null)
        {
            await _connection.DisposeAsync();
        }
    }

    [Fact]
    public async Task A_change_is_announced_even_when_the_save_after_it_fails()
    {
        SimulationRegistry runs = _host.Server.Services.GetRequiredService<SimulationRegistry>();
        await _host.CreateDriver().StartAsync(_simulationId);

        Func<Task> pauseThenFailToSave = () => runs.ApplyAsync(
            _simulationId,
            run => run.Module.Pause(),
            _ => throw new IOException("The disk is full."));

        await pauseThenFailToSave.Should().ThrowAsync<IOException>("the trainer has to be told the save failed");

        for (int attempt = 0; attempt < 100 && !_states.Contains("Paused"); attempt++)
        {
            await Task.Delay(TimeSpan.FromMilliseconds(100));
        }

        _states.Should().Contain("Paused", "the run did pause, so the room has to hear it");
    }
}
