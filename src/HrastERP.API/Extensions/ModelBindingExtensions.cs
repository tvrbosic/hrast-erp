using System.Text.Json;
using HrastERP.API.Models;
using Microsoft.AspNetCore.Mvc;

namespace HrastERP.API.Extensions;

public static class ModelBindingExtensions
{
    public static IServiceCollection ConfigureModelBindingErrorFormat(this IServiceCollection services)
    {
        services.Configure<ApiBehaviorOptions>(options =>
        {
            options.InvalidModelStateResponseFactory = context =>
            {
                var fieldErrors = context.ModelState
                    .Where(e => e.Value?.Errors.Count > 0)
                    .ToDictionary(
                        e => JsonNamingPolicy.CamelCase.ConvertName(e.Key),
                        e => e.Value!.Errors.Select(err => err.ErrorMessage).ToArray());

                var body = new ErrorResponse("General.Validation", "One or more validation errors occurred.")
                {
                    Errors = fieldErrors
                };

                return new UnprocessableEntityObjectResult(body);
            };
        });

        return services;
    }
}
