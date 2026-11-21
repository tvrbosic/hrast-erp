namespace HrastERP.API.OpenApi;

public sealed class OpenApiSettings
{
    public const string SectionName = "OpenApi";

    /// <summary>
    /// Serves the OpenAPI document at <c>/openapi/v1.json</c> and the Scalar UI at <c>/scalar/v1</c>.
    /// </summary>
    public bool Enabled { get; init; }
}
