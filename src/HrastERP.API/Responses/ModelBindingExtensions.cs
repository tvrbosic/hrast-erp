using System.Text.Json;
using HrastERP.SharedKernel.Results;
using Microsoft.AspNetCore.Mvc;

namespace HrastERP.API.Responses;

/// <summary>
/// Makes the framework's own client errors use the API's flat <see cref="ErrorResponse"/> shape
/// instead of ProblemDetails.
/// </summary>
public static class ModelBindingExtensions
{
    /// <summary>
    /// Returns model binding failures (malformed JSON, wrong types, missing <c>[Required]</c> fields)
    /// as 422 <c>General.Validation</c>, the same response <c>ValidationBehavior</c> gives, and leaves
    /// other bodiless client errors for <c>StatusCodeErrorResponse</c>.
    /// </summary>
    public static IServiceCollection ConfigureModelBindingErrorFormat(this IServiceCollection services)
    {
        services.Configure<ApiBehaviorOptions>(options =>
        {
            // [ApiController] would turn bodiless client errors (e.g. 415 for an unsupported Content-Type) into
            // ProblemDetails; leave them bodiless so StatusCodeErrorResponse writes an ErrorResponse instead
            options.SuppressMapClientErrors = true;

            // Runs when model binding fails (malformed JSON, wrong type, missing [Required] field) before the
            // controller action is reached, so MediatR's ValidationBehavior never runs. Build the same 422
            // General.Validation ErrorResponse it would return, instead of ValidationProblemDetails
            options.InvalidModelStateResponseFactory = context =>
            {
                var fieldErrors = context.ModelState
                    .Where(e => e.Value?.Errors.Count > 0)
                    .ToDictionary(
                        // camelCase keys ("FirstName" -> "firstName") to match ValidationBehavior's output
                        e => JsonNamingPolicy.CamelCase.ConvertName(e.Key),
                        e => e.Value!.Errors.Select(err => err.ErrorMessage).ToArray());

                var error = GeneralErrors.Validation(fieldErrors);
                var body = new ErrorResponse(error.Code, error.Message)
                {
                    Errors = error.ValidationErrors
                };

                return new UnprocessableEntityObjectResult(body);
            };
        });

        return services;
    }
}
