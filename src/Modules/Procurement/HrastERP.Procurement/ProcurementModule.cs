using System.Reflection;
using FluentValidation;
using HrastERP.Infrastructure.Persistence;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace HrastERP.Procurement;

public static class ProcurementModule
{
    public static IServiceCollection AddProcurementModule(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var assembly = typeof(ProcurementModule).Assembly;

        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(assembly));
        services.AddValidatorsFromAssembly(assembly);
        services.AddSingleton(new EntityConfigurationAssembly(assembly));

        return services;
    }
}
