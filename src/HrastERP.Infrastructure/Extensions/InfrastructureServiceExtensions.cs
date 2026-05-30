using Microsoft.Extensions.DependencyInjection;

namespace HrastERP.Infrastructure.Extensions;

public static class InfrastructureServiceExtensions
{
    /// <summary>
    /// Registers all shared infrastructure services.
    /// This is the single entry point called from Program.cs — it delegates to focused sub-methods.
    /// </summary>
    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        // Register EF Core DbContext, database settings, and persistence interceptors
        services.AddPersistence();

        // Register ASP.NET Core Identity, JWT settings, and authentication services
        services.AddIdentityServices();

        // Register MediatR pipeline behaviors (validation, logging) that apply to all module handlers
        services.AddMediatRPipelineBehaviors();

        return services;
    }
}
