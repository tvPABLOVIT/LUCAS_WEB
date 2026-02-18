namespace LucasWeb.Api.Models;

public class User
{
    public Guid Id { get; set; }
    public string FullName { get; set; } = "";
    public string? Email { get; set; }
    public string? PasswordHash { get; set; }
    public string Role { get; set; } = "user"; // admin, manager, master, user
    public string? PinHash { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
