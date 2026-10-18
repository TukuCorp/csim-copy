using System.Collections.Concurrent;
using System.Globalization;
using CarbonSim.Engine.Domain;
using CarbonSim.Web.Simulations;
using FluentAssertions;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;

namespace CarbonSim.Web.Tests.Simulations;

/// <summary>
/// The Phase 2 review found bot exchange fills were never announced on the tick that produced
/// them; they were dumped in a burst the next time a player acted. One journal cursor, advanced
/// under the run's gate by both paths, is now the single source of TradeExecuted for exchange
/// fills, so a bot fill is reported on its own tick exactly once.
/// </summary>
public sealed class BotFillBroadcastTests : IClassFixture<SimulationTestHost>, IAsyncLifetime
{
    private readonly SimulationTestHost _host;
    // Written from the connection's callback thread while the test reads it.
    private readonly ConcurrentQueue<HubEvent> _events = new();
    private HubConnection? _connection;
    private SignedIn _delta = null!;
    private Guid _simulationId;

    public BotFillBroadcastTests(SimulationTestHost host)
    {
        _host = host;
    }

    public async Task InitializeAsync()
    {
        Simulation simulation = TestRunFactory.BuildBotRun();
        SimulationRegistry runs = _host.Server.Services.GetRequiredService<SimulationRegistry>();
        _simulationId = runs.Start(simulation, automateHumans: true);
        _delta = await _host.SignInAsync(TestRunFactory.DeltaCompany);
        _connection = _host.CreateHubBuilder(_delta.Cookie).Build();

        _connection.On<string, string, decimal, decimal, string, string>(
            "TradeExecuted",
            (channel, product, price, volume, buyer, seller) =>
            {
                _events.Enqueue(new HubEvent("TradeExecuted", [channel, product, price, volume, buyer, seller]));
            });
        _connection.On<string, int, int, bool>(
            "MarketStateChanged",
            (state, year, auction, open) => _events.Enqueue(new HubEvent("MarketStateChanged", [state, year, auction, open])));

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
    public async Task A_bot_exchange_fill_is_announced_on_its_tick_and_only_once()
    {
        SimulationRegistry runs = _host.Server.Services.GetRequiredService<SimulationRegistry>();
        SimulationClockService driver = _host.CreateDriver();
        await driver.StartAsync(_simulationId);

        // Late in the year, past both of the bots' market passes, their orders cross and the fill
        // happens while the tick is running rather than in response to anything a player did.
        _host.FakeTime.Advance(TimeSpan.FromMinutes(19));
        await driver.TickAsync(_simulationId);

        await WaitForExchangeFillAsync();

        List<HubEvent> fills = ExchangeFills();

        fills.Should().ContainSingle();
        fills[0].Args[1].Should().Be("Vintage 1");
        Convert.ToDecimal(fills[0].Args[3], CultureInfo.InvariantCulture).Should().Be(1_000m);
        fills[0].Args[4].Should().Be(TestRunFactory.RedRiverCompany);
        fills[0].Args[5].Should().Be(TestRunFactory.DeltaCompany);

        // A later player order that does not trade must not re-announce the bot's fill. It has to
        // be inside the volatility band, which is anchored on the price the bots traded at.
        // The pause that follows is heard after anything the order sent, so waiting for it is
        // what makes "nothing more arrived" a real check rather than a guess at a delay.
        await runs.PlaceOrderAsync(_simulationId, _delta.Actor, 1, "Vintage 1", "Buy", "Limit", 1m, 100m);
        await driver.PauseAsync(_simulationId);

        for (int attempt = 0; attempt < 300 && !_events.Any(@event => @event.Method == "MarketStateChanged" && (string)@event.Args[0]! == "Paused"); attempt++)
        {
            await Task.Delay(TimeSpan.FromMilliseconds(100));
        }

        _events.Should().Contain(@event => @event.Method == "MarketStateChanged" && (string)@event.Args[0]! == "Paused");
        ExchangeFills().Should().ContainSingle();
    }

    private List<HubEvent> ExchangeFills() =>
    [
        .. _events.Where(@event => @event.Method == "TradeExecuted" && (string)@event.Args[0]! == "Exchange"),
    ];

    private async Task WaitForExchangeFillAsync()
    {
        // Long polling needs a moment: a tick that clears three auctions and lands an exchange fill
        // sends a run of events, and the connection has to drain them all.
        for (int attempt = 0; attempt < 300 && ExchangeFills().Count == 0; attempt++)
        {
            await Task.Delay(TimeSpan.FromMilliseconds(100));
        }
    }
}
