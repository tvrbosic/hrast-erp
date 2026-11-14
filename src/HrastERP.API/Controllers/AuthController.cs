using HrastERP.API.Extensions;
using HrastERP.API.Requests.Auth;
using HrastERP.API.Responses;
using HrastERP.Infrastructure.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HrastERP.API.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController(IAuthService authService) : ControllerBase
{
    // Responses carrying tokens must never be cached by browsers or proxies (RFC 6749 §5.1).
    // Sends "Cache-Control: no-store,no-cache" and "Pragma: no-cache", on error results too.
    [HttpPost("login")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    [ProducesResponseType<ApiResponse<AuthTokens>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Login(
        LoginRequest request, CancellationToken ct)
    {
        var result = await authService.LoginAsync(request.Email, request.Password, ct);
        return result.ToActionResult();
    }

    [HttpPost("refresh")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    [ProducesResponseType<ApiResponse<AuthTokens>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Refresh(
        RefreshTokenRequest request, CancellationToken ct)
    {
        var result = await authService.RefreshAsync(request.RefreshToken, ct);
        return result.ToActionResult();
    }

    [HttpPost("logout")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Logout(
        LogoutRequest request, CancellationToken ct)
    {
        var result = await authService.LogoutAsync(request.RefreshToken, ct);
        return result.ToActionResult();
    }

    // Any authenticated user may read their own data, so no [RequirePermission]
    [HttpGet("me")]
    [Authorize]
    [ProducesResponseType<ApiResponse<AuthenticatedUser>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Me(CancellationToken ct)
    {
        var result = await authService.GetCurrentUserAsync(ct);
        return result.ToActionResult();
    }
}
