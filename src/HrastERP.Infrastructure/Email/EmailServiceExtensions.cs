using Microsoft.Extensions.DependencyInjection;

namespace HrastERP.Infrastructure.Email;

internal static class EmailServiceExtensions
{
    /// <summary>
    /// Registers SMTP settings and the email service.
    /// Called internally by <see cref="InfrastructureServiceExtensions.AddInfrastructure"/>.
    /// </summary>
    internal static IServiceCollection AddEmailServices(this IServiceCollection services)
    {
        services
            .AddOptions<SmtpSettings>()
            .BindConfiguration(SmtpSettings.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddScoped<IEmailService, MailKitEmailService>();

        return services;
    }
}
