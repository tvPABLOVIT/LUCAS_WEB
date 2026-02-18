using LucasWeb.Api.Data;
using LucasWeb.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace LucasWeb.Api.Services;

public class AuthService : IAuthService
{
    private readonly AppDbContext _db;
    private readonly IOptions<LucasOptions> _options;
    private const int TokenLength = 64;

    public AuthService(AppDbContext db, IOptions<LucasOptions> options)
    {
        _db = db;
        _options = options;
    }

    public async Task<(User? user, string? token)> ValidatePinAsync(string pin)
    {
        if (string.IsNullOrWhiteSpace(pin)) return (null, null);
        var users = await _db.Users.Where(u => u.IsActive && u.PinHash != null).ToListAsync();
        foreach (var user in users)
        {
            if (user.PinHash != null && BCrypt.Net.BCrypt.Verify(pin.Trim(), user.PinHash))
            {
                var token = GenerateToken();
                var expiresAt = DateTime.UtcNow.AddMinutes(_options.Value.TokenExpirationMinutes);
                _db.AuthTokens.Add(new AuthToken
                {
                    Id = Guid.NewGuid(),
                    Token = token,
                    UserId = user.Id,
                    ExpiresAt = expiresAt,
                    CreatedAt = DateTime.UtcNow
                });
                await _db.SaveChangesAsync();
                return (user, token);
            }
        }
        return (null, null);
    }

    public async Task<User?> GetUserByTokenAsync(string token)
    {
        if (string.IsNullOrWhiteSpace(token)) return null;
        var authToken = await _db.AuthTokens
            .FirstOrDefaultAsync(t => t.Token == token && t.ExpiresAt > DateTime.UtcNow);
        if (authToken == null) return null;
        return await _db.Users.FindAsync(authToken.UserId);
    }

    public async Task InvalidateTokenAsync(string token)
    {
        var t = await _db.AuthTokens.FirstOrDefaultAsync(x => x.Token == token);
        if (t != null)
        {
            _db.AuthTokens.Remove(t);
            await _db.SaveChangesAsync();
        }
    }

    private static string GenerateToken()
    {
        var bytes = new byte[TokenLength / 2];
        using var rng = System.Security.Cryptography.RandomNumberGenerator.Create();
        rng.GetBytes(bytes);
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
