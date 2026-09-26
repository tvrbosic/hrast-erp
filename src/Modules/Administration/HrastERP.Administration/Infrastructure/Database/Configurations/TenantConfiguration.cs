using HrastERP.Administration.Domain.Entities;
using HrastERP.Infrastructure.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HrastERP.Administration.Infrastructure.Database.Configurations;

internal sealed class TenantConfiguration : IEntityTypeConfiguration<Tenant>
{
    public void Configure(EntityTypeBuilder<Tenant> builder)
    {
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id).ValueGeneratedNever();

        builder.Property(t => t.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.HasIndex(t => t.Name)
            .IsUnique();

        builder.Property(t => t.IsActive)
            .HasDefaultValue(true);

        // FK: ApplicationUser.TenantId -> Tenant.Id
        // Configured here (not in ApplicationUserConfiguration) to avoid circular project reference.
        builder.HasMany<ApplicationUser>()
            .WithOne()
            .HasForeignKey(u => u.TenantId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
