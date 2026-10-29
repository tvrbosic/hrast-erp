using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace HrastERP.Infrastructure.Database;

/// <summary>
/// Identifies database constraint violations behind a <see cref="DbUpdateException"/>, so handlers can
/// map races that slip past their pre-checks to the same <c>Result</c> errors instead of a 500.
/// Keeps Npgsql types out of module code.
/// </summary>
public static class DbUpdateExceptionExtensions
{
    public static bool IsUniqueViolation(this DbUpdateException exception) =>
        exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation };

    public static bool IsForeignKeyViolation(this DbUpdateException exception) =>
        exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.ForeignKeyViolation };
}
