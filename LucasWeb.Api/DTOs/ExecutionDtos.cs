using System.Text.Json.Serialization;

namespace LucasWeb.Api.DTOs;

public class ShiftDto
{
    [JsonPropertyName("shift_name")]
    public string ShiftName { get; set; } = "";

    [JsonPropertyName("revenue")]
    public decimal Revenue { get; set; }

    [JsonPropertyName("hours_worked")]
    public decimal HoursWorked { get; set; }

    [JsonPropertyName("planned_hours")]
    public decimal? PlannedHours { get; set; }

    [JsonPropertyName("staff_floor")]
    public int StaffFloor { get; set; }

    [JsonPropertyName("staff_kitchen")]
    public int StaffKitchen { get; set; }

    /// <summary>Horas reales repartidas a sala (proporción StaffFloor/(StaffFloor+StaffKitchen)).</summary>
    [JsonPropertyName("hours_sala_estimated")]
    public decimal? HoursSalaEstimated { get; set; }

    /// <summary>Horas reales repartidas a cocina (proporción StaffKitchen/(StaffFloor+StaffKitchen)).</summary>
    [JsonPropertyName("hours_cocina_estimated")]
    public decimal? HoursCocinaEstimated { get; set; }

    [JsonPropertyName("feedback_q1")]
    public string? FeedbackQ1 { get; set; }

    [JsonPropertyName("feedback_q2")]
    public string? FeedbackQ2 { get; set; }

    [JsonPropertyName("feedback_q3")]
    public string? FeedbackQ3 { get; set; }

    [JsonPropertyName("feedback_q4")]
    public string? FeedbackQ4 { get; set; }

    [JsonPropertyName("feedback_q5")]
    public string? FeedbackQ5 { get; set; }

    /// <summary>Facturación por persona en sala (Revenue / max(1, StaffFloor)).</summary>
    [JsonPropertyName("revenue_per_waiter_sala")]
    public decimal? RevenuePerWaiterSala { get; set; }

    /// <summary>Dificultad 1–5 derivada de Q1–Q4.</summary>
    [JsonPropertyName("difficulty_score")]
    public decimal? DifficultyScore { get; set; }

    /// <summary>Cómodo / Límite / Complicado.</summary>
    [JsonPropertyName("comfort_level")]
    public string? ComfortLevel { get; set; }

    [JsonPropertyName("revenue_per_waiter_cocina")]
    public decimal? RevenuePerWaiterCocina { get; set; }

    [JsonPropertyName("difficulty_score_kitchen")]
    public decimal? DifficultyScoreKitchen { get; set; }

    [JsonPropertyName("comfort_level_kitchen")]
    public string? ComfortLevelKitchen { get; set; }

    [JsonPropertyName("recorded_by")]
    public string? RecordedBy { get; set; }

    [JsonPropertyName("edited_by")]
    public string? EditedBy { get; set; }

    // Clima por turno (solo lectura; lo rellena backend)
    [JsonPropertyName("weather_code")]
    public int? WeatherCode { get; set; }

    [JsonPropertyName("weather_temp_avg")]
    public decimal? WeatherTempAvg { get; set; }

    [JsonPropertyName("weather_precip_mm")]
    public decimal? WeatherPrecipMm { get; set; }

    [JsonPropertyName("weather_wind_max_kmh")]
    public decimal? WeatherWindMaxKmh { get; set; }
}

public class CreateExecutionRequest
{
    [JsonPropertyName("date")]
    public string Date { get; set; } = ""; // yyyy-MM-dd

    [JsonPropertyName("total_revenue")]
    public decimal TotalRevenue { get; set; }

    [JsonPropertyName("total_hours_worked")]
    public decimal TotalHoursWorked { get; set; }

    [JsonPropertyName("staff_total")]
    public int StaffTotal { get; set; }

    [JsonPropertyName("notes")]
    public string? Notes { get; set; }

    [JsonPropertyName("shifts")]
    public List<ShiftDto> Shifts { get; set; } = new();
}

public class UpdateExecutionRequest
{
    [JsonPropertyName("total_revenue")]
    public decimal TotalRevenue { get; set; }

    [JsonPropertyName("total_hours_worked")]
    public decimal TotalHoursWorked { get; set; }

    [JsonPropertyName("staff_total")]
    public int StaffTotal { get; set; }

    [JsonPropertyName("notes")]
    public string? Notes { get; set; }

    [JsonPropertyName("shifts")]
    public List<ShiftDto> Shifts { get; set; } = new();
}

public class ExecutionDayResponse
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("date")]
    public string Date { get; set; } = "";

    [JsonPropertyName("total_revenue")]
    public decimal TotalRevenue { get; set; }

    [JsonPropertyName("total_hours_worked")]
    public decimal TotalHoursWorked { get; set; }

    [JsonPropertyName("staff_total")]
    public int StaffTotal { get; set; }

    [JsonPropertyName("notes")]
    public string? Notes { get; set; }

    [JsonPropertyName("is_feedback_only")]
    public bool IsFeedbackOnly { get; set; }

    // Clima diario (solo lectura; lo rellena backend)
    [JsonPropertyName("weather_code")]
    public int? WeatherCode { get; set; }

    [JsonPropertyName("weather_temp_max")]
    public decimal? WeatherTempMax { get; set; }

    [JsonPropertyName("weather_temp_min")]
    public decimal? WeatherTempMin { get; set; }

    [JsonPropertyName("weather_precip_mm")]
    public decimal? WeatherPrecipMm { get; set; }

    [JsonPropertyName("weather_wind_max_kmh")]
    public decimal? WeatherWindMaxKmh { get; set; }

    [JsonPropertyName("shifts")]
    public List<ShiftDto> Shifts { get; set; } = new();
}
