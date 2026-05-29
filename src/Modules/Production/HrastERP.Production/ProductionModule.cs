using System.Reflection;
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

        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(assembly));
        services.AddValidatorsFromAssembly(assembly);
        services.AddSingleton(new EntityConfigurationAssembly(assembly));

        return services;
    }
}
