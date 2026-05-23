using System.Reflection;
using HrastERP.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace HrastERP.Administration.Infrastructure.Extensions;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers all Administration module infrastructure services.
    /// Invoked from <c>Program.cs</c> as part of the application composition root.
    /// </summary>
    public static IServiceCollection AddAdministrationInfrastructure(
        this IServiceCollection services)
    {
        // Registers this assembly so HrastDbContext auto-discovers all IEntityTypeConfiguration<T> classes in it.
        // This removes the need to manually register each entity configuration here or add DbSet<T> properties to HrastDbContext.
        services.AddSingleton(new EntityConfigurationAssembly(Assembly.GetExecutingAssembly()));

        return services;
    }
}
