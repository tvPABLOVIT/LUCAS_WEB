using Microsoft.Extensions.Hosting;

namespace LucasWeb.Api.Services;

/// <summary>
/// Ejecuta automáticamente la evaluación de la predicción de la semana pasada (bias/MAE)
/// y el recálculo de patrones (lluvia, festivos, temperatura) una vez al día.
/// Así el bias se actualiza sin depender de que el usuario llame a "Evaluar predicción" o "Calcular patrones".
/// </summary>
public class PredictionEvaluationHostedService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;

    public PredictionEvaluationHostedService(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try { await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken); } catch { }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunOnceAsync(stoppingToken);
            }
            catch
            {
                // No tumbar la API por fallos de evaluación.
            }

            try
            {
                await Task.Delay(TimeSpan.FromHours(24), stoppingToken);
            }
            catch
            {
                break;
            }
        }
    }

    private async Task RunOnceAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var evaluate = scope.ServiceProvider.GetRequiredService<IEvaluatePredictionsService>();
        var patterns = scope.ServiceProvider.GetRequiredService<IDetectedPatternsService>();

        await evaluate.EvaluateLastWeekIfPendingAsync();
        await patterns.ComputeAndSavePatternsAsync();
    }
}
