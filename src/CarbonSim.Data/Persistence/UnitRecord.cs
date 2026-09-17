using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CarbonSim.Data.Persistence;

/// <summary>An emitting facility: what it emits, what it may buy, and whether a bot runs it.</summary>
public sealed class UnitRecord
{
    public Guid SimulationId { get; set; }

    public int UnitId { get; set; }

    public string Name { get; set; } = string.Empty;

    public int CompanyId { get; set; }

    public decimal BaselineEmissions { get; set; }

    public decimal NormalOperatingProfit { get; set; }

    /// <summary>Whether the trainer has handed this unit to a bot.</summary>
    public bool AutoTrade { get; set; }
}

/// <summary>One project on a unit's own menu, as it was when the run was saved.</summary>
public sealed class AbatementOptionRecord
{
    public Guid SimulationId { get; set; }

    public int UnitId { get; set; }

    public string Code { get; set; } = string.Empty;

    /// <summary>
    /// Position in the unit's menu. An implemented project is identified by this position, so
    /// the order is part of the state rather than a display detail.
    /// </summary>
    public int Ordinal { get; set; }

    public string Name { get; set; } = string.Empty;

    /// <summary>Cost per tonne of the reduction the project brings.</summary>
    public decimal UpfrontCost { get; set; }

    public decimal AnnualReduction { get; set; }

    public int ImplementationYears { get; set; }

    public int LifetimeYears { get; set; }

    public decimal AnnualNetRevenue { get; set; }
}

internal sealed class UnitRecordConfiguration : IEntityTypeConfiguration<UnitRecord>
{
    public void Configure(EntityTypeBuilder<UnitRecord> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("Units");
        builder.HasKey(unit => new { unit.SimulationId, unit.UnitId });
        builder.Property(unit => unit.Name).IsRequired().HasMaxLength(200);

        builder
            .HasOne<CompanyRecord>()
            .WithMany()
            .HasForeignKey(unit => new { unit.SimulationId, unit.CompanyId })
            .OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class AbatementOptionRecordConfiguration : IEntityTypeConfiguration<AbatementOptionRecord>
{
    public void Configure(EntityTypeBuilder<AbatementOptionRecord> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("AbatementOptions");
        builder.HasKey(option => new { option.SimulationId, option.UnitId, option.Code });
        builder.Property(option => option.Code).HasMaxLength(60);
        builder.Property(option => option.Name).IsRequired().HasMaxLength(200);

        builder
            .HasOne<UnitRecord>()
            .WithMany()
            .HasForeignKey(option => new { option.SimulationId, option.UnitId })
            .OnDelete(DeleteBehavior.Cascade);
    }
}
