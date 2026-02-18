namespace LucasWeb.Api.Models;

/// <summary>Patrón aprendido del histórico: impacto lluvia, festivos, temperatura.</summary>
public class DetectedPattern
{
    public Guid Id { get; set; }
    /// <summary>Tipo: "Impacto clima lluvioso", "Impacto festivos", "Impacto temperatura".</summary>
    public string Type { get; set; } = "";
    /// <summary>Clave opcional para variantes (ej. "default").</summary>
    public string? Key { get; set; }
    /// <summary>JSON: pct_diff, factor, confidence, etc.</summary>
    public string? JsonData { get; set; }
    /// <summary>Confianza 0–1 para ponderar el factor.</summary>
    public decimal Confidence { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
