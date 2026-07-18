using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HrastERP.Infrastructure.Persistence.Audit;

internal sealed class AuditLogConfiguration : IEntityTypeConfiguration<AuditLogEntry>
{
    public void Configure(EntityTypeBuilder<AuditLogEntry> builder)
    {
        builder.ToTable("audit_log");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedNever();

        builder.Property(e => e.EntityName)
            .HasMaxLength(256)
            .IsRequired();

        builder.Property(e => e.EntityId)
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(e => e.Action)
            .HasConversion<string>()
            .HasMaxLength(16)
            .IsRequired();

        builder.Property(e => e.OldValues)
            .HasColumnType("text");

        builder.Property(e => e.NewValues)
            .HasColumnType("text");

        builder.Property(e => e.UserId).IsRequired();
        builder.Property(e => e.TenantId).IsRequired();
        builder.Property(e => e.Timestamp).IsRequired();

        builder.HasIndex(e => new { e.EntityName, e.EntityId })
            .HasDatabaseName("IX_audit_log_entity");

        builder.HasIndex(e => e.UserId)
            .HasDatabaseName("IX_audit_log_user");

        builder.HasIndex(e => e.Timestamp)
            .HasDatabaseName("IX_audit_log_timestamp");
    }
}
