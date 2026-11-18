using HrastERP.API.Responses;
using HrastERP.SharedKernel.Results;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

namespace HrastERP.API.Authentication;

/// <summary>
/// JwtBearer <c>OnChallenge</c> handler that writes every 401 (no, invalid or expired access token) as an
/// <see cref="ErrorResponse"/>: <c>Auth.TokenExpired</c> when the token has expired, so clients know a refresh
/// can help, otherwise <c>General.Unauthorized</c>. Without it, JwtBearer answers with a bodiless 401.
/// </summary>
public static class JwtChallengeResponse
{
    public static async Task WriteAsync(JwtBearerChallengeContext context)
    {
        // Take over the response; otherwise JwtBearer writes its own bodiless 401 after this handler
        context.HandleResponse();

        // Pick the error from why authentication failed (null failure = no token was sent)
        var expired = context.AuthenticateFailure is SecurityTokenExpiredException;
        var error = expired ? ApiErrors.TokenExpired : GeneralErrors.Unauthorized;

        // RFC 6750 §3: a 401 carries a WWW-Authenticate challenge; error="invalid_token" when a token was sent
        // but rejected. HandleResponse() skips the header JwtBearer would have set, so it is written here
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        context.Response.Headers.WWWAuthenticate = context.AuthenticateFailure is null
            ? context.Options.Challenge
            : $"{context.Options.Challenge} error=\"invalid_token\"";

        await context.Response.WriteAsJsonAsync(new ErrorResponse(error.Code, error.Message));
    }
}
