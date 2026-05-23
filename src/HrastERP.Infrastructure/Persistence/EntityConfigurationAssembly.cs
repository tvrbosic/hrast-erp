using System.Reflection;

namespace HrastERP.Infrastructure.Persistence;

/// <summary>
/// Carries a module's assembly reference so <see cref="HrastDbContext"/> can scan it for
/// <see cref="Microsoft.EntityFrameworkCore.IEntityTypeConfiguration{TEntity}"/> implementations.
/// Each module registers one instance via <c>services.AddSingleton(new EntityConfigurationAssembly(Assembly.GetExecutingAssembly()))</c>
/// in its <c>Add&lt;Module&gt;Infrastructure</c> extension method.
/// </summary>
public sealed class EntityConfigurationAssembly(Assembly assembly)
{
    public Assembly Assembly { get; } = assembly;
}
