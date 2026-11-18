using HrastERP.API.Responses;
using HrastERP.SharedKernel.Results;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;

namespace HrastERP.API.Authorization;

// Turns the outcome of UseAuthorization() into a response for every policy. A forbidden result is written
// as an ErrorResponse 403: the first ErrorFailureReason's error, otherwise General.Forbidden (e.g. a missing
// permission). Without this, ASP.NET Core answers with a bodiless 403. Challenges (no or invalid token)
// and successes go to the default handler; a challenge's 401 body is written by JwtChallengeResponse.
public sealed class ForbiddenResponseHandler : IAuthorizationMiddlewareResultHandler
{
    private readonly AuthorizationMiddlewareResultHandler _default = new();

    public async Task HandleAsync(
        RequestDelegate next,
        HttpContext context,
        AuthorizationPolicy policy,
        PolicyAuthorizationResult authorizeResult)
    {
        if (!authorizeResult.Forbidden)
        {
            await _default.HandleAsync(next, context, policy, authorizeResult);
            return;
        }

        var error = authorizeResult.AuthorizationFailure?.FailureReasons
            .OfType<ErrorFailureReason>()
            .Select(r => r.Error)
            .FirstOrDefault() ?? GeneralErrors.Forbidden;

        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        await context.Response.WriteAsJsonAsync(new ErrorResponse(error.Code, error.Message));
    }
}
