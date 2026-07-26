using FluentValidation;
using HrastERP.Infrastructure.Database;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace HrastERP.Administration;

public static class AdministrationModule
{
    // Service extension method — called in Program.cs as builder.Services.AddAdministrationModule(...)
    public static IServiceCollection AddAdministrationModule(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var assembly = typeof(AdministrationModule).Assembly;

        // Registers all MediatR command and query handlers defined in this module's assembly
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(assembly));
        // Registers all FluentValidation validators defined in this module's assembly
        services.AddValidatorsFromAssembly(assembly);
        // Registers this module's assembly so HrastDbContext can discover and apply EF Core entity configurations
        services.AddSingleton(new EntityConfigurationAssembly(assembly));

        return services;
    }
}
