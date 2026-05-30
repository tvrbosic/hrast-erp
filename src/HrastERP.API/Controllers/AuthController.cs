using HrastERP.API.Contracts.Auth;
using HrastERP.Infrastructure.Authentication;
using Microsoft.AspNetCore.Mvc;

namespace HrastERP.API.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController(IAuthService authService) : ControllerBase
{
    [HttpPost("login")]
    public async Task<IActionResult> Login(
        LoginRequest request, CancellationToken ct)
    {
        var result = await authService.LoginAsync(request.Email, request.Password, ct);

        return result.IsSuccess
            ? Ok(result.Value)
            : Unauthorized(new { error = result.Error.Message });
    }

    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh(
        RefreshTokenRequest request, CancellationToken ct)
    {
        var result = await authService.RefreshAsync(request.RefreshToken, ct);

        return result.IsSuccess
            ? Ok(result.Value)
            : Unauthorized(new { error = result.Error.Message });
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout(
        LogoutRequest request, CancellationToken ct)
    {
        var result = await authService.LogoutAsync(request.RefreshToken, ct);

        return result.IsSuccess
            ? NoContent()
            : BadRequest(new { error = result.Error.Message });
    }
}
