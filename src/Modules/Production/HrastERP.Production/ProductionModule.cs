using FluentValidation;
using HrastERP.Infrastructure.Persistence;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace HrastERP.Production;

public static class ProductionModule
{
    public static IServiceCollection AddProductionModule(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var assembly = typeof(ProductionModule).Assembly;

        // Registers all MediatR command and query handlers defined in this module's assembly
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(assembly));
        // Registers all FluentValidation validators defined in this module's assembly
        services.AddValidatorsFromAssembly(assembly);
        // Registers this module's assembly so HrastDbContext can discover and apply EF Core entity configurations
        services.AddSingleton(new EntityConfigurationAssembly(assembly));

        return services;
    }
}
