using CarbonSim.Engine.Clock;
using CarbonSim.Engine.Domain;
using FluentAssertions;

namespace CarbonSim.Engine.Tests.Clock;

public sealed class SimulationClockTests
{
    private const int AuctionsPerYear = 4;

    /// <summary>Section 5 min, auction window 3 min: opens at 2:00, closes at 5:00 of each section.</summary>
    private static (Simulation Simulation, TestTimeProvider Time, SimulationClock Clock) Build(
        bool pauseAfterAuction = false)
    {
        Simulation simulation = TestSimulation.Build();
        TestTimeProvider time = new();
        SimulationClockOptions options = new() { PauseAfterAuction = pauseAfterAuction };

        return (simulation, time, new SimulationClock(simulation, time, options));
    }

    private static IReadOnlyList<ClockEvent> Advance(TestTimeProvider time, SimulationClock clock, TimeSpan amount)
    {
        time.Advance(amount);
        return clock.Advance();
    }

    [Fact]
    public void A_new_simulation_waits_to_start()
    {
        (Simulation simulation, _, SimulationClock clock) = Build();

        simulation.State.Should().Be(SimulationState.Pending);
        clock.State.Should().Be(SimulationState.Pending);
        clock.CurrentYear.Should().Be(0);
        clock.ElapsedInYear.Should().Be(TimeSpan.Zero);
    }

    [Fact]
    public void Starting_opens_year_one()
    {
        (Simulation simulation, _, SimulationClock clock) = Build();

        IReadOnlyList<ClockEvent> events = clock.Start();

        simulation.State.Should().Be(SimulationState.Running);
        clock.CurrentYear.Should().Be(1);
        clock.CurrentAuction.Should().Be(1);
        events.Should().ContainSingle().Which.Kind.Should().Be(ClockEventKind.YearStarted);
    }

    [Fact]
    public void A_simulation_can_only_start_from_pending()
    {
        (_, _, SimulationClock clock) = Build();
        clock.Start();

        Action startAgain = () => clock.Start();

        startAgain.Should().Throw<InvalidOperationException>().WithMessage("*pending*");
    }

    [Fact]
    public void Advancing_without_time_passing_raises_nothing()
    {
        (_, _, SimulationClock clock) = Build();
        clock.Start();

        clock.Advance().Should().BeEmpty();
        clock.ElapsedInYear.Should().Be(TimeSpan.Zero);
    }

