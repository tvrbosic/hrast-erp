using HrastERP.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HrastERP.Infrastructure.Identity;

public sealed class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.ToTable("refresh_tokens", DatabaseSchemas.Administration);
        builder.HasKey(rt => rt.Id);

        builder.Property(rt => rt.TenantId)
            .IsRequired();

        builder.HasIndex(rt => rt.TenantId);

        // Holds the SHA-256 hex hash (64 characters), never the raw token
        builder.Property(rt => rt.Token)
            .IsRequired()
            .HasMaxLength(64);

        builder.HasIndex(rt => rt.Token)
            .IsUnique();

        builder.HasIndex(rt => rt.UserId);
    }
}
