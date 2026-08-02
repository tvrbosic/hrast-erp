using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HrastERP.Infrastructure.FileStorage;

internal sealed class StoredFileConfiguration : IEntityTypeConfiguration<StoredFile>
{
    public void Configure(EntityTypeBuilder<StoredFile> builder)
    {
        builder.ToTable("stored_files");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedNever();

        builder.Property(e => e.FileName)
            .IsRequired()
            .HasMaxLength(256);

        builder.Property(e => e.ContentType)
            .IsRequired()
            .HasMaxLength(128);

        builder.Property(e => e.SizeInBytes)
            .IsRequired();

        builder.Property(e => e.StoragePath)
            .IsRequired()
            .HasMaxLength(512);

        builder.Property(e => e.TenantId)
            .IsRequired();

        builder.HasIndex(e => e.TenantId)
            .HasDatabaseName("IX_stored_files_tenant");

        builder.HasIndex(e => e.StoragePath)
            .IsUnique()
            .HasDatabaseName("IX_stored_files_storage_path");
    }
}
