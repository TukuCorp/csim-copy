using CarbonSim.Engine.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CarbonSim.Data.Persistence;

/// <summary>A player in the saved run: a person at a keyboard, or the rule-based owner of an AI company.</summary>
public sealed class PlayerRecord
{
    public Guid SimulationId { get; set; }

    public int PlayerId { get; set; }

    public string Name { get; set; } = string.Empty;

    public PlayerKind Kind { get; set; }
}

/// <summary>A company that has to comply, and the books it keeps.</summary>
public sealed class CompanyRecord
{
    public Guid SimulationId { get; set; }

    public int CompanyId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Sector { get; set; } = string.Empty;

    public int OwnerPlayerId { get; set; }

    public decimal Capital { get; set; }

    /// <summary>Cash promised to resting orders and uncleared auction bids.</summary>
    public decimal EscrowedCash { get; set; }

    public decimal OverdraftLimit { get; set; }
}

internal sealed class PlayerRecordConfiguration : IEntityTypeConfiguration<PlayerRecord>
{
    public void Configure(EntityTypeBuilder<PlayerRecord> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("Players");
        builder.HasKey(player => new { player.SimulationId, player.PlayerId });
        builder.Property(player => player.Name).IsRequired().HasMaxLength(200);
        builder.Property(player => player.Kind).HasConversion<int>();

        builder
            .HasOne<SimulationRecord>()
            .WithMany()
            .HasForeignKey(player => player.SimulationId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class CompanyRecordConfiguration : IEntityTypeConfiguration<CompanyRecord>
{
    public void Configure(EntityTypeBuilder<CompanyRecord> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("Companies");
        builder.HasKey(company => new { company.SimulationId, company.CompanyId });
        builder.Property(company => company.Name).IsRequired().HasMaxLength(200);
        builder.Property(company => company.Sector).IsRequired().HasMaxLength(120);

        builder
            .HasOne<SimulationRecord>()
            .WithMany()
            .HasForeignKey(company => company.SimulationId)
            .OnDelete(DeleteBehavior.Cascade);

        builder
            .HasOne<PlayerRecord>()
            .WithMany()
            .HasForeignKey(company => new { company.SimulationId, company.OwnerPlayerId })
            .OnDelete(DeleteBehavior.Cascade);

        builder
            .HasOne<SectorRecord>()
            .WithMany()
            .HasForeignKey(company => new { company.SimulationId, company.Sector })
            .OnDelete(DeleteBehavior.Cascade);
    }
}
