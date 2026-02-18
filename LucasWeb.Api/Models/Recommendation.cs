namespace LucasWeb.Api.Models;

public class Recommendation
{
    public Guid Id { get; set; }
    public string Title { get; set; } = "";
    public string? Message { get; set; }
    public string Status { get; set; } = "pending"; // pending, accepted, applied, discarded, expired
    public DateTime CreatedAt { get; set; }
    public DateTime? ExpiresAt { get; set; }
}
