using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.OpenApi;
using Scalar.AspNetCore;

namespace HrastERP.API.OpenApi;

/// <summary>
/// Registers OpenAPI document generation and, in Development only, serves the document and the Scalar UI.
/// </summary>
public static class OpenApiServiceExtensions
{
    /// <summary>
    /// Registers the OpenAPI document with the API title, version and a global JWT Bearer security
    /// scheme, so Scalar can send a token with every request.
    /// </summary>
    public static IServiceCollection AddOpenApiServices(this IServiceCollection services)
    {
        services.AddOpenApi(options =>
        {
            options.AddDocumentTransformer((document, _, _) =>
            {
                // Set the title and version shown in Scalar
                document.Info.Title = "HrastERP API";
                document.Info.Version = "v1";

                // Define the "BearerAuth" scheme: a JWT sent as "Authorization: Bearer <token>"
                document.Components ??= new OpenApiComponents();
                document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>(StringComparer.Ordinal);
                document.Components.SecuritySchemes["BearerAuth"] = new OpenApiSecurityScheme
                {
                    Type = SecuritySchemeType.Http,
                    Scheme = JwtBearerDefaults.AuthenticationScheme,
                    BearerFormat = "JWT",
                    Description = "Enter the JWT token obtained from POST /api/auth/login"
                };

                // Require the scheme for every operation, so Scalar's "Auth" token is sent on all requests
                document.Security =
                [
                    new OpenApiSecurityRequirement
                    {
                        // [] = no scopes; scopes only apply to OAuth2/OpenID Connect schemes
                        { new OpenApiSecuritySchemeReference("BearerAuth", document), [] }
                    }
                ];

                return Task.CompletedTask;
            });
        });

        return services;
    }

    /// <summary>
    /// In Development, maps the OpenAPI document at <c>/openapi/v1.json</c> and the Scalar UI at
    /// <c>/scalar/v1</c>. Does nothing in other environments.
    /// </summary>
    public static WebApplication UseOpenApiInfrastructure(this WebApplication app)
    {
        if (!app.Environment.IsDevelopment())
            return app;

        app.MapOpenApi();
        app.MapScalarApiReference();

        return app;
    }
}
