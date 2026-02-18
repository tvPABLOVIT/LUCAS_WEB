using System.Security.Claims;
using LucasWeb.Api.Services;
using Microsoft.AspNetCore.Http;

namespace LucasWeb.Api.Middleware;

public class BearerAuthMiddleware
{
    private readonly RequestDelegate _next;

    public BearerAuthMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context, IAuthService authService)
    {
        var authHeader = context.Request.Headers.Authorization.FirstOrDefault();
        if (!string.IsNullOrEmpty(authHeader) && authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            var token = authHeader["Bearer ".Length..].Trim();
            if (!string.IsNullOrEmpty(token))
            {
                var user = await authService.GetUserByTokenAsync(token);
                if (user != null)
                {
                    var claims = new[]
                    {
                        new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                        new Claim(ClaimTypes.Role, user.Role),
                        new Claim("userId", user.Id.ToString())
                    };
                    context.User = new ClaimsPrincipal(new ClaimsIdentity(claims, "Bearer") { });
                }
            }
        }
        await _next(context);
    }
}
