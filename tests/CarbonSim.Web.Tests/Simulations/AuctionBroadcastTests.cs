using CarbonSim.Engine.Domain;
using CarbonSim.Web.Simulations;
using FluentAssertions;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;

namespace CarbonSim.Web.Tests.Simulations;

/// <summary>Every message the hub speaks, remembered in arrival order for assertions.</summary>
internal sealed record HubEvent(string Method, object?[] Args);

/// <summary>Two watchers on one run hear the same auction clear, sealed bids included.</summary>
public sealed class AuctionBroadcastTests : IClassFixture<SimulationTestHost>, IAsyncLifetime
{
    private readonly SimulationTestHost _host;
    private readonly List<HubEvent> _first = [];
    private readonly List<HubEvent> _second = [];
    private HubConnection? _firstConnection;
    private HubConnection? _secondConnection;
    private SignedIn _delta = null!;
    private SignedIn _redRiver = null!;
    private Guid _simulationId;

    public AuctionBroadcastTests(SimulationTestHost host)
    {
        _host = host;
    }

    public async Task InitializeAsync()
    {
        Simulation simulation = TestRunFactory.Build();
        SimulationRegistry runs = _host.Server.Services.GetRequiredService<SimulationRegistry>();
        _simulationId = runs.Start(simulation);
        _delta = await _host.SignInAsync(TestRunFactory.DeltaCompany);
        _redRiver = await _host.SignInAsync(TestRunFactory.RedRiverCompany);
        _firstConnection = Connection(_delta.Cookie, _first);
        _secondConnection = Connection(_redRiver.Cookie, _second);
        await _firstConnection.StartAsync();
        await _secondConnection.StartAsync();

        await _firstConnection.InvokeAsync("JoinSimulation", _simulationId);
        await _secondConnection.InvokeAsync("JoinSimulation", _simulationId);
    }

    public async Task DisposeAsync()
    {
        if (_firstConnection is not null)
        {
            await _firstConnection.DisposeAsync();
        }

        if (_secondConnection is not null)
        {
            await _secondConnection.DisposeAsync();
        }
    }

    [Fact]
    public async Task Two_watchers_hear_the_same_auction_clear()
    {
        SimulationRegistry runs = _host.Server.Services.GetRequiredService<SimulationRegistry>();
        SimulationClockService driver = _host.CreateDriver();
        await driver.StartAsync(_simulationId);
        runs.Run(_simulationId).Auctions.Auctions.Should().HaveCount(12);

        // Two sealed bids into the first auction's vintage-1 lot: one from each company. The
        // window opens 2 minutes in and closes at 5, so bidding at 3 minutes lands inside it.
        _host.FakeTime.Advance(TimeSpan.FromMinutes(3));
        await runs.BidAtAuctionAsync(_simulationId, _delta.Actor, 1, 1, 140m, 1_000m);
        await runs.BidAtAuctionAsync(_simulationId, _redRiver.Actor, 2, 1, 150m, 1_000m);
        _host.FakeTime.Advance(TimeSpan.FromMinutes(2));
        await driver.TickAsync(_simulationId);

        for (int attempt = 0; attempt < 50 && !_first.Any(@event => @event.Method == "AuctionCleared"); attempt++)
        {
            await Task.Delay(TimeSpan.FromMilliseconds(100));
        }

        AuctionCleared first = Cleared(_first);
        AuctionCleared second = Cleared(_second);
        first.Should().Be(second);
        first.Year.Should().Be(1);
        first.Sequence.Should().Be(1);

        // The two bids cover only a fraction of the 25,000 t lot, so the auction clears at
        // the floor rather than at either bid price.
        first.ClearingPrice.Should().Be(100m);
        first.VolumeSold.Should().Be(2_000m);

        _first.Should().ContainSingle(@event => @event.Method == "AuctionOpened");
        _second.Should().ContainSingle(@event => @event.Method == "AuctionOpened");
        _first.Should().ContainSingle(@event => @event.Method == "AuctionClosing");
        _second.Should().ContainSingle(@event => @event.Method == "AuctionClosing");
    }

    private HubConnection Connection(string cookie, List<HubEvent> seen)
    {
        HubConnection connection = _host.CreateHubBuilder(cookie).Build();

        connection.On<int, int, decimal?, decimal, decimal>(
            "AuctionCleared",
            (year, sequence, price, sold, offered) => { seen.Add(new HubEvent("AuctionCleared", [year, sequence, price, sold, offered])); });
        connection.On<int, int, decimal>(
            "AuctionOpened",
            (year, sequence, offered) => { seen.Add(new HubEvent("AuctionOpened", [year, sequence, offered])); });
        connection.On<int, int, TimeSpan>(
            "AuctionClosing",
            (year, sequence, left) => { seen.Add(new HubEvent("AuctionClosing", [year, sequence, left])); });

        return connection;
    }

    private static AuctionCleared Cleared(List<HubEvent> seen)
    {
        HubEvent cleared = seen.Should().ContainSingle(@event => @event.Method == "AuctionCleared").Subject;

        return new AuctionCleared(
            Convert.ToInt32(cleared.Args[0], System.Globalization.CultureInfo.InvariantCulture),
            Convert.ToInt32(cleared.Args[1], System.Globalization.CultureInfo.InvariantCulture),
            cleared.Args[2] is null ? null : Convert.ToDecimal(cleared.Args[2], System.Globalization.CultureInfo.InvariantCulture),
            Convert.ToDecimal(cleared.Args[3], System.Globalization.CultureInfo.InvariantCulture),
            Convert.ToDecimal(cleared.Args[4], System.Globalization.CultureInfo.InvariantCulture));
    }

    private sealed record AuctionCleared(int Year, int Sequence, decimal? ClearingPrice, decimal VolumeSold, decimal OfferedVolume);
}
