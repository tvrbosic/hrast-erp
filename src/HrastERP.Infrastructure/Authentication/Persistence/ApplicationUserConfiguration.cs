using HrastERP.Infrastructure.Database.Audit;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HrastERP.Infrastructure.Authentication;

public sealed class ApplicationUserConfiguration : IEntityTypeConfiguration<ApplicationUser>
{
    public void Configure(EntityTypeBuilder<ApplicationUser> builder)
    {
        builder.ToTable("asp_net_users");

        builder.Property(u => u.TenantId)
            .IsRequired();

        builder.HasIndex(u => u.TenantId);

        // Identity names these indexes explicitly ("EmailIndex", "UserNameIndex") and the naming
        // convention doesn't rewrite explicit names, so set them in the convention's format.
        builder.HasIndex(u => u.NormalizedEmail).HasDatabaseName("ix_asp_net_users_normalized_email");
        builder.HasIndex(u => u.NormalizedUserName).HasDatabaseName("ix_asp_net_users_normalized_user_name");

        builder.Property(u => u.FirstName)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(u => u.LastName)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(u => u.IsActive)
            .HasDefaultValue(true);

        // Credential material must never be copied into the audit log.
        builder.Property(u => u.PasswordHash).ExcludeFromAudit();
        builder.Property(u => u.SecurityStamp).ExcludeFromAudit();
        builder.Property(u => u.ConcurrencyStamp).ExcludeFromAudit();

        // Defaults let raw SQL inserts (the dev admin fixture seed) and pre-existing rows
        // satisfy the non-null audit columns; EF inserts get real values from the interceptor.
        builder.Property(u => u.CreatedAt)
            .HasDefaultValueSql("now()");

        builder.Property(u => u.CreatedBy)
            .HasDefaultValue(Guid.Empty);

        builder.HasMany(u => u.RefreshTokens)
            .WithOne(rt => rt.User)
            .HasForeignKey(rt => rt.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
