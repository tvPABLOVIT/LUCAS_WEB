using LucasWeb.Api.DTOs;
using LucasWeb.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LucasWeb.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;

    public AuthController(IAuthService authService) => _authService = authService;

    [HttpPost("pin")]
    [AllowAnonymous]
    public async Task<ActionResult<AuthResponse>> LoginWithPin([FromBody] PinLoginRequest request)
    {
        if (string.IsNullOrWhiteSpace(request?.Pin))
            return BadRequest(new { message = "PIN requerido" });

        var (user, token) = await _authService.ValidatePinAsync(request.Pin);
        if (user == null || token == null)
            return Unauthorized(new { message = "PIN incorrecto" });

        return Ok(new AuthResponse
        {
            Role = user.Role,
            UserId = user.Id.ToString(),
            Token = token
        });
    }

    [HttpGet("me")]
    [Authorize]
    public IActionResult Me()
    {
        var userId = User.FindFirst("userId")?.Value;
        var role = User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value;
        if (string.IsNullOrEmpty(userId)) return Unauthorized();
        return Ok(new MeResponse { UserId = userId, Role = role ?? "user" });
    }

    [HttpPost("logout")]
    [AllowAnonymous]
    public async Task<IActionResult> Logout()
    {
        var token = Request.Headers.Authorization.FirstOrDefault()?.Replace("Bearer ", "").Trim();
        if (!string.IsNullOrEmpty(token))
            await _authService.InvalidateTokenAsync(token);
        return Ok();
    }
}
