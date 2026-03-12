namespace LucasWeb.Api.Services;

/// <summary>Evalúa predicciones de semanas ya cerradas y aprende bias y MAE por día de la semana.</summary>
public interface IEvaluatePredictionsService
{
    /// <summary>Busca la predicción de la semana pasada, compara con realidad, actualiza ActualRevenue/CompletedAt y aprende bias/MAE por DOW.</summary>
    Task EvaluateLastWeekIfPendingAsync();

    /// <summary>Evalúa la predicción de la semana indicada si está cerrada y aún no evaluada (para evaluación automática al consultar comparativas).</summary>
    Task EvaluateWeekIfPendingAsync(DateTime weekStartMonday);

    /// <summary>Re-evalúa la predicción de la semana con los datos actuales de facturación (p. ej. tras importar Excel con reales). Actualiza ActualRevenue y métricas para que las comparativas reflejen real vs predicción.</summary>
    Task ReEvaluateWeekAsync(DateTime weekStartMonday);
}
