namespace LucasWeb.Api.Models;

public class ShiftFeedback
{
    public Guid Id { get; set; }
    public Guid ExecutionDayId { get; set; }
    public ExecutionDay ExecutionDay { get; set; } = null!;
    public string ShiftName { get; set; } = ""; // Mediodia, Tarde, Noche
    public decimal Revenue { get; set; }
    public decimal HoursWorked { get; set; }
    public decimal? PlannedHours { get; set; }
    public int StaffFloor { get; set; }
    public int StaffKitchen { get; set; }
    public string? FeedbackQ1 { get; set; }
    public string? FeedbackQ2 { get; set; }
    public string? FeedbackQ3 { get; set; }
    public string? FeedbackQ4 { get; set; }
    /// <summary>Dificultad del turno en cocina (mismas opciones que Q4: Muy fácil … Muy difícil).</summary>
    public string? FeedbackQ5 { get; set; }
    /// <summary>Facturación del turno por persona en sala (Revenue / max(1, StaffFloor)).</summary>
    public decimal? RevenuePerWaiterSala { get; set; }
    /// <summary>Dificultad 1–5 derivada del feedback Q1–Q4.</summary>
    public decimal? DifficultyScore { get; set; }
    /// <summary>Cómodo / Límite / Complicado según DifficultyScore.</summary>
    public string? ComfortLevel { get; set; }
    /// <summary>Facturación del turno por persona en cocina (Revenue / max(1, StaffKitchen)).</summary>
    public decimal? RevenuePerWaiterCocina { get; set; }
    /// <summary>Dificultad cocina 1–5 derivada de Q5.</summary>
    public decimal? DifficultyScoreKitchen { get; set; }
    /// <summary>Cómodo / Límite / Complicado según DifficultyScoreKitchen.</summary>
    public string? ComfortLevelKitchen { get; set; }
    public string? RecordedBy { get; set; }
    public string? EditedBy { get; set; }
    // Clima por turno (agregado a partir de datos horarios).
    public int? WeatherCode { get; set; }
    public decimal? WeatherTempAvg { get; set; }
    public decimal? WeatherPrecipMm { get; set; }
    public decimal? WeatherWindMaxKmh { get; set; }
    public DateTime CreatedAt { get; set; }
}
