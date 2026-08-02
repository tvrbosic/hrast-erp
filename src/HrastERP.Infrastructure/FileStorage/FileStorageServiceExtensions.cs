using Microsoft.Extensions.DependencyInjection;

namespace HrastERP.Infrastructure.FileStorage;

internal static class FileStorageServiceExtensions
{
    /// <summary>
    /// Registers file storage settings and the file storage service.
    /// Called internally by <see cref="InfrastructureServiceExtensions.AddInfrastructure"/>.
    /// </summary>
    internal static IServiceCollection AddFileStorageServices(this IServiceCollection services)
    {
        services
            .AddOptions<FileStorageSettings>()
            .BindConfiguration(FileStorageSettings.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddScoped<IFileStorageService, LocalFileStorageService>();

        return services;
    }
}
