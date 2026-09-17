using CarbonSim.Data.Accounts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CarbonSim.Data.Accounts;

/// <summary>
/// The account table. Both uniqueness rules are enforced by the database rather than by the
/// store, so two players registering at the same moment cannot claim the same address or the
/// same company.
/// </summary>
internal sealed class PlayerAccountConfiguration : IEntityTypeConfiguration<PlayerAccount>
{
    public void Configure(EntityTypeBuilder<PlayerAccount> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("Accounts");
        builder.HasKey(account => account.Id);

        builder.Property(account => account.Email).IsRequired().HasMaxLength(320);
        builder.Property(account => account.DisplayName).IsRequired().HasMaxLength(120);
        builder.Property(account => account.PasswordHash).IsRequired().HasMaxLength(400);
        builder.Property(account => account.Role).HasConversion<int>();
        builder.Property(account => account.CompanyName).HasMaxLength(200);
        builder.Property(account => account.ResetCodeHash).HasMaxLength(400);

        builder.HasIndex(account => account.Email).IsUnique();
        builder
            .HasIndex(account => account.CompanyName)
            .IsUnique()
            .HasFilter("\"CompanyName\" IS NOT NULL");
    }
}
