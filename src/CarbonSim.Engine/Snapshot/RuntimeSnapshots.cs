using CarbonSim.Engine.Bots;
using CarbonSim.Engine.Domain;

namespace CarbonSim.Engine.Snapshot;

/// <summary>
/// The clock's own bookkeeping: how far into the year it is, when it was last started, and
/// which of the year's notices it has already raised. The through-markers matter as much as
/// the offset does — a restored clock that forgot them would raise the same auction-opening or
/// year-ending notice twice. State and year are mirrored from the simulation so a clock row
/// read on its own still says where the run is.
/// </summary>
/// <remarks>
/// <see cref="Elapsed"/> and <see cref="RunningSince"/> are the clock's own pair. While the
/// clock is running the offset in the year is <see cref="Elapsed"/> plus the wall time since
/// <see cref="RunningSince"/>; while it is paused, halted or between years
/// <see cref="RunningSince"/> is null and <see cref="Elapsed"/> is the frozen offset. Both are
/// stored, rather than the elapsed figure alone, because only the pair rebuilds a running clock
/// without the host having to guess when it was started.
/// </remarks>
public sealed record ClockSnapshot(
    SimulationState State,
    int CurrentYear,
    TimeSpan Elapsed,
    DateTimeOffset? RunningSince,
    DateTimeOffset? HaltAt,
    int OpenNoticedThrough,
    int OpenedThrough,
    int CloseNoticedThrough,
    int ClosedThrough,
    bool HaltedForYearEnd,
    bool PauseAfterAuction,
    TimeSpan AuctionNotice);

/// <summary>
/// The bots of one simulation, in the order the engine built them. Trigger times are stored
/// rather than re-drawn: they are the reason two hundred bots do not all move on the same
/// tick, and re-drawing them on restore would both shift the run's random stream and change
/// when every bot acts.
/// </summary>
public sealed record BotsSnapshot(IReadOnlyList<BotSnapshot> Bots);

/// <summary>
/// One bot: the units it trades for, its settings, what it currently thinks an allowance is
/// worth, and the progress it has made through the year. The done-set and bid-set are what
/// stop a bot acting twice for the same year and stop it bidding into a section twice, so a
/// restore without them would have every bot act again on the next tick.
/// </summary>
public sealed record BotSnapshot(
    int CompanyId,
    IReadOnlyList<int> UnitIds,
    BotSettingsSnapshot Settings,
    decimal ExpectedPrice,
    IReadOnlyList<BotTriggerTimeSnapshot> TriggerTimes,
    IReadOnlyList<BotTriggerDoneSnapshot> Done,
    IReadOnlyList<BotSectionSnapshot> BidIn);

/// <summary>One bot's instincts, as the difficulty preset left them.</summary>
public sealed record BotSettingsSnapshot(
    BotDifficulty Difficulty,
    decimal AbatementMargin,
    decimal BidPriceNoise,
    decimal BidVolumeFraction,
    decimal OffsetDiscount,
    decimal ReservationPriceFactor);

/// <summary>When in the year a bot acts for a trigger, as a fraction of the year.</summary>
public sealed record BotTriggerTimeSnapshot(BotTrigger Trigger, decimal AtFractionOfYear);

/// <summary>A trigger a bot has already acted on in a year.</summary>
public sealed record BotTriggerDoneSnapshot(int Year, BotTrigger Trigger);

/// <summary>An auction section a bot has already bid into.</summary>
public sealed record BotSectionSnapshot(int Year, int Section);
