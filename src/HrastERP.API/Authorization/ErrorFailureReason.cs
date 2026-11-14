using HrastERP.SharedKernel.Results;
using Microsoft.AspNetCore.Authorization;

namespace HrastERP.API.Authorization;

// A failure reason that carries the Error to return. An authorization handler fails with it when its
// policy needs a specific 403 code; ForbiddenResponseHandler writes that Error instead of General.Forbidden.
public sealed class ErrorFailureReason(IAuthorizationHandler handler, Error error)
    : AuthorizationFailureReason(handler, error.Message)
{
    public Error Error { get; } = error;
}
