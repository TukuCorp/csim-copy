using CarbonSim.Engine.Clock;
using CarbonSim.Engine.Domain;
using CarbonSim.Engine.Run;
using CarbonSim.Engine.Snapshot;
using FluentAssertions;

namespace CarbonSim.Engine.Tests.Clock;

/// <summary>
/// "Pause after each auction" can resume itself after a set number of seconds (Spec p3; catalog
/// `AutoResumeAfterAuctionSeconds`), and the trainer can call off a resume that is pending (catalog
/// `CancelUnpause`). The pause still stops the clock: the time it waited is not counted in the year.
/// </summary>
public sealed class AutoResumeTests
{
    private static readonly TimeSpan Wait = TimeSpan.FromSeconds(30);

    // Section 5 min, auction window 3 min: auction 1 closes at 3:00.
    private static (Simulation Simulation, TestTimeProvider Time, SimulationClock Clock) Build()
    {
        Simulation simulation = TestSimulation.Build();
        TestTimeProvider time = new();
        SimulationClock clock = new(simulation, time, new SimulationClockOptions { PauseAfterAuction = true, AutoResumeAfterAuction = Wait });
        clock.Start();

        return (simulation, time, clock);
    }

    [Fact]
    public void The_clock_resumes_itself_once_the_wait_is_over_and_the_wait_is_not_counted()
    {
        (Simulation simulation, TestTimeProvider time, SimulationClock clock) = Build();
        time.Advance(TimeSpan.FromMinutes(3));
        clock.Advance();
        TimeSpan frozen = clock.ElapsedInYear;

        simulation.State.Should().Be(SimulationState.PausedAfterAuction);
        clock.TimeToAutoResume.Should().Be(Wait);

        time.Advance(Wait - TimeSpan.FromSeconds(1));
        clock.ResumeIfDue();
        simulation.State.Should().Be(SimulationState.PausedAfterAuction, "the wait is not over yet");

        time.Advance(TimeSpan.FromSeconds(1));
        clock.ResumeIfDue();

        simulation.State.Should().Be(SimulationState.Running);
        clock.ElapsedInYear.Should().Be(frozen);
        clock.TimeToAutoResume.Should().BeNull();
    }

    [Fact]
    public void The_trainer_can_call_off_a_pending_resume()
    {
        (Simulation simulation, TestTimeProvider time, SimulationClock clock) = Build();
        time.Advance(TimeSpan.FromMinutes(3));
        clock.Advance();

        clock.CancelAutoResume();
        time.Advance(Wait * 4);
        clock.ResumeIfDue();

        simulation.State.Should().Be(SimulationState.PausedAfterAuction);
        clock.TimeToAutoResume.Should().BeNull();
    }

    [Fact]
    public void A_run_that_is_advanced_while_paused_resumes_on_time_and_a_save_keeps_the_wait()
    {
        // The run builds its clock from the scenario's own settings.
        Simulation simulation = TestSimulation.Build(pauseAfterAuction: true, autoResumeAfterAuction: Wait);
        TestTimeProvider time = new();
        SimulationRun run = SimulationRun.Create(simulation, time);
        run.Start();
        time.Advance(TimeSpan.FromMinutes(3));
        run.Advance();

        run.Clock.State.Should().Be(SimulationState.PausedAfterAuction);

        SimulationSnapshot saved = SimulationSnapshots.Capture(run.Simulation, run.Clock, run.Exchange, run.Otc, run.Auctions);
        saved.Clock.ResumeAt.Should().NotBeNull();
        saved.Clock.AutoResumeAfterAuction.Should().Be(Wait);

        time.Advance(Wait);
        run.Advance();

        run.Clock.State.Should().Be(SimulationState.Running);
    }
}
