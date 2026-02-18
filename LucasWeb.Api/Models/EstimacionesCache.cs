namespace LucasWeb.Api.Models;

public class EstimacionesCache
{
    public Guid Id { get; set; }
    public string JsonPayload { get; set; } = "{}";
    public DateTime UpdatedAt { get; set; }
}
