using System.Text.Json.Serialization;

namespace LucasWeb.Api.DTOs;

public class ImportExcelResult
{
    [JsonPropertyName("days_created")]
    public int DaysCreated { get; set; }

    [JsonPropertyName("days_updated")]
    public int DaysUpdated { get; set; }

    [JsonPropertyName("shifts_updated")]
    public int ShiftsUpdated { get; set; }

    [JsonPropertyName("errors")]
    public List<string> Errors { get; set; } = new();

    /// <summary>Mensaje final (ej. "Estimaciones: X importados, Y actualizados, Z errores." o "Importados: X, Actualizados: Y, Errores: Z").</summary>
    [JsonPropertyName("message")]
    public string? Message { get; set; }
}
