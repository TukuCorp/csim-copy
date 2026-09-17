using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CarbonSim.Data.Persistence;

/// <summary>A sector of the trading system: its share of the cap and the projects its units may buy.</summary>
public sealed class SectorRecord
{
    public Guid SimulationId { get; set; }

    public string Name { get; set; } = string.Empty;

    public decimal EmissionShare { get; set; }
}

/// <summary>One abatement project on a sector's menu, with its cost and effect per tonne.</summary>
public sealed class AbatementMenuRecord
{
    public Guid SimulationId { get; set; }

    public string Sector { get; set; } = string.Empty;

    public string Code { get; set; } = string.Empty;

    /// <summary>Position on the sector's menu; the order is what a unit's own menu is built from.</summary>
    public int Ordinal { get; set; }

    public string Name { get; set; } = string.Empty;

    /// <summary>Share of the unit's baseline emissions the project removes each year.</summary>
    public decimal AnnualReductionShare { get; set; }

    public decimal UpfrontCostPerTonne { get; set; }

    public decimal AnnualNetRevenuePerTonne { get; set; }

    public int ImplementationYears { get; set; }

    public int LifetimeYears { get; set; }
}

internal sealed class SectorRecordConfiguration : IEntityTypeConfiguration<SectorRecord>
{
    public void Configure(EntityTypeBuilder<SectorRecord> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("Sectors");
        builder.HasKey(sector => new { sector.SimulationId, sector.Name });
        builder.Property(sector => sector.Name).HasMaxLength(120);

        builder
            .HasOne<SimulationRecord>()
            .WithMany()
            .HasForeignKey(sector => sector.SimulationId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class AbatementMenuRecordConfiguration : IEntityTypeConfiguration<AbatementMenuRecord>
{
    public void Configure(EntityTypeBuilder<AbatementMenuRecord> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("AbatementMenu");
        builder.HasKey(menu => new { menu.SimulationId, menu.Sector, menu.Code });
        builder.Property(menu => menu.Sector).HasMaxLength(120);
        builder.Property(menu => menu.Code).HasMaxLength(60);
        builder.Property(menu => menu.Name).IsRequired().HasMaxLength(200);

        builder
            .HasOne<SectorRecord>()
            .WithMany()
            .HasForeignKey(menu => new { menu.SimulationId, menu.Sector })
            .OnDelete(DeleteBehavior.Cascade);
    }
}
