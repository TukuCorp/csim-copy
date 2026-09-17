using CarbonSim.Engine.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CarbonSim.Data.Persistence;

/// <summary>
/// A saved run: one row per simulation, with everything else hanging off it under a cascading
/// delete. Saving is therefore "replace the graph", which is what a trainer's save does anyway.
/// </summary>
public sealed class SimulationRecord
{
    public Guid SimulationId { get; set; }

    public string Name { get; set; } = string.Empty;

    public ulong Seed { get; set; }

    /// <summary>
    /// Where the single random stream had got to. Restoring it is what makes a reloaded run
    /// continue with the same draws the original would have made.
    /// </summary>
    public ulong RandomState { get; set; }

    public SimulationState State { get; set; }

    public int CurrentYear { get; set; }

    public TradingSystemRecord? TradingSystem { get; set; }
}

/// <summary>One emissions trading system inside a saved run, with the parameters it ran under.</summary>
public sealed class TradingSystemRecord
{
    public Guid SimulationId { get; set; }

    public int TradingSystemId { get; set; }

    public string Name { get; set; } = string.Empty;

    public ParametersRecord? Parameters { get; set; }
}

/// <summary>The rules an administrator ran the trading system under; one row per trading system.</summary>
public sealed class ParametersRecord
{
    public Guid SimulationId { get; set; }

    public int TradingSystemId { get; set; }

    public decimal Cap { get; set; }

    public decimal AnnualCapReductionRate { get; set; }

    public decimal FreeAllocationShare { get; set; }

    public int Years { get; set; }

    public decimal OffsetUsageLimit { get; set; }

    public decimal BankingLimit { get; set; }

    public decimal PenaltyPerTonne { get; set; }

    public decimal PenaltyAllowanceDebit { get; set; }

    public decimal AuctionFloorPrice { get; set; }

    public decimal AuctionCeilingPrice { get; set; }

    public int AuctionsPerYear { get; set; }

    public TimeSpan YearLength { get; set; }

    public TimeSpan AuctionDuration { get; set; }

    public decimal TradingOpenShareOfYear { get; set; }

    public decimal VolatilityBand { get; set; }

    public decimal OverdraftInterestRate { get; set; }
}

/// <summary>The business-as-usual growth band a sector's units are drawn from.</summary>
public sealed class BausGrowthRecord
{
    public Guid SimulationId { get; set; }

    public int TradingSystemId { get; set; }

    public int BausGrowthId { get; set; }

    public string Sector { get; set; } = string.Empty;

    public decimal MinAnnualRate { get; set; }

    public decimal MaxAnnualRate { get; set; }
}

internal sealed class SimulationRecordConfiguration : IEntityTypeConfiguration<SimulationRecord>
{
    public void Configure(EntityTypeBuilder<SimulationRecord> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("Simulations");
        builder.HasKey(simulation => simulation.SimulationId);
        builder.Property(simulation => simulation.Name).IsRequired().HasMaxLength(200);

        // SQLite has no unsigned integers, so the seed and the stream position are stored as the
        // signed 64 bits of the same value rather than losing the top half of the range.
        builder.Property(simulation => simulation.Seed).HasConversion(value => ToSigned(value), value => ToUnsigned(value));
        builder.Property(simulation => simulation.RandomState).HasConversion(value => ToSigned(value), value => ToUnsigned(value));
        builder.Property(simulation => simulation.State).HasConversion<int>();

        builder
            .HasOne(simulation => simulation.TradingSystem)
            .WithOne()
            .HasForeignKey<TradingSystemRecord>(system => system.SimulationId)
            .OnDelete(DeleteBehavior.Cascade);
    }

    private static long ToSigned(ulong value) => unchecked((long)value);

    private static ulong ToUnsigned(long value) => unchecked((ulong)value);
}

internal sealed class TradingSystemRecordConfiguration : IEntityTypeConfiguration<TradingSystemRecord>
{
    public void Configure(EntityTypeBuilder<TradingSystemRecord> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("TradingSystems");
        builder.HasKey(system => new { system.SimulationId, system.TradingSystemId });
        builder.Property(system => system.Name).IsRequired().HasMaxLength(200);

        builder
            .HasOne(system => system.Parameters)
            .WithOne()
            .HasForeignKey<ParametersRecord>(parameters => new { parameters.SimulationId, parameters.TradingSystemId })
            .OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class ParametersRecordConfiguration : IEntityTypeConfiguration<ParametersRecord>
{
    public void Configure(EntityTypeBuilder<ParametersRecord> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("Parameters");
        builder.HasKey(parameters => new { parameters.SimulationId, parameters.TradingSystemId });
    }
}

internal sealed class BausGrowthRecordConfiguration : IEntityTypeConfiguration<BausGrowthRecord>
{
    public void Configure(EntityTypeBuilder<BausGrowthRecord> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("BausGrowthBands");
        builder.HasKey(growth => new { growth.SimulationId, growth.BausGrowthId });
        builder.Property(growth => growth.Sector).IsRequired().HasMaxLength(120);

        builder
            .HasOne<ParametersRecord>()
            .WithMany()
            .HasForeignKey(growth => new { growth.SimulationId, growth.TradingSystemId })
            .OnDelete(DeleteBehavior.Cascade);
    }
}
