using System.Text.Json;
using HrastERP.API.Responses;
using HrastERP.SharedKernel.Results;
using Microsoft.AspNetCore.Mvc;

namespace HrastERP.API.Extensions;

public static class ModelBindingExtensions
{
    public static IServiceCollection ConfigureModelBindingErrorFormat(this IServiceCollection services)
    {
        services.Configure<ApiBehaviorOptions>(options =>
        {
            // [ApiController] would turn bodiless client errors (e.g. 415 for an unsupported Content-Type) into
            // ProblemDetails; leave them bodiless so StatusCodeErrorResponse writes an ErrorResponse instead
            options.SuppressMapClientErrors = true;

            options.InvalidModelStateResponseFactory = context =>
            {
                var fieldErrors = context.ModelState
                    .Where(e => e.Value?.Errors.Count > 0)
                    .ToDictionary(
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
