namespace LucasWeb.Api.Models;

public class WeeklyPrediction
{
    public Guid Id { get; set; }
    public DateTime WeekStartMonday { get; set; }
    public decimal? PredictedRevenue { get; set; }
    /// <summary>Facturación real de la semana (tras EvaluatePredictions).</summary>
    public decimal? ActualRevenue { get; set; }
    public string? DailyPredictionsJson { get; set; }
    public string? HistoricalStatsJson { get; set; }
    public string? AccuracyMetricsJson { get; set; }
    /// <summary>Métricas de precisión de personal recomendado vs real (sala/cocina por turno).</summary>
    public string? StaffAccuracyJson { get; set; }
    public DateTime CreatedAt { get; set; }
    /// <summary>Cuándo se evaluó la predicción (semana ya cerrada).</summary>
    public DateTime? CompletedAt { get; set; }
}
