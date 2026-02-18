using System.Text.Json.Serialization;

namespace LucasWeb.Api.DTOs;

public class RecommendationDto
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("title")]
    public string Title { get; set; } = "";

    [JsonPropertyName("message")]
    public string? Message { get; set; }

    [JsonPropertyName("text")]
    public string? Text { get; set; }

    [JsonPropertyName("status")]
    public string Status { get; set; } = "";

    [JsonPropertyName("createdAt")]
    public DateTime CreatedAt { get; set; }
}

public class RecommendationVersionResponse
{
    [JsonPropertyName("version")]
    public long Version { get; set; }
}
