using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HrastERP.Infrastructure.Database.Audit;

/// <summary>
/// Marks properties whose values must never be written to the audit log (e.g. password hashes).
/// <see cref="AuditLogInterceptor"/> skips them, and cleanup jobs that write audit entries read the
/// same annotation through <see cref="IsExcludedFromAudit"/>.
/// </summary>
public static class AuditExclusion
{
    public const string AnnotationName = "HrastERP:ExcludeFromAudit";

    public static PropertyBuilder<TProperty> ExcludeFromAudit<TProperty>(this PropertyBuilder<TProperty> builder)
    {
        builder.HasAnnotation(AnnotationName, true);
        return builder;
    }

    public static bool IsExcludedFromAudit(this IReadOnlyProperty property)
    {
        return property.FindAnnotation(AnnotationName)?.Value is true;
    }
}
