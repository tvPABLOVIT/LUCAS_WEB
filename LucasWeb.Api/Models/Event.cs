namespace LucasWeb.Api.Models;

/// <summary>Evento que puede afectar demanda (manual o importado de Open Data BCN).</summary>
public class Event
{
    public Guid Id { get; set; }
    /// <summary>Fecha del evento (solo día).</summary>
    public DateTime Date { get; set; }
    public string Name { get; set; } = "";
    /// <summary>Alto, Medio, Bajo o vacío.</summary>
    public string? Impact { get; set; }
    public string? Description { get; set; }
    public string? Source { get; set; }
    public string? ExternalId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
