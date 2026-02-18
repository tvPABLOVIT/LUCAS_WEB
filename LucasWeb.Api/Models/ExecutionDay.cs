namespace LucasWeb.Api.Models;

public class ExecutionDay
{
    public Guid Id { get; set; }
    public DateTime Date { get; set; } // solo fecha, sin hora
    public decimal TotalRevenue { get; set; }
    public decimal TotalHoursWorked { get; set; }
    /// <summary>Horas totales del día según el cuadrante PDF (suma columna Total por empleado). Usado cuando no hay horas reales (Excel).</summary>
    public decimal? PlannedHoursTotal { get; set; }
    public int StaffTotal { get; set; }
    public string? Notes { get; set; }
    public int? WeatherCode { get; set; }
    /// <summary>Temperatura representativa (por compatibilidad UI). Preferimos TempMax.</summary>
    public decimal? WeatherTemp { get; set; }
    public decimal? WeatherTempMax { get; set; }
    public decimal? WeatherTempMin { get; set; }
    /// <summary>Precipitación diaria total (mm).</summary>
    public decimal? WeatherPrecipMm { get; set; }
    /// <summary>Viento máximo (km/h) a 10m.</summary>
    public decimal? WeatherWindMaxKmh { get; set; }
    public bool IsHoliday { get; set; }
    /// <summary>
    /// Marca de "solo feedback": se registró personal/preguntas pero no hay datos de facturación/horas.
    /// Se usa para excluir estos días de KPIs y predicciones basadas en revenue/hours.
    /// </summary>
    public bool IsFeedbackOnly { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public ICollection<ShiftFeedback> ShiftFeedbacks { get; set; } = new List<ShiftFeedback>();
}
