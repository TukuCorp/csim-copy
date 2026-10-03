using CarbonSim.Data.Persistence;
using CarbonSim.Engine.Domain;
using CarbonSim.Engine.Market;
using CarbonSim.Web.Simulations;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace CarbonSim.Web.Tests.Simulations;

/// <summary>The driver's year: auctions clear, the year is ended explicitly, and it saves once.</summary>
public sealed class DriverYearTests : IClassFixture<SimulationTestHost>
{
    private readonly SimulationTestHost _host;

    public DriverYearTests(SimulationTestHost host)
    {
        _host = host;
    }

    [Fact]
    public async Task A_full_year_reconciles_and_persists()
    {
        (Simulation _, Guid id, SimulationClockService driver) = await StartAsync();

        await PlayYearAsync(id, driver);

        // The last tick halts trading at the year boundary; ending the year is then explicit,
        // mirroring the administrator's End Year control rather than happening inside the tick.
        _host.FakeTime.Advance(TimeSpan.FromMinutes(2));
        await driver.TickAsync(id);
        Registry().Run(id).Simulation.State.Should().Be(SimulationState.TradingHalted);

        await driver.EndYearAsync(id);

        Registry().Run(id).Simulation.State.Should().Be(SimulationState.YearEnded);
        Registry().Run(id).Simulation.Compliance.Results.Should().HaveCount(2);
        Registry().Run(id).Auctions.Auctions.Where(auction => auction.Year == 1).Should().OnlyContain(auction => auction.IsCleared);

        using IServiceScope scope = _host.Server.Services.CreateScope();
        ISimulationRepository store = scope.ServiceProvider.GetRequiredService<ISimulationRepository>();
        (await store.LoadAsync(id)).Should().NotBeNull();
    }

    [Fact]
    public async Task A_year_is_saved_once_however_often_the_run_is_ticked()
    {
        (Simulation _, Guid id, SimulationClockService driver) = await StartAsync();

        await PlayYearAsync(id, driver);
        _host.FakeTime.Advance(TimeSpan.FromMinutes(2));
        await driver.TickAsync(id);

        // The counter is per host, so compare against where it stood before this year's save.
        int saved = _host.Saves.Count;
        await driver.EndYearAsync(id);
        _host.Saves.Count.Should().Be(saved + 1);

        await driver.TickAsync(id);
        await driver.TickAsync(id);

        _host.Saves.Count.Should().Be(saved + 1, "a year that has already ended is not written again by later ticks");
    }

    [Fact]
    public async Task The_next_year_begins_and_grants_its_free_allocation()
    {
        (Simulation simulation, Guid id, SimulationClockService driver) = await StartAsync();

        await PlayYearAsync(id, driver);
        _host.FakeTime.Advance(TimeSpan.FromMinutes(2));
        await driver.TickAsync(id);
        await driver.EndYearAsync(id);

        Company delta = simulation.Companies.Single(company => company.Name == TestRunFactory.DeltaCompany);
        decimal before = simulation.Ledger.Available(delta, Product.Allowance(2));

        await driver.BeginNextYearAsync(id);

        Registry().Run(id).Simulation.CurrentYear.Should().Be(2);
        Registry().Run(id).Simulation.State.Should().Be(SimulationState.Running);
        simulation.Ledger.Available(delta, Product.Allowance(2)).Should().BeGreaterThan(before);
    }

    [Fact]
    public async Task Bids_cover_only_what_the_lot_offers()
    {
        (Simulation _, Guid id, SimulationClockService driver) = await StartAsync();
        SignedIn delta = await _host.SignInAsync(TestRunFactory.DeltaCompany);

        _host.FakeTime.Advance(TimeSpan.FromMinutes(3));
        await Registry().BidAtAuctionAsync(id, delta.Actor, 1, 1, 140m, 500m);
        _host.FakeTime.Advance(TimeSpan.FromMinutes(2));
        await driver.TickAsync(id);

        Auction auction = Registry().Run(id).Auctions.ForSection(1, 1);
        auction.IsCleared.Should().BeTrue();
        auction.Results.Single().VolumeSold.Should().Be(500m);
        auction.Results.Single().ClearingPrice.Should().Be(100m, "a single small bid leaves the lot under-subscribed");
    }

    private SimulationRegistry Registry() => _host.Server.Services.GetRequiredService<SimulationRegistry>();

    private async Task<(Simulation Simulation, Guid Id, SimulationClockService Driver)> StartAsync()
    {
        Simulation simulation = TestRunFactory.Build();
        SimulationRegistry registry = _host.Server.Services.GetRequiredService<SimulationRegistry>();
        Guid id = registry.Start(simulation);
        SimulationClockService driver = _host.CreateDriver();
        await driver.StartAsync(id);

        return (simulation, id, driver);
    }

    /// <summary>
    /// Plays the four sections of year 1 with two bids in each, so every auction has something
    /// to clear, and ticks up to the year boundary.
    /// </summary>
    private async Task PlayYearAsync(Guid id, SimulationClockService driver)
    {
        SimulationRegistry registry = Registry();
        SignedIn delta = await _host.SignInAsync(TestRunFactory.DeltaCompany);
        SignedIn redRiver = await _host.SignInAsync(TestRunFactory.RedRiverCompany);

        for (int section = 1; section <= 4; section++)
        {
            _host.FakeTime.Advance(TimeSpan.FromMinutes(section == 1 ? 3 : 5));
            await registry.BidAtAuctionAsync(id, delta.Actor, 1, 1, 140m, 500m);
            await registry.BidAtAuctionAsync(id, redRiver.Actor, 2, 1, 150m, 500m);
            await driver.TickAsync(id);
        }
    }
}
