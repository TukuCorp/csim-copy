using CarbonSim.Engine.Domain;
using CarbonSim.Engine.Snapshot;

namespace CarbonSim.Engine.Clock;

/// <summary>
/// The virtual-year clock. One virtual year is divided into as many sections as there are
/// auctions; each section ends with an auction window, and everything before that window is
/// the gap the players trade in. The clock reads the time from an injected
/// <see cref="TimeProvider"/> and never sleeps, blocks or spawns threads: a host calls
/// <see cref="Advance"/> whenever it wants, and the clock reports what became due.
/// </summary>
/// <remarks>
/// The clock owns <see cref="Simulation.State"/> and <see cref="Simulation.CurrentYear"/>, so
/// there is a single answer to "where are we". Pausing, halting and the end of a year all
/// freeze the year offset; only a running clock moves it.
/// </remarks>
public sealed class SimulationClock
{
    private readonly Simulation _simulation;
    private readonly Parameters _parameters;
    private readonly TimeProvider _timeProvider;
    private readonly SimulationClockOptions _options;
    private readonly TimeSpan _sectionLength;
    private readonly TimeSpan _auctionOpensAfter;

    private TimeSpan _elapsed;
    private DateTimeOffset? _runningSince;
    private DateTimeOffset? _haltAt;
    private int _openNoticedThrough;
    private int _openedThrough;
    private int _closeNoticedThrough;
    private int _closedThrough;
    private bool _haltedForYearEnd;

    public SimulationClock(
        Simulation simulation,
        TimeProvider timeProvider,
        SimulationClockOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(simulation);
        ArgumentNullException.ThrowIfNull(timeProvider);

        _simulation = simulation;
        _timeProvider = timeProvider;
        _options = options ?? new SimulationClockOptions();
        _parameters = simulation.TradingSystems.Single().Parameters;

        if (_parameters.AuctionsPerYear < 1)
        {
            throw new ArgumentException("The trading system needs at least one auction per year.", nameof(simulation));
        }

        _sectionLength = TimeSpan.FromTicks(_parameters.YearLength.Ticks / _parameters.AuctionsPerYear);

        if (_parameters.AuctionDuration > _sectionLength)
        {
            throw new ArgumentException(
                $"The auction window ({_parameters.AuctionDuration}) must fit inside a year section ({_sectionLength}).",
                nameof(simulation));
        }

        _auctionOpensAfter = _sectionLength - _parameters.AuctionDuration;
    }

    /// <summary>Or "game state" in the original's vocabulary.</summary>
    public SimulationState State => _simulation.State;

    public int CurrentYear => _simulation.CurrentYear;

    /// <summary>How many auctions the year holds.</summary>
    public int AuctionsInYear => _parameters.AuctionsPerYear;

    public TimeSpan YearLength => _parameters.YearLength;

    /// <summary>The section of the year the clock is in, 1-based, clamped to the last section.</summary>
    public int CurrentAuction => SectionOf(ElapsedInYear);

    /// <summary>Time run inside the current year; frozen while paused, halted or between years.</summary>
    public TimeSpan ElapsedInYear
    {
        get
        {
            TimeSpan elapsed = _elapsed;

            if (_runningSince is { } since)
            {
                elapsed += _timeProvider.GetUtcNow() - since;
            }

            return elapsed < TimeSpan.Zero ? TimeSpan.Zero : elapsed > _parameters.YearLength ? _parameters.YearLength : elapsed;
        }
    }

    public TimeSpan TimeLeftInYear => _parameters.YearLength - ElapsedInYear;

    /// <summary>True only while the clock runs and the current section's auction window is open.</summary>
    public bool IsAuctionOpen
    {
        get
        {
            if (State != SimulationState.Running)
            {
                return false;
            }

            TimeSpan elapsed = ElapsedInYear;
            TimeSpan start = SectionStart(SectionOf(elapsed));

            return elapsed >= start + _auctionOpensAfter && elapsed < start + _sectionLength;
        }
    }

    /// <summary>Time until the current section's auction window closes, or zero when none is open.</summary>
    public TimeSpan TimeToAuctionClose =>
        IsAuctionOpen
            ? SectionStart(SectionOf(ElapsedInYear)) + _sectionLength - ElapsedInYear
            : TimeSpan.Zero;

    /// <summary>Time until the current section's auction window opens, or zero when one is open.</summary>
    public TimeSpan TimeToAuctionOpen
    {
        get
        {
            if (IsAuctionOpen)
            {
                return TimeSpan.Zero;
            }

            TimeSpan elapsed = ElapsedInYear;
            int section = SectionOf(elapsed);
            TimeSpan opens = SectionStart(section) + _auctionOpensAfter;

            return elapsed < opens ? opens - elapsed : TimeSpan.Zero;
        }
    }

