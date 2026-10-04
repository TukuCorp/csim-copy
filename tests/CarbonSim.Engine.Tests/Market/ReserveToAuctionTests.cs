using CarbonSim.Engine.Bots;
using CarbonSim.Engine.Clock;
using CarbonSim.Engine.Domain;
using CarbonSim.Engine.Market;
using CarbonSim.Engine.Market.Exchange;
using CarbonSim.Engine.Market.Otc;
using CarbonSim.Engine.Snapshot;
using FluentAssertions;

namespace CarbonSim.Engine.Tests.Market;

/// <summary>
/// The government's reserve-to-auction rule: a share of the volume an auction could not sell is
/// offered again in the next auction instead of sitting in the reserve for the rest of the run.
/// </summary>
public sealed class ReserveToAuctionTests
{
    private const decimal LotVolume = 237_500m;

    [Fact]
    public void By_default_unsold_volume_stays_in_reserve_as_before()
    {
        Simulation simulation = TestSimulation.Build();
        AuctionSchedule schedule = AuctionSchedule.Build(simulation);

        schedule.ForSection(1, 1).Clear();

        simulation.Government.Held(1).Should().Be(LotVolume, "the run behaves exactly as it did before the parameter existed");
        schedule.ForSection(1, 2).Lots.Should().ContainSingle().Which.Volume.Should().Be(LotVolume);
    }

    [Fact]
    public void A_share_of_the_unsold_volume_is_offered_again_in_the_next_auction()
    {
        Simulation simulation = TestSimulation.Build(governmentReserveToAuctionPercent: 0.50m);
        AuctionSchedule schedule = AuctionSchedule.Build(simulation);

        schedule.ForSection(1, 1).Clear();

        schedule.ForSection(1, 2).Lots.Sum(lot => lot.Volume).Should().Be(LotVolume, "the reserve is reviewed at the end of the year");
        simulation.Government.Held(1).Should().Be(LotVolume, "nothing leaves the reserve mid-year");

        for (int sequence = 2; sequence <= 4; sequence++)
        {
            schedule.ForSection(1, sequence).Clear();
        }

        Auction next = schedule.ForSection(2, 1);
        next.Lots.Should().HaveCount(2, "the volume offered again is a lot of its own vintage");
        next.Lots[1].Vintage.Should().Be(1);
        next.Lots[1].IsForward.Should().BeFalse();
        next.Lots[1].Volume.Should().Be((LotVolume * 4m) / 2m, "half of the year's unsold volume is offered again");
        simulation.Government.Held(1).Should().Be((LotVolume * 4m) / 2m, "only the share offered again has left the reserve");
    }

    [Fact]
    public void The_whole_reserve_can_be_offered_again()
    {
        Simulation simulation = TestSimulation.Build(governmentReserveToAuctionPercent: 1m);
        AuctionSchedule schedule = AuctionSchedule.Build(simulation);

        foreach (int sequence in new[] { 1, 2, 3, 4 })
        {
            schedule.ForSection(1, sequence).Clear();
        }

        schedule.ForSection(2, 1).Lots.Sum(lot => lot.Volume).Should().Be((LotVolume * 4m) + 230_375m);
        simulation.Government.Held(1).Should().Be(0m);
    }

    [Fact]
    public void Volume_offered_again_at_the_end_of_a_year_reaches_the_next_year()
    {
        Simulation simulation = TestSimulation.Build(governmentReserveToAuctionPercent: 1m);
        AuctionSchedule schedule = AuctionSchedule.Build(simulation);

        foreach (int sequence in new[] { 1, 2, 3, 4 })
        {
            schedule.ForSection(1, sequence).Clear();
        }

        // Every tonne offered in year 1 was left unsold and carried one auction at a time into
        // the next year's first auction, so the whole year-1 auctionable volume lands there
        // alongside that auction's own lot.
        Auction firstOfYearTwo = schedule.ForSection(2, 1);
        firstOfYearTwo.Lots.Should().HaveCount(2);
        firstOfYearTwo.Lots[1].Vintage.Should().Be(1, "the carried volume keeps the vintage it was issued under");
        firstOfYearTwo.Lots[1].Volume.Should().Be(950_000m, "the whole of year 1's auctionable volume was carried across");
        simulation.Government.Held(1).Should().Be(0m);
    }

