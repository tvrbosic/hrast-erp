using HrastERP.Infrastructure.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HrastERP.Infrastructure.Authorization.Persistence;

internal sealed class RoleConfiguration : IEntityTypeConfiguration<Role>
{
    public void Configure(EntityTypeBuilder<Role> builder)
    {
        builder.ToTable("roles");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Name).HasMaxLength(100).IsRequired();

        // Trimmed, upper-invariant copy of Name (same idea as Identity's NormalizedUserName).
        // The unique index enforces case-insensitive name uniqueness even under concurrent writes.
        builder.Property(r => r.NormalizedName).HasMaxLength(100).IsRequired();
        builder.HasIndex(r => r.NormalizedName).IsUnique();
        builder.Property(r => r.Description).HasMaxLength(500);
        builder.Property(r => r.Permissions).HasConversion<long>();

        builder.HasMany(r => r.Users)
               .WithOne(u => u.Role)
               .HasForeignKey(u => u.RoleId)
               .OnDelete(DeleteBehavior.Restrict);
    }
}