    /// <summary>
    /// The clock's own position and bookkeeping. The through-markers are captured as well as
    /// the offset: they are what stops the same auction-opening or year-ending notice being
    /// raised again after a restore, and re-running the year from zero would send every notice
    /// a second time.
    /// </summary>
    internal ClockSnapshot ToSnapshot()
    {
        return new ClockSnapshot(
            State,
            CurrentYear,
            _elapsed,
            _runningSince,
            _haltAt,
            _openNoticedThrough,
            _openedThrough,
            _closeNoticedThrough,
            _closedThrough,
            _haltedForYearEnd,
            _options.PauseAfterAuction,
            _options.AuctionNotice);
    }

    /// <summary>
    /// Puts back the position and notice markers a snapshot was taken with. The options are
    /// part of the clock's construction, so they arrive through the constructor rather than
    /// here; everything that moves while a run is played is put back below.
    /// </summary>
    internal void Restore(ClockSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        _elapsed = snapshot.Elapsed;
        _runningSince = snapshot.RunningSince;
        _haltAt = snapshot.HaltAt;
        _openNoticedThrough = snapshot.OpenNoticedThrough;
        _openedThrough = snapshot.OpenedThrough;
        _closeNoticedThrough = snapshot.CloseNoticedThrough;
        _closedThrough = snapshot.ClosedThrough;
        _haltedForYearEnd = snapshot.HaltedForYearEnd;
    }

    /// <summary>Begins year 1. Only a pending simulation can start.</summary>
    public IReadOnlyList<ClockEvent> Start()
    {
        Require(SimulationState.Pending, "A simulation can only be started while it is pending.");

        _simulation.CurrentYear = 1;
        _elapsed = TimeSpan.Zero;
        _runningSince = _timeProvider.GetUtcNow();
        ResetNotices();
        _simulation.State = SimulationState.Running;

        return [new ClockEvent(ClockEventKind.YearStarted, CurrentYear, 1, TimeSpan.Zero)];
    }

    /// <summary>
    /// Reports everything that became due since the last call. Safe to call at any time: while
    /// the clock is not running it does nothing.
    /// </summary>
    public IReadOnlyList<ClockEvent> Advance()
    {
        List<ClockEvent> events = [];

        if (State != SimulationState.Running)
        {
            return events;
        }

        RaiseDueEvents(events, ElapsedInYear);

        if (_haltAt is { } haltAt && _timeProvider.GetUtcNow() >= haltAt)
        {
            _haltAt = null;
            Freeze();
            _simulation.State = SimulationState.TradingHalted;
            events.Add(new ClockEvent(ClockEventKind.TradingHalted, CurrentYear, CurrentAuction, ElapsedInYear));
            return events;
        }

        HaltIfYearIsOver(events);

        return events;
    }

    /// <summary>Stops the clock where it is; the year offset is kept.</summary>
    public IReadOnlyList<ClockEvent> Pause()
    {
        Require(SimulationState.Running, "A simulation can only be paused while it is running.");

        Freeze();
        _simulation.State = SimulationState.Paused;

        return [];
    }

    /// <summary>Starts the clock again after a pause, a pause after an auction, or a halt.</summary>
    public IReadOnlyList<ClockEvent> Resume()
    {
        if (State is not (SimulationState.Paused or SimulationState.PausedAfterAuction or SimulationState.TradingHalted))
        {
            throw new InvalidOperationException($"A simulation in state {State} is not waiting to be resumed.");
        }

        _runningSince = _timeProvider.GetUtcNow();
        _simulation.State = SimulationState.Running;

        return [];
    }

    /// <summary>
    /// Halts trading, warning the players first when <paramref name="warnLead"/> is given so
    /// they can finish what they are doing. Without a lead time trading halts at once.
    /// </summary>
    public IReadOnlyList<ClockEvent> RequestHalt(TimeSpan? warnLead = null)
    {
        Require(SimulationState.Running, "Trading can only be halted while the clock is running.");

        if (warnLead is null || warnLead <= TimeSpan.Zero)
        {
            Freeze();
            _simulation.State = SimulationState.TradingHalted;

            return [new ClockEvent(ClockEventKind.TradingHalted, CurrentYear, CurrentAuction, ElapsedInYear)];
        }

        _haltAt = _timeProvider.GetUtcNow() + warnLead;

        return [new ClockEvent(ClockEventKind.TradingHaltWarned, CurrentYear, CurrentAuction, ElapsedInYear + warnLead.Value)];
    }