    [Fact]
    public void The_first_auction_opens_after_the_lead_in_and_closes_at_the_end_of_its_section()
    {
        (_, TestTimeProvider time, SimulationClock clock) = Build();
        clock.Start();

        Advance(time, clock, TimeSpan.FromMinutes(1.5));
        clock.IsAuctionOpen.Should().BeFalse();

        Advance(time, clock, TimeSpan.FromSeconds(31));
        clock.IsAuctionOpen.Should().BeTrue();
        clock.TimeToAuctionClose.Should().Be(TimeSpan.FromSeconds(179));
        clock.ElapsedInYear.Should().Be(TimeSpan.FromMinutes(2) + TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void Every_section_of_the_year_gets_its_own_auction_notices()
    {
        (_, TestTimeProvider time, SimulationClock clock) = Build();
        clock.Start();

        List<ClockEventKind> kinds = [];

        for (int tick = 0; tick < 80; tick++)
        {
            time.Advance(TimeSpan.FromSeconds(15));
            kinds.AddRange(clock.Advance().Select(raised => raised.Kind));
        }

        kinds.Count(kind => kind == ClockEventKind.AuctionOpeningSoon).Should().Be(AuctionsPerYear);
        kinds.Count(kind => kind == ClockEventKind.AuctionOpened).Should().Be(AuctionsPerYear);
        kinds.Count(kind => kind == ClockEventKind.AuctionEndingSoon).Should().Be(AuctionsPerYear);
        kinds.Count(kind => kind == ClockEventKind.AuctionClosed).Should().Be(AuctionsPerYear);
        kinds.Should().EndWith([ClockEventKind.YearEnding, ClockEventKind.TradingHalted]);
    }

    [Fact]
    public void Auction_notices_fire_once_each()
    {
        (_, TestTimeProvider time, SimulationClock clock) = Build();
        clock.Start();

        Advance(time, clock, TimeSpan.FromSeconds(90))
            .Should().ContainSingle().Which.Kind.Should().Be(ClockEventKind.AuctionOpeningSoon);

        Advance(time, clock, TimeSpan.FromSeconds(1)).Should().BeEmpty();
    }

    [Fact]
    public void An_auction_closes_and_the_clock_keeps_running_when_the_trainer_did_not_ask_to_pause()
    {
        (Simulation simulation, TestTimeProvider time, SimulationClock clock) = Build();
        clock.Start();

        Advance(time, clock, TimeSpan.FromSeconds(90))
            .Should().ContainSingle().Which.Kind.Should().Be(ClockEventKind.AuctionOpeningSoon);
        Advance(time, clock, TimeSpan.FromSeconds(30))
            .Should().ContainSingle().Which.Kind.Should().Be(ClockEventKind.AuctionOpened);

        Advance(time, clock, TimeSpan.FromMinutes(2) + TimeSpan.FromSeconds(29)).Should().BeEmpty();
        Advance(time, clock, TimeSpan.FromSeconds(1))
            .Should().ContainSingle().Which.Kind.Should().Be(ClockEventKind.AuctionEndingSoon);

        Advance(time, clock, TimeSpan.FromSeconds(30))
            .Should().ContainSingle().Which.Kind.Should().Be(ClockEventKind.AuctionClosed);

        simulation.State.Should().Be(SimulationState.Running);
        clock.CurrentAuction.Should().Be(2);
        clock.IsAuctionOpen.Should().BeFalse();
    }

    [Fact]
    public void With_pause_after_auction_the_clock_stops_until_the_trainer_resumes()
    {
        (Simulation simulation, TestTimeProvider time, SimulationClock clock) = Build(pauseAfterAuction: true);
        clock.Start();

        Advance(time, clock, TimeSpan.FromMinutes(5));

        simulation.State.Should().Be(SimulationState.PausedAfterAuction);
        TimeSpan frozen = clock.ElapsedInYear;

        Advance(time, clock, TimeSpan.FromMinutes(3));
        clock.ElapsedInYear.Should().Be(frozen);
        clock.CurrentAuction.Should().Be(2);

        clock.Resume();
        simulation.State.Should().Be(SimulationState.Running);
        Advance(time, clock, TimeSpan.FromMinutes(1));
        clock.ElapsedInYear.Should().Be(frozen + TimeSpan.FromMinutes(1));
    }

    [Fact]
    public void The_last_section_halts_trading_so_the_year_can_be_ended()
    {
        (Simulation simulation, TestTimeProvider time, SimulationClock clock) = Build();
        clock.Start();

        Advance(time, clock, TimeSpan.FromMinutes(20));

        simulation.State.Should().Be(SimulationState.TradingHalted);
        clock.ElapsedInYear.Should().Be(TimeSpan.FromMinutes(20));
        clock.CurrentAuction.Should().Be(AuctionsPerYear);
    }

    [Fact]
    public void A_year_can_only_be_ended_after_trading_has_halted()
    {
        (_, _, SimulationClock clock) = Build();
        clock.Start();

        Action endWhileRunning = () => clock.EndYear();

        endWhileRunning.Should().Throw<InvalidOperationException>().WithMessage("*halted*");
    }

    [Fact]
    public void Ending_a_year_moves_to_year_ended_and_beginning_the_next_year_restarts_the_sections()
    {
        (Simulation simulation, TestTimeProvider time, SimulationClock clock) = Build();
        clock.Start();
        Advance(time, clock, TimeSpan.FromMinutes(20));

        IReadOnlyList<ClockEvent> ended = clock.EndYear();

        simulation.State.Should().Be(SimulationState.YearEnded);
        ended.Should().ContainSingle().Which.Kind.Should().Be(ClockEventKind.YearEnded);

        IReadOnlyList<ClockEvent> started = clock.BeginNextYear();

        simulation.State.Should().Be(SimulationState.Running);
        clock.CurrentYear.Should().Be(2);
        clock.CurrentAuction.Should().Be(1);
        clock.ElapsedInYear.Should().Be(TimeSpan.Zero);
        started.Should().ContainSingle().Which.Kind.Should().Be(ClockEventKind.YearStarted);
    }

    [Fact]
    public void A_new_year_cannot_start_before_the_previous_one_has_ended()
    {
        (_, _, SimulationClock clock) = Build();
        clock.Start();

        Action begin = () => clock.BeginNextYear();

        begin.Should().Throw<InvalidOperationException>().WithMessage("*ended*");
    }

    [Fact]
    public void Pausing_requires_a_running_clock()
    {
        (_, _, SimulationClock clock) = Build();

        Action pause = () => clock.Pause();

        pause.Should().Throw<InvalidOperationException>().WithMessage("*running*");
    }

    [Fact]
    public void Pausing_freezes_the_year_and_resuming_continues_it()
    {
        (Simulation simulation, TestTimeProvider time, SimulationClock clock) = Build();
        clock.Start();
        Advance(time, clock, TimeSpan.FromMinutes(1));

        clock.Pause();
        simulation.State.Should().Be(SimulationState.Paused);

        Advance(time, clock, TimeSpan.FromMinutes(4));
        clock.ElapsedInYear.Should().Be(TimeSpan.FromMinutes(1));

        clock.Resume();
        Advance(time, clock, TimeSpan.FromMinutes(1));
        clock.ElapsedInYear.Should().Be(TimeSpan.FromMinutes(2));
    }

    [Fact]
    public void Halting_trading_warns_players_first_when_a_warning_lead_is_configured()
    {
        (Simulation simulation, TestTimeProvider time, SimulationClock clock) = Build();
        clock.Start();
        Advance(time, clock, TimeSpan.FromSeconds(30));

        IReadOnlyList<ClockEvent> warned = clock.RequestHalt(TimeSpan.FromSeconds(30));

        warned.Should().ContainSingle().Which.Kind.Should().Be(ClockEventKind.TradingHaltWarned);
        simulation.State.Should().Be(SimulationState.Running, "the warning window is the players' last chance to trade");

        Advance(time, clock, TimeSpan.FromSeconds(29)).Should().BeEmpty();
        simulation.State.Should().Be(SimulationState.Running);

        IReadOnlyList<ClockEvent> halted = Advance(time, clock, TimeSpan.FromSeconds(1));

        halted.Should().ContainSingle().Which.Kind.Should().Be(ClockEventKind.TradingHalted);
        simulation.State.Should().Be(SimulationState.TradingHalted);

        TimeSpan frozen = clock.ElapsedInYear;
        Advance(time, clock, TimeSpan.FromMinutes(2));
        clock.ElapsedInYear.Should().Be(frozen);
    }

    [Fact]
    public void Halting_without_a_warning_lead_halts_immediately()
    {
        (Simulation simulation, _, SimulationClock clock) = Build();
        clock.Start();

        clock.RequestHalt().Should().ContainSingle().Which.Kind.Should().Be(ClockEventKind.TradingHalted);
        simulation.State.Should().Be(SimulationState.TradingHalted);
    }

    [Fact]
    public void Trading_can_be_resumed_after_a_halt()
    {
        (Simulation simulation, TestTimeProvider time, SimulationClock clock) = Build();
        clock.Start();
        Advance(time, clock, TimeSpan.FromMinutes(2));
        clock.RequestHalt();
        TimeSpan frozen = clock.ElapsedInYear;

        clock.Resume();

        simulation.State.Should().Be(SimulationState.Running);
        Advance(time, clock, TimeSpan.FromMinutes(1));
        clock.ElapsedInYear.Should().Be(frozen + TimeSpan.FromMinutes(1));
    }

    [Fact]
    public void The_simulation_ends_only_after_the_last_year_has_ended()
    {
        (Simulation simulation, TestTimeProvider time, SimulationClock clock) = Build();
        clock.Start();

        Action tooEarly = () => clock.EndSimulation();
        tooEarly.Should().Throw<InvalidOperationException>().WithMessage("*year*");

        Advance(time, clock, TimeSpan.FromMinutes(20));
        clock.EndYear();
        Action afterFirstYear = () => clock.EndSimulation();
        afterFirstYear.Should().Throw<InvalidOperationException>("two years are still to be played");

        clock.BeginNextYear();
        Advance(time, clock, TimeSpan.FromMinutes(20));
        clock.EndYear();
        clock.BeginNextYear();
        Advance(time, clock, TimeSpan.FromMinutes(20));
        clock.EndYear();

        clock.EndSimulation().Should().ContainSingle().Which.Kind.Should().Be(ClockEventKind.SimulationEnded);
        simulation.State.Should().Be(SimulationState.GameEnded);
    }

    [Fact]
    public void The_clock_reports_what_the_progress_strip_shows()
    {
        (_, TestTimeProvider time, SimulationClock clock) = Build();
        clock.Start();
        Advance(time, clock, TimeSpan.FromMinutes(7));

        clock.CurrentYear.Should().Be(1);
        clock.CurrentAuction.Should().Be(2);
        clock.ElapsedInYear.Should().Be(TimeSpan.FromMinutes(7));
        clock.TimeLeftInYear.Should().Be(TimeSpan.FromMinutes(13));
        clock.IsAuctionOpen.Should().BeTrue();
    }

    [Fact]
    public void An_auction_window_must_fit_inside_its_section()
    {
        Simulation simulation = TestSimulation.Build(auctionDuration: TimeSpan.FromMinutes(6));

        Action build = () => new SimulationClock(simulation, new TestTimeProvider());

        build.Should().Throw<ArgumentException>().WithMessage("*section*");
    }
}
