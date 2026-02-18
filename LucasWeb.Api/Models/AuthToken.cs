namespace LucasWeb.Api.Models;

public class AuthToken
{
    public Guid Id { get; set; }
    public string Token { get; set; } = "";
    public Guid UserId { get; set; }
    public DateTime ExpiresAt { get; set; }
    public DateTime CreatedAt { get; set; }
}
