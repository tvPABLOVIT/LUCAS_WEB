using LucasWeb.Api.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace LucasWeb.Api.Services;

/// <summary>
/// Elimina periódicamente los AuthTokens expirados para evitar acumulación en la BD.
/// Se ejecuta una vez al día.
/// </summary>
public class AuthTokenCleanupHostedService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<AuthTokenCleanupHostedService> _logger;

    public AuthTokenCleanupHostedService(IServiceScopeFactory scopeFactory, ILogger<AuthTokenCleanupHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
        }
        catch (OperationCanceledException) { return; }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var now = DateTime.UtcNow;
                var expired = await db.AuthTokens.Where(t => t.ExpiresAt < now).ToListAsync(stoppingToken);
                var count = expired.Count;
                if (count > 0)
                {
                    db.AuthTokens.RemoveRange(expired);
                    await db.SaveChangesAsync(stoppingToken);
                    _logger.LogInformation("Limpieza de tokens: eliminados {Count} tokens expirados.", count);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error al limpiar tokens expirados.");
            }

            try
            {
                await Task.Delay(TimeSpan.FromHours(24), stoppingToken);
            }
            catch (OperationCanceledException) { break; }
        }
    }
}