    [Fact]
    public void The_last_auction_of_the_run_has_nowhere_to_offer_again_so_the_volume_stays_in_reserve()
    {
        Simulation simulation = TestSimulation.Build(governmentReserveToAuctionPercent: 1m);
        AuctionSchedule schedule = AuctionSchedule.Build(simulation);

        foreach (Auction auction in schedule.Auctions)
        {
            auction.Clear();
        }

        simulation.Government.Held(3).Should().BeGreaterThan(0m, "the final auction's unsold volume has no later auction");
    }

    [Fact]
    public void Volume_is_conserved_across_a_reoffer()
    {
        Simulation simulation = TestSimulation.Build(governmentReserveToAuctionPercent: 0.75m);
        AuctionSchedule schedule = AuctionSchedule.Build(simulation);

        schedule.ForSection(1, 1).Clear();

        decimal inReserveAndOffered = simulation.Government.Held(1)
            + schedule.Auctions.Where(auction => !auction.IsCleared).Sum(auction => auction.Lots.Where(lot => lot.Vintage == 1).Sum(lot => lot.Volume));

        inReserveAndOffered.Should().Be(950_000m, "no allowance is created or destroyed by offering it again");
    }

    [Fact]
    public void The_reoffer_share_has_to_be_a_fraction()
    {
        Parameters tooHigh = TestSimulation.Parameters() with { GovernmentReserveToAuctionPercent = 1.25m };
        Parameters negative = TestSimulation.Parameters() with { GovernmentReserveToAuctionPercent = -0.1m };

        tooHigh.Validate().Should().Contain(message => message.Contains("governmentReserveToAuctionPercent"));
        negative.Validate().Should().Contain(message => message.Contains("governmentReserveToAuctionPercent"));
        TestSimulation.Parameters().Validate().Should().BeEmpty();
    }

    [Fact]
    public void A_bot_bids_the_volume_offered_again()
    {
        Simulation simulation = TestSimulation.Build(governmentReserveToAuctionPercent: 1m);
        foreach (Unit unit in simulation.Units)
        {
            unit.AutoTrade = true;
        }

        AuctionSchedule schedule = AuctionSchedule.Build(simulation);
        TestTimeProvider time = new();
        SimulationClock clock = new(simulation, time);
        SimulationBots bots = SimulationBots.Create(simulation);
        BotMarkets markets = new(new Exchange(simulation), new OtcMarket(simulation), schedule);

        clock.Start();

        // Play year one the way a host does, but with nobody bidding, so all of its volume is
        // left unsold and carried into year two.
        while (clock.State != SimulationState.YearEnded)
        {
            time.Advance(TimeSpan.FromSeconds(30));
            IReadOnlyList<ClockEvent> events = clock.Advance();

            foreach (ClockEvent closed in events.Where(@event => @event.Kind == ClockEventKind.AuctionClosed))
            {
                schedule.ForSection(closed.Year, closed.Auction).Clear();
            }

            if (clock.State == SimulationState.TradingHalted)
            {
                clock.EndYear();
            }
        }

        clock.BeginNextYear();

        Auction second = schedule.ForSection(2, 1);
        second.Lots.Should().HaveCount(2, "the carried volume joins the year's own lot");

        // Run into year two's first auction window and let the bots bid.
        time.Advance(TimeSpan.FromMinutes(2) + TimeSpan.FromSeconds(1));
        clock.Advance();
        bots.Act(simulation, clock, markets);

        second.Bids.Should().NotBeEmpty("a bot bids for the volume offered again");
        second.Bids.Select(bid => bid.Vintage).Should().Contain(1, "the carried vintage is bid for");
    }

    [Fact]
    public void The_reoffer_share_survives_a_snapshot()
    {
        Simulation simulation = TestSimulation.Build(governmentReserveToAuctionPercent: 0.50m);
        AuctionSchedule schedule = AuctionSchedule.Build(simulation);
        TestTimeProvider time = new();
        SimulationClock clock = new(simulation, time);

        SimulationSnapshot snapshot = SimulationSnapshots.Capture(
            simulation,
            clock,
            new Exchange(simulation),
            new OtcMarket(simulation),
            schedule,
            bots: null);

        RestoredSimulation restored = SimulationSnapshots.Restore(snapshot, time);

        restored.Simulation.TradingSystems.Single().Parameters.GovernmentReserveToAuctionPercent
            .Should().Be(0.50m, "the reoffer share is a rule of the run, so it is carried with it");
        restored.Auctions.Auctions.Should().HaveCount(schedule.Auctions.Count);
    }
}
