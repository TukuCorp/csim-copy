using CarbonSim.Engine.Bots;
using CarbonSim.Engine.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CarbonSim.Data.Persistence;

/// <summary>
/// The clock's bookkeeping: how far into the year the run had got, and which of the year's
/// notices it had already raised. The through-markers matter as much as the offset, or a
/// restored clock would raise the same auction or year-end notice twice.
/// </summary>
public sealed class ClockRecord
{
    public Guid SimulationId { get; set; }

    public SimulationState State { get; set; }

    public int CurrentYear { get; set; }

    public TimeSpan Elapsed { get; set; }

    public DateTimeOffset? RunningSince { get; set; }

    public DateTimeOffset? HaltAt { get; set; }

    public int OpenNoticedThrough { get; set; }

    public int OpenedThrough { get; set; }

    public int CloseNoticedThrough { get; set; }

    public int ClosedThrough { get; set; }

    public bool HaltedForYearEnd { get; set; }

    public bool PauseAfterAuction { get; set; }

    public TimeSpan AuctionNotice { get; set; }
}

/// <summary>
/// One bot, numbered by the order the engine built it in - the order is part of the state,
/// because it decides which bot acts on a tick first.
/// </summary>
public sealed class BotRecord
{
    public Guid SimulationId { get; set; }

    public int BotId { get; set; }

    public int CompanyId { get; set; }

    /// <summary>What the bot currently thinks an allowance is worth.</summary>
    public decimal ExpectedPrice { get; set; }

    public BotDifficulty Difficulty { get; set; }

    public decimal AbatementMargin { get; set; }

    public decimal BidPriceNoise { get; set; }

    public decimal BidVolumeFraction { get; set; }

    public decimal OffsetDiscount { get; set; }

    public decimal ReservationPriceFactor { get; set; }
}

/// <summary>A unit a bot trades for.</summary>
public sealed class BotUnitRecord
{
    public Guid SimulationId { get; set; }

    public int BotId { get; set; }

    public int UnitId { get; set; }
}

/// <summary>When in the year a bot acts for one trigger, drawn once and stored rather than redrawn.</summary>
public sealed class BotTriggerTimeRecord
{
    public Guid SimulationId { get; set; }

    public int BotId { get; set; }

    public BotTrigger Trigger { get; set; }

    public decimal AtFractionOfYear { get; set; }
}

/// <summary>A trigger a bot has already acted on in a year.</summary>
public sealed class BotTriggerDoneRecord
{
    public Guid SimulationId { get; set; }

    public int BotId { get; set; }

    public int Year { get; set; }

    public BotTrigger Trigger { get; set; }
}

/// <summary>An auction section a bot has already bid into.</summary>
public sealed class BotBidSectionRecord
{
    public Guid SimulationId { get; set; }

    public int BotId { get; set; }

    public int Year { get; set; }

    public int Section { get; set; }
}

internal sealed class ClockRecordConfiguration : IEntityTypeConfiguration<ClockRecord>
{
    public void Configure(EntityTypeBuilder<ClockRecord> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("Clocks");
        builder.HasKey(clock => clock.SimulationId);
        builder.Property(clock => clock.State).HasConversion<int>();

        builder
            .HasOne<SimulationRecord>()
            .WithMany()
            .HasForeignKey(clock => clock.SimulationId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class BotRecordConfiguration : IEntityTypeConfiguration<BotRecord>
{
    public void Configure(EntityTypeBuilder<BotRecord> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("Bots");
        builder.HasKey(bot => new { bot.SimulationId, bot.BotId });

        builder
            .HasOne<CompanyRecord>()
            .WithMany()
            .HasForeignKey(bot => new { bot.SimulationId, bot.CompanyId })
            .OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class BotUnitRecordConfiguration : IEntityTypeConfiguration<BotUnitRecord>
{
    public void Configure(EntityTypeBuilder<BotUnitRecord> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("BotUnits");
        builder.HasKey(unit => new { unit.SimulationId, unit.BotId, unit.UnitId });

        builder
            .HasOne<BotRecord>()
            .WithMany()
            .HasForeignKey(unit => new { unit.SimulationId, unit.BotId })
            .OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class BotTriggerTimeRecordConfiguration : IEntityTypeConfiguration<BotTriggerTimeRecord>
{
    public void Configure(EntityTypeBuilder<BotTriggerTimeRecord> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("BotTriggerTimes");
        builder.HasKey(time => new { time.SimulationId, time.BotId, time.Trigger });

        builder
            .HasOne<BotRecord>()
            .WithMany()
            .HasForeignKey(time => new { time.SimulationId, time.BotId })
            .OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class BotTriggerDoneRecordConfiguration : IEntityTypeConfiguration<BotTriggerDoneRecord>
{
    public void Configure(EntityTypeBuilder<BotTriggerDoneRecord> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("BotTriggerDone");
        builder.HasKey(done => new { done.SimulationId, done.BotId, done.Year, done.Trigger });

        builder
            .HasOne<BotRecord>()
            .WithMany()
            .HasForeignKey(done => new { done.SimulationId, done.BotId })
            .OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class BotBidSectionRecordConfiguration : IEntityTypeConfiguration<BotBidSectionRecord>
{
    public void Configure(EntityTypeBuilder<BotBidSectionRecord> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("BotBidSections");
        builder.HasKey(section => new { section.SimulationId, section.BotId, section.Year, section.Section });

        builder
            .HasOne<BotRecord>()
            .WithMany()
            .HasForeignKey(section => new { section.SimulationId, section.BotId })
            .OnDelete(DeleteBehavior.Cascade);
    }
}
