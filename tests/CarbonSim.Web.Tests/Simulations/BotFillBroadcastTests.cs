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
    private readonly List<HubEvent> _events = [];
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
                _events.Add(new HubEvent("TradeExecuted", [channel, product, price, volume, buyer, seller]));
            });

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

        // Past 60% of the year, where both bots' trade triggers lie, their orders cross and the
        // fill happens while the tick is running rather than in response to anything a player did.
        _host.FakeTime.Advance(TimeSpan.FromMinutes(13));
        await driver.TickAsync(_simulationId);

        await WaitForExchangeFillAsync();

        List<HubEvent> fills = ExchangeFills();
        fills.Should().ContainSingle();
        fills[0].Args[1].Should().Be("Vintage 1");
        Convert.ToDecimal(fills[0].Args[3], CultureInfo.InvariantCulture).Should().Be(1_000m);
        fills[0].Args[4].Should().Be(TestRunFactory.RedRiverCompany);
        fills[0].Args[5].Should().Be(TestRunFactory.DeltaCompany);

        // A later player order that does not trade must not re-announce the bot's fill.
        await runs.PlaceOrderAsync(_simulationId, _delta.Actor, 1, "Vintage 1", "Buy", "Limit", 1m, 90m);
        await Task.Delay(TimeSpan.FromMilliseconds(200));

        ExchangeFills().Should().ContainSingle();
    }

    private List<HubEvent> ExchangeFills() =>
    [
        .. _events.Where(@event => @event.Method == "TradeExecuted" && (string)@event.Args[0]! == "Exchange"),
    ];

    private async Task WaitForExchangeFillAsync()
    {
        for (int attempt = 0; attempt < 50 && ExchangeFills().Count == 0; attempt++)
        {
            await Task.Delay(TimeSpan.FromMilliseconds(100));
        }
    }
}
