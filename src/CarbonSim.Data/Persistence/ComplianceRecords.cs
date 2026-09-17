using CarbonSim.Engine.Market;
using CarbonSim.Engine.Reporting;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CarbonSim.Data.Persistence;

/// <summary>One company's obligation and outcome for one compliance year.</summary>
public sealed class CompanyComplianceRecord
{
    public Guid SimulationId { get; set; }

    public int Year { get; set; }

    public int CompanyId { get; set; }

    /// <summary>What the company's units emitted in the year.</summary>
    public decimal Obligation { get; set; }

    public decimal OffsetsSurrendered { get; set; }

    public decimal AllowancesSurrendered { get; set; }

    public decimal Banked { get; set; }

    /// <summary>Allowances that could not be banked and were taken off the company.</summary>
    public decimal Forfeited { get; set; }

    public decimal Shortfall { get; set; }

    public decimal PenaltyCash { get; set; }

    public decimal PenaltyAllowanceDebit { get; set; }
}

/// <summary>A fine the administrator imposed on a unit.</summary>
public sealed class FineRecord
{
    public Guid SimulationId { get; set; }

    public int FineId { get; set; }

    public int UnitId { get; set; }

    public int CompanyId { get; set; }

    public decimal Amount { get; set; }

    public string Description { get; set; } = string.Empty;

    public int Year { get; set; }
}

/// <summary>
/// A company-year the register has already reconciled. Stored on its own because it is what
/// stops a company-year being reconciled twice, whether or not it produced a result.
/// </summary>
public sealed class ReconciledCompanyYearRecord
{
    public Guid SimulationId { get; set; }

    public int CompanyId { get; set; }

    public int Year { get; set; }
}

/// <summary>
/// One trade the run has seen, on any channel, in the order it happened. A null seller is the
/// government, which is how auction sales are recorded.
/// </summary>
public sealed class JournalTradeRecord
{
    public Guid SimulationId { get; set; }

    public long Sequence { get; set; }

    public int Year { get; set; }

    public TradeChannel Channel { get; set; }

    public ProductKind ProductKind { get; set; }

    public int Vintage { get; set; }

    public decimal Price { get; set; }

    public decimal Volume { get; set; }

    public int BuyerCompanyId { get; set; }

    public int? SellerCompanyId { get; set; }
}

internal sealed class CompanyComplianceRecordConfiguration : IEntityTypeConfiguration<CompanyComplianceRecord>
{
    public void Configure(EntityTypeBuilder<CompanyComplianceRecord> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("CompanyCompliances");
        builder.HasKey(result => new { result.SimulationId, result.Year, result.CompanyId });

        builder
            .HasOne<CompanyRecord>()
            .WithMany()
            .HasForeignKey(result => new { result.SimulationId, result.CompanyId })
            .OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class FineRecordConfiguration : IEntityTypeConfiguration<FineRecord>
{
    public void Configure(EntityTypeBuilder<FineRecord> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("Fines");
        builder.HasKey(fine => new { fine.SimulationId, fine.FineId });
        builder.Property(fine => fine.Description).IsRequired().HasMaxLength(400);

        builder
            .HasOne<UnitRecord>()
            .WithMany()
            .HasForeignKey(fine => new { fine.SimulationId, fine.UnitId })
            .OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class ReconciledCompanyYearRecordConfiguration : IEntityTypeConfiguration<ReconciledCompanyYearRecord>
{
    public void Configure(EntityTypeBuilder<ReconciledCompanyYearRecord> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("ReconciledCompanyYears");
        builder.HasKey(reconciled => new { reconciled.SimulationId, reconciled.CompanyId, reconciled.Year });

        builder
            .HasOne<CompanyRecord>()
            .WithMany()
            .HasForeignKey(reconciled => new { reconciled.SimulationId, reconciled.CompanyId })
            .OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class JournalTradeRecordConfiguration : IEntityTypeConfiguration<JournalTradeRecord>
{
    public void Configure(EntityTypeBuilder<JournalTradeRecord> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("JournalTrades");
        builder.HasKey(trade => new { trade.SimulationId, trade.Sequence });
        builder.Property(trade => trade.Channel).HasConversion<int>();

        builder
            .HasOne<CompanyRecord>()
            .WithMany()
            .HasForeignKey(trade => new { trade.SimulationId, trade.BuyerCompanyId })
            .OnDelete(DeleteBehavior.Cascade);

        builder
            .HasOne<SimulationRecord>()
            .WithMany()
            .HasForeignKey(trade => trade.SimulationId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
