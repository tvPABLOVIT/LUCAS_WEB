using LucasWeb.Api.Models;

namespace LucasWeb.Api.Services;

public interface IAuthService
{
    Task<(User? user, string? token)> ValidatePinAsync(string pin);
    Task<User?> GetUserByTokenAsync(string token);
    Task InvalidateTokenAsync(string token);
}
