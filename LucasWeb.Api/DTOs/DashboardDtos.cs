using System.Text.Json.Serialization;

namespace LucasWeb.Api.DTOs;

public class DashboardDayItemDto
{
    [JsonPropertyName("date")]
    public string Date { get; set; } = "";

    [JsonPropertyName("dayName")]
    public string DayName { get; set; } = "";

    [JsonPropertyName("revenue")]
    public decimal Revenue { get; set; }

    [JsonPropertyName("hoursWorked")]
    public decimal HoursWorked { get; set; }

    [JsonPropertyName("productivity")]
    public decimal Productivity { get; set; }

    [JsonPropertyName("staffTotal")]
    public int StaffTotal { get; set; }

    /// <summary>Resumen personal sala por turno: "2-2-2" (Mediodía-Tarde-Noche).</summary>
    [JsonPropertyName("staffSummarySala")]
    public string? StaffSummarySala { get; set; }

    /// <summary>Resumen personal cocina por turno: "1-1-1".</summary>
    [JsonPropertyName("staffSummaryCocina")]
    public string? StaffSummaryCocina { get; set; }

    /// <summary>Horas de equipo calculadas: (Sala+Cocina)*horasPorTurno por cada turno. Solo se usan cuando el personal es manual (sin PDF): cada turno cuenta como h/turno completas.</summary>
    [JsonPropertyName("calculatedStaffHours")]
    public decimal? CalculatedStaffHours { get; set; }

    /// <summary>Suma de horas por turno del cuadrante PDF (horas reales por turno; si alguien trabajó 2 h en un turno, son 2 h). Cuando hay PDF, se usan estas como horas del día en lugar de CalculatedStaffHours.</summary>
    [JsonPropertyName("plannedHoursFromPdf")]
    public decimal? PlannedHoursFromPdf { get; set; }

    /// <summary>Distribución de horas por turno del PDF: "Mediodía: X h, Tarde: Y h, Noche: Z h".</summary>
    [JsonPropertyName("plannedHoursBreakdown")]
    public string? PlannedHoursBreakdown { get; set; }

    /// <summary>Horas efectivas: Excel (TotalHoursWorked) > PDF (PlannedHoursTotal/PlannedHoursFromShifts) > CalculatedStaffHours (solo si dato manual).</summary>
    [JsonPropertyName("effectiveHours")]
    public decimal? EffectiveHours { get; set; }

    /// <summary>Productividad del día usando EffectiveHours (Revenue / EffectiveHours).</summary>
    [JsonPropertyName("effectiveProductivity")]
    public decimal? EffectiveProductivity { get; set; }

    /// <summary>Media de facturación de ese día de la semana en las últimas 12 semanas (antes de la semana seleccionada).</summary>
    [JsonPropertyName("avgRevenueHistoric")]
    public decimal? AvgRevenueHistoric { get; set; }

    /// <summary>Porcentaje de este día respecto a la media histórica del día de la semana: (Revenue - avg) / avg * 100. Null si no hay media.</summary>
    [JsonPropertyName("pctVsAvgHistoric")]
    public int? PctVsAvgHistoric { get; set; }

    /// <summary>Tendencia del día de la semana en el tiempo: comparando mitad reciente vs mitad antigua de las últimas 12 semanas (ej. "↑ Al alza (+17%)").</summary>
    [JsonPropertyName("trendLabel")]
    public string? TrendLabel { get; set; }

    /// <summary>Comparación con el mismo día de la semana anterior, ej. "vs sem. ant.: +5 %".</summary>
    [JsonPropertyName("trendVsPrevWeek")]
    public string? TrendVsPrevWeek { get; set; }

    [JsonPropertyName("weatherCode")]
    public int? WeatherCode { get; set; }

    [JsonPropertyName("weatherTempMax")]
    public decimal? WeatherTempMax { get; set; }

    [JsonPropertyName("weatherTempMin")]
    public decimal? WeatherTempMin { get; set; }

    [JsonPropertyName("weatherPrecipMm")]
    public decimal? WeatherPrecipMm { get; set; }

    [JsonPropertyName("weatherWindMaxKmh")]
    public decimal? WeatherWindMaxKmh { get; set; }
}

public class DashboardWeekResponse
{
    [JsonPropertyName("totalRevenue")]
    public decimal TotalRevenue { get; set; }

    [JsonPropertyName("avgProductivity")]
    public decimal? AvgProductivity { get; set; }

    [JsonPropertyName("totalHours")]
    public decimal TotalHours { get; set; }

    [JsonPropertyName("avgStaff")]
    public decimal AvgStaff { get; set; }

    [JsonPropertyName("avgRevenueHistoric")]
    public decimal? AvgRevenueHistoric { get; set; }

    [JsonPropertyName("avgProductivityHistoric")]
    public decimal? AvgProductivityHistoric { get; set; }

    [JsonPropertyName("avgHoursHistoric")]
    public decimal? AvgHoursHistoric { get; set; }

    [JsonPropertyName("costePersonalEurFromContrato")]
    public decimal? CostePersonalEurFromContrato { get; set; }

    [JsonPropertyName("costePersonalPctVsHistoric")]
    public decimal? CostePersonalPctVsHistoric { get; set; }

    [JsonPropertyName("prevWeekRevenue")]
    public decimal? PrevWeekRevenue { get; set; }

    [JsonPropertyName("prevWeekProductivity")]
    public decimal? PrevWeekProductivity { get; set; }

    [JsonPropertyName("resumenClasificacion")]
    public string? ResumenClasificacion { get; set; }

    [JsonPropertyName("resumenTexto")]
    public string? ResumenTexto { get; set; }

    [JsonPropertyName("costePersonalEur")]
    public decimal? CostePersonalEur { get; set; }

    [JsonPropertyName("costePersonalPctFacturacion")]
    public decimal? CostePersonalPctFacturacion { get; set; }

    [JsonPropertyName("facturacionObjetivo")]
    public decimal? FacturacionObjetivo { get; set; }

    [JsonPropertyName("productividadObjetivo")]
    public decimal? ProductividadObjetivo { get; set; }

    [JsonPropertyName("days")]
    public List<DashboardDayItemDto> Days { get; set; } = new();

    [JsonPropertyName("last30Days")]
    public List<DailyRevenueItemDto> Last30Days { get; set; } = new();
}

public class DailyRevenueItemDto
{
    [JsonPropertyName("date")]
    public string Date { get; set; } = "";

    [JsonPropertyName("revenue")]
    public decimal Revenue { get; set; }
}
