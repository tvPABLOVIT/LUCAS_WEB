using System.Text.Json.Serialization;

namespace LucasWeb.Api.DTOs;

public class PinLoginRequest
{
    [JsonPropertyName("pin")]
    public string Pin { get; set; } = "";
}

public class AuthResponse
{
    [JsonPropertyName("role")]
    public string Role { get; set; } = "";

    [JsonPropertyName("userId")]
    public string UserId { get; set; } = "";

    [JsonPropertyName("token")]
    public string Token { get; set; } = "";
}

public class MeResponse
{
    [JsonPropertyName("userId")]
    public string UserId { get; set; } = "";

    [JsonPropertyName("role")]
    public string Role { get; set; } = "";
}
