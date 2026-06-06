using HrastERP.API.Models;
using HrastERP.SharedKernel.Results;
using Microsoft.AspNetCore.Mvc;

namespace HrastERP.API.Extensions;

public static class ResultExtensions
{
    public static IActionResult ToActionResult(this Result result)
    {
        if (result.IsSuccess)
            return new OkResult();

        return ToErrorResult(result.Error);
    }

    public static IActionResult ToActionResult<TValue>(this Result<TValue> result)
    {
        if (result.IsSuccess)
            return new OkObjectResult(result.Value);

        return ToErrorResult(result.Error);
    }

    private static IActionResult ToErrorResult(Error error)
    {
        var body = new ErrorResponse(error.Code, error.Message)
        {
            Errors = error.ValidationErrors
        };

        return error.Type switch
        {
            // 404 Not Found — requested resource does not exist
            ErrorType.NotFound   => new NotFoundObjectResult(body),
            // 403 Forbidden — authenticated but not authorized to perform this action
            ErrorType.Forbidden  => new ObjectResult(body) { StatusCode = 403 },
            // 409 Conflict — request conflicts with current state (e.g. duplicate, stale data)
            ErrorType.Conflict   => new ConflictObjectResult(body),
            // 500 Internal Server Error — unhandled or unexpected failure
            ErrorType.Unexpected => new ObjectResult(body) { StatusCode = 500 },
            // 422 Unprocessable Entity — request is well-formed but fails business/domain validation
            ErrorType.Validation => new UnprocessableEntityObjectResult(body),
            // Should never happen — indicates a new ErrorType was added without a mapping
            _ => throw new InvalidOperationException($"Unhandled ErrorType: {error.Type}")
        };
    }
}
