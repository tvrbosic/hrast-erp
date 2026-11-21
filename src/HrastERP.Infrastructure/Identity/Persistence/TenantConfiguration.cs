using HrastERP.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HrastERP.Infrastructure.Identity;

internal sealed class TenantConfiguration : IEntityTypeConfiguration<Tenant>
{
    public void Configure(EntityTypeBuilder<Tenant> builder)
    {
        builder.ToTable("tenants", DatabaseSchemas.Administration);
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id).ValueGeneratedNever();

        builder.Property(t => t.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(t => t.NormalizedName)
            .IsRequired()
            .HasMaxLength(200);

        builder.HasIndex(t => t.NormalizedName)
            .IsUnique();

        builder.Property(t => t.IsActive)
            .HasDefaultValue(true);

        // FK: ApplicationUser.TenantId -> Tenant.Id
        builder.HasMany<ApplicationUser>()
            .WithOne()
            .HasForeignKey(u => u.TenantId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