    /// <summary>Closes the year. Trading has to be halted first.</summary>
    public IReadOnlyList<ClockEvent> EndYear()
    {
        Require(SimulationState.TradingHalted, "Unable to end the year before trading has halted.");

        _simulation.State = SimulationState.YearEnded;

        return [new ClockEvent(ClockEventKind.YearEnded, CurrentYear, CurrentAuction, ElapsedInYear)];
    }

    /// <summary>Opens the next year, resetting the year offset and the section counters.</summary>
    public IReadOnlyList<ClockEvent> BeginNextYear()
    {
        Require(SimulationState.YearEnded, "Unable to start a new year before the previous year has ended.");

        if (CurrentYear >= _parameters.Years)
        {
            throw new InvalidOperationException($"Year {CurrentYear} was the last of {_parameters.Years}; end the simulation instead.");
        }

        _simulation.CurrentYear++;
        _elapsed = TimeSpan.Zero;
        _haltedForYearEnd = false;
        _runningSince = _timeProvider.GetUtcNow();
        ResetNotices();
        _simulation.State = SimulationState.Running;

        return [new ClockEvent(ClockEventKind.YearStarted, CurrentYear, 1, TimeSpan.Zero)];
    }

    /// <summary>Ends the simulation, once the last year has been closed.</summary>
    public IReadOnlyList<ClockEvent> EndSimulation()
    {
        if (State != SimulationState.YearEnded || CurrentYear < _parameters.Years)
        {
            throw new InvalidOperationException(
                $"The simulation can only end after year {_parameters.Years} has ended (now year {CurrentYear}, state {State}).");
        }

        _simulation.State = SimulationState.GameEnded;

        return [new ClockEvent(ClockEventKind.SimulationEnded, CurrentYear, CurrentAuction, ElapsedInYear)];
    }

    private void HaltIfYearIsOver(List<ClockEvent> events)
    {
        if (_haltedForYearEnd || ElapsedInYear < _parameters.YearLength)
        {
            return;
        }

        _haltedForYearEnd = true;
        Freeze();
        _simulation.State = SimulationState.TradingHalted;
        events.Add(new ClockEvent(ClockEventKind.YearEnding, CurrentYear, AuctionsInYear, _parameters.YearLength));
        events.Add(new ClockEvent(ClockEventKind.TradingHalted, CurrentYear, AuctionsInYear, _parameters.YearLength));
    }

    private void RaiseDueEvents(List<ClockEvent> events, TimeSpan elapsed)
    {
        for (int section = 1; section <= AuctionsInYear; section++)
        {
            TimeSpan sectionStart = SectionStart(section);
            TimeSpan opens = sectionStart + _auctionOpensAfter;
            TimeSpan closes = sectionStart + _sectionLength;

            if (_options.AuctionNotice > TimeSpan.Zero && _openNoticedThrough < section && elapsed >= opens - _options.AuctionNotice)
            {
                _openNoticedThrough = section;
                events.Add(new ClockEvent(ClockEventKind.AuctionOpeningSoon, CurrentYear, section, opens - _options.AuctionNotice));
            }

            if (_openedThrough < section && elapsed >= opens)
            {
                _openedThrough = section;
                events.Add(new ClockEvent(ClockEventKind.AuctionOpened, CurrentYear, section, opens));
            }

            if (_options.AuctionNotice > TimeSpan.Zero && _closeNoticedThrough < section && elapsed >= closes - _options.AuctionNotice)
            {
                _closeNoticedThrough = section;
                events.Add(new ClockEvent(ClockEventKind.AuctionEndingSoon, CurrentYear, section, closes - _options.AuctionNotice));
            }

            if (_closedThrough < section && elapsed >= closes)
            {
                _closedThrough = section;
                events.Add(new ClockEvent(ClockEventKind.AuctionClosed, CurrentYear, section, closes));

                if (_options.PauseAfterAuction && section < AuctionsInYear)
                {
                    Freeze();
                    _simulation.State = SimulationState.PausedAfterAuction;
                    return;
                }
            }
        }
    }

    private TimeSpan SectionStart(int section) => TimeSpan.FromTicks(_sectionLength.Ticks * (section - 1));

    private int SectionOf(TimeSpan elapsed)
    {
        int section = (int)(elapsed.Ticks / _sectionLength.Ticks) + 1;

        return section < 1 ? 1 : section > AuctionsInYear ? AuctionsInYear : section;
    }

    private void Freeze()
    {
        _elapsed = ElapsedInYear;
        _runningSince = null;
    }

    private void ResetNotices()
    {
        _openNoticedThrough = 0;
        _openedThrough = 0;
        _closeNoticedThrough = 0;
        _closedThrough = 0;
        _haltAt = null;
    }

    private void Require(SimulationState state, string message)
    {
        if (State != state)
        {
            throw new InvalidOperationException($"{message} (the simulation is {State}).");
        }
    }
}
