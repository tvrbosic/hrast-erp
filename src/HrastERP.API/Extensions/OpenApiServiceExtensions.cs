using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.OpenApi;
using Scalar.AspNetCore;

namespace HrastERP.API.Extensions;

public static class OpenApiServiceExtensions
{
    public static IServiceCollection AddOpenApiServices(this IServiceCollection services)
    {
        services.AddOpenApi(options =>
        {
            options.AddDocumentTransformer((document, _, _) =>
            {
                document.Info.Title = "HrastERP API";
                document.Info.Version = "v1";

                document.Components ??= new OpenApiComponents();
                document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>(StringComparer.Ordinal);
                document.Components.SecuritySchemes["BearerAuth"] = new OpenApiSecurityScheme
                {
                    Type = SecuritySchemeType.Http,
                    Scheme = JwtBearerDefaults.AuthenticationScheme,
                    BearerFormat = "JWT",
                    Description = "Enter the JWT token obtained from POST /api/auth/login"
                };

                document.Security =
                [
                    new OpenApiSecurityRequirement
                    {
                        { new OpenApiSecuritySchemeReference("BearerAuth", document), [] }
                    }
                ];

                return Task.CompletedTask;
            });
        });

        return services;
    }

    public static WebApplication UseOpenApiInfrastructure(this WebApplication app)
    {
        if (!app.Environment.IsDevelopment())
            return app;

        app.MapOpenApi();
        app.MapScalarApiReference();

        return app;
    }
}
