using HrastERP.Infrastructure.Authentication;
using HrastERP.Infrastructure.Behaviors;
using HrastERP.Infrastructure.Hangfire;
using HrastERP.Infrastructure.Database;
using Microsoft.Extensions.DependencyInjection;

namespace HrastERP.Infrastructure;

public static class InfrastructureServiceExtensions
{
    /// <summary>
    /// Registers all shared infrastructure services.
    /// This is the single entry point called from Program.cs — it delegates to focused sub-methods.
    /// </summary>
    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        // Register EF Core DbContext, database settings, and interceptors
        services.AddDatabase();

        // Register ASP.NET Core Identity, JWT settings, and authentication services
        services.AddIdentityServices();

        // Register MediatR pipeline behaviors (validation, logging) that apply to all module handlers
        services.AddMediatRPipelineBehaviors();

        // Register Hangfire background job infrastructure and cleanup jobs
        services.AddBackgroundJobs();

        return services;
    }
}
