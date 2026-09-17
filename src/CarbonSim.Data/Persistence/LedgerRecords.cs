using CarbonSim.Engine.Finance;
using CarbonSim.Engine.Market;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CarbonSim.Data.Persistence;

/// <summary>
/// What one company holds of one product and can spend. The engine keeps a key for every
/// company and product it has ever touched, even when the balance it leaves is zero, so a row
/// here means "this key exists" rather than "this key is worth something". That is why the two
/// buckets are separate tables: merging them would lose the difference between a key that was
/// never granted and one that was granted and spent.
/// </summary>
public sealed class LedgerAvailableRecord
{
    public Guid SimulationId { get; set; }

    public int CompanyId { get; set; }

    public ProductKind ProductKind { get; set; }

    /// <summary>Zero for offsets, which carry no vintage.</summary>
    public int Vintage { get; set; }

    public decimal Volume { get; set; }
}

/// <summary>What one company holds of one product and has already promised elsewhere.</summary>
public sealed class LedgerEscrowedRecord
{
    public Guid SimulationId { get; set; }

    public int CompanyId { get; set; }

    public ProductKind ProductKind { get; set; }

    public int Vintage { get; set; }

    public decimal Volume { get; set; }
}

/// <summary>A year whose free allocation has already been granted, so it is never granted twice.</summary>
public sealed class LedgerGrantedYearRecord
{
    public Guid SimulationId { get; set; }

    public int Year { get; set; }
}

/// <summary>The government's side of the run: what it raised, and whether the cap was issued.</summary>
public sealed class GovernmentRecord
{
    public Guid SimulationId { get; set; }

    public decimal Revenue { get; set; }

    public bool HasIssued { get; set; }
}

/// <summary>Allowances of one vintage the government still holds, which is what the auctions draw on.</summary>
public sealed class GovernmentReserveRecord
{
    public Guid SimulationId { get; set; }

    public int Vintage { get; set; }

    public decimal Volume { get; set; }
}

/// <summary>Allowances of one vintage the cap issued to the government in the first place.</summary>
public sealed class GovernmentIssuedRecord
{
    public Guid SimulationId { get; set; }

    public int Vintage { get; set; }

    public decimal Volume { get; set; }
}

/// <summary>One movement of money, signed, in the order it happened.</summary>
public sealed class CashMovementRecord
{
    public Guid SimulationId { get; set; }

    public long Sequence { get; set; }

    public int Year { get; set; }

    public int CompanyId { get; set; }

    public CashCategory Category { get; set; }

    public decimal Amount { get; set; }

    public string Description { get; set; } = string.Empty;
}

internal sealed class LedgerAvailableRecordConfiguration : IEntityTypeConfiguration<LedgerAvailableRecord>
{
    public void Configure(EntityTypeBuilder<LedgerAvailableRecord> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("LedgerAvailable");
        builder.HasKey(balance => new { balance.SimulationId, balance.CompanyId, balance.ProductKind, balance.Vintage });
        builder.Property(balance => balance.ProductKind).HasConversion<int>();

        builder
            .HasOne<CompanyRecord>()
            .WithMany()
            .HasForeignKey(balance => new { balance.SimulationId, balance.CompanyId })
            .OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class LedgerEscrowedRecordConfiguration : IEntityTypeConfiguration<LedgerEscrowedRecord>
{
    public void Configure(EntityTypeBuilder<LedgerEscrowedRecord> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("LedgerEscrowed");
        builder.HasKey(balance => new { balance.SimulationId, balance.CompanyId, balance.ProductKind, balance.Vintage });
        builder.Property(balance => balance.ProductKind).HasConversion<int>();

        builder
            .HasOne<CompanyRecord>()
            .WithMany()
            .HasForeignKey(balance => new { balance.SimulationId, balance.CompanyId })
            .OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class LedgerGrantedYearRecordConfiguration : IEntityTypeConfiguration<LedgerGrantedYearRecord>
{
    public void Configure(EntityTypeBuilder<LedgerGrantedYearRecord> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("LedgerGrantedYears");
        builder.HasKey(year => new { year.SimulationId, year.Year });

        builder
            .HasOne<SimulationRecord>()
            .WithMany()
            .HasForeignKey(year => year.SimulationId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class GovernmentRecordConfiguration : IEntityTypeConfiguration<GovernmentRecord>
{
    public void Configure(EntityTypeBuilder<GovernmentRecord> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("Government");
        builder.HasKey(government => government.SimulationId);

        builder
            .HasOne<SimulationRecord>()
            .WithMany()
            .HasForeignKey(government => government.SimulationId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class GovernmentReserveRecordConfiguration : IEntityTypeConfiguration<GovernmentReserveRecord>
{
    public void Configure(EntityTypeBuilder<GovernmentReserveRecord> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("GovernmentReserves");
        builder.HasKey(reserve => new { reserve.SimulationId, reserve.Vintage });

        builder
            .HasOne<GovernmentRecord>()
            .WithMany()
            .HasForeignKey(reserve => reserve.SimulationId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class GovernmentIssuedRecordConfiguration : IEntityTypeConfiguration<GovernmentIssuedRecord>
{
    public void Configure(EntityTypeBuilder<GovernmentIssuedRecord> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("GovernmentIssued");
        builder.HasKey(issued => new { issued.SimulationId, issued.Vintage });

        builder
            .HasOne<GovernmentRecord>()
            .WithMany()
            .HasForeignKey(issued => issued.SimulationId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class CashMovementRecordConfiguration : IEntityTypeConfiguration<CashMovementRecord>
{
    public void Configure(EntityTypeBuilder<CashMovementRecord> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("CashMovements");
        builder.HasKey(movement => new { movement.SimulationId, movement.Sequence });
        builder.Property(movement => movement.Category).HasConversion<int>();
        builder.Property(movement => movement.Description).IsRequired().HasMaxLength(400);

        builder
            .HasOne<SimulationRecord>()
            .WithMany()
            .HasForeignKey(movement => movement.SimulationId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
