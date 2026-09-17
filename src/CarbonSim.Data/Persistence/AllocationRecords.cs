using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CarbonSim.Data.Persistence;

/// <summary>The cap and the free cake for one year of the run, as the plan computed them.</summary>
public sealed class AllocationYearRecord
{
    public Guid SimulationId { get; set; }

    /// <summary>Virtual year, counting from 1.</summary>
    public int Year { get; set; }

    public decimal Cap { get; set; }

    public decimal FreeAllocation { get; set; }
}

/// <summary>The growth rate drawn for one unit, which is why it is stored rather than redrawn.</summary>
public sealed class UnitAllocationRecord
{
    public Guid SimulationId { get; set; }

    public int UnitId { get; set; }

    public decimal BausGrowth { get; set; }
}

/// <summary>One unit's slice of the free cake and its business-as-usual path, for one year.</summary>
public sealed class UnitAllocationYearRecord
{
    public Guid SimulationId { get; set; }

    public int UnitId { get; set; }

    public int Year { get; set; }

    public decimal FreeAllocation { get; set; }

    public decimal BausEmissions { get; set; }
}

/// <summary>
/// A project a unit has committed to. The option is identified by its position in the unit's
/// menu, because that is the instance the portfolio recognises as implemented.
/// </summary>
public sealed class ImplementedAbatementRecord
{
    public Guid SimulationId { get; set; }

    public int UnitId { get; set; }

    public int OptionIndex { get; set; }

    public string OptionCode { get; set; } = string.Empty;

    public int ImplementedIn { get; set; }

    public int OperatingFromYear { get; set; }

    public int ExpiresAfterYear { get; set; }
}

/// <summary>A year in which a unit was taken out of service.</summary>
public sealed class ShutdownRecord
{
    public Guid SimulationId { get; set; }

    public int UnitId { get; set; }

    public int Year { get; set; }
}

internal sealed class AllocationYearRecordConfiguration : IEntityTypeConfiguration<AllocationYearRecord>
{
    public void Configure(EntityTypeBuilder<AllocationYearRecord> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("AllocationYears");
        builder.HasKey(year => new { year.SimulationId, year.Year });

        builder
            .HasOne<SimulationRecord>()
            .WithMany()
            .HasForeignKey(year => year.SimulationId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class UnitAllocationRecordConfiguration : IEntityTypeConfiguration<UnitAllocationRecord>
{
    public void Configure(EntityTypeBuilder<UnitAllocationRecord> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("UnitAllocations");
        builder.HasKey(allocation => new { allocation.SimulationId, allocation.UnitId });

        builder
            .HasOne<UnitRecord>()
            .WithMany()
            .HasForeignKey(allocation => new { allocation.SimulationId, allocation.UnitId })
            .OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class UnitAllocationYearRecordConfiguration : IEntityTypeConfiguration<UnitAllocationYearRecord>
{
    public void Configure(EntityTypeBuilder<UnitAllocationYearRecord> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("UnitAllocationYears");
        builder.HasKey(year => new { year.SimulationId, year.UnitId, year.Year });

        builder
            .HasOne<UnitAllocationRecord>()
            .WithMany()
            .HasForeignKey(year => new { year.SimulationId, year.UnitId })
            .OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class ImplementedAbatementRecordConfiguration : IEntityTypeConfiguration<ImplementedAbatementRecord>
{
    public void Configure(EntityTypeBuilder<ImplementedAbatementRecord> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("ImplementedAbatements");
        builder.HasKey(project => new { project.SimulationId, project.UnitId, project.OptionIndex });
        builder.Property(project => project.OptionCode).IsRequired().HasMaxLength(60);

        builder
            .HasOne<UnitRecord>()
            .WithMany()
            .HasForeignKey(project => new { project.SimulationId, project.UnitId })
            .OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class ShutdownRecordConfiguration : IEntityTypeConfiguration<ShutdownRecord>
{
    public void Configure(EntityTypeBuilder<ShutdownRecord> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("UnitShutdowns");
        builder.HasKey(shutdown => new { shutdown.SimulationId, shutdown.UnitId, shutdown.Year });

        builder
            .HasOne<UnitRecord>()
            .WithMany()
            .HasForeignKey(shutdown => new { shutdown.SimulationId, shutdown.UnitId })
            .OnDelete(DeleteBehavior.Cascade);
    }
}
