using LucasWeb.Api.Data;
using LucasWeb.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LucasWeb.Api.Controllers;

[ApiController]
[Route("api/users")]
[Authorize(Roles = "admin,master")]
public class UsersController : ControllerBase
{
    private readonly AppDbContext _db;

    public UsersController(AppDbContext db) => _db = db;

    [HttpGet]
    public async Task<ActionResult<IEnumerable<object>>> List()
    {
        var list = await _db.Users
            .OrderBy(u => u.FullName)
            .Select(u => new
            {
                id = u.Id.ToString(),
                fullName = u.FullName,
                email = u.Email ?? "",
                role = u.Role ?? "user",
                isActive = u.IsActive
            })
            .ToListAsync();
        return Ok(list);
    }

    // Nota: el frontend contempla "master" como rol de acceso completo.
    private static readonly string[] AllowedRoles = { "user", "manager", "admin", "master" };

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateUserRequest? request)
    {
        if (request == null)
            return BadRequest(new { message = "Datos de usuario no válidos (body vacío o formato incorrecto)." });
        if (string.IsNullOrWhiteSpace(request.FullName))
            return BadRequest(new { message = "El nombre es obligatorio." });
        if (string.IsNullOrWhiteSpace(request.Email))
            return BadRequest(new { message = "El email es obligatorio." });
        var email = request.Email.Trim();
        var existingEmails = await _db.Users.Where(u => u.Email != null).Select(u => u.Email).ToListAsync();
        if (existingEmails.Any(e => string.Equals((e ?? "").Trim(), email, StringComparison.OrdinalIgnoreCase)))
            return BadRequest(new { message = "El email ya está en uso." });
        if (string.IsNullOrWhiteSpace(request.Pin) || request.Pin.Length < 4)
            return BadRequest(new { message = "El PIN debe tener al menos 4 caracteres." });
        if (request.Pin.Length > 12)
            return BadRequest(new { message = "El PIN no puede tener más de 12 caracteres." });

        var role = request.Role ?? "user";
        if (!AllowedRoles.Contains(role, StringComparer.OrdinalIgnoreCase))
            role = "user";

        var pinHash = BCrypt.Net.BCrypt.HashPassword(request.Pin.Trim());
        var now = DateTime.UtcNow;
        var user = new User
        {
            Id = Guid.NewGuid(),
            FullName = request.FullName.Trim(),
            Email = email,
            Role = role,
            PinHash = pinHash,
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now
        };
        _db.Users.Add(user);
        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(List), new
        {
            id = user.Id.ToString(),
            fullName = user.FullName,
            email = user.Email ?? "",
            role = user.Role,
            isActive = user.IsActive
        });
    }

    [HttpPatch("{id}")]
    public async Task<IActionResult> Update(string id, [FromBody] UpdateUserRequest request)
    {
        if (!Guid.TryParse(id, out var guid))
            return BadRequest(new { message = "Id de usuario inválido." });
        var user = await _db.Users.FindAsync(guid);
        if (user == null)
            return NotFound(new { message = "Usuario no encontrado." });

        if (request.FullName != null) user.FullName = request.FullName.Trim();
        if (request.Email != null) user.Email = string.IsNullOrWhiteSpace(request.Email) ? null : request.Email.Trim();
        if (request.Role != null)
        {
            var role = AllowedRoles.Contains(request.Role, StringComparer.OrdinalIgnoreCase) ? request.Role : user.Role;
            user.Role = role;
        }
        if (request.IsActive.HasValue) user.IsActive = request.IsActive.Value;
        if (!string.IsNullOrWhiteSpace(request.Pin))
        {
            if (request.Pin.Length < 4)
                return BadRequest(new { message = "El PIN debe tener al menos 4 caracteres." });
            if (request.Pin.Length > 12)
                return BadRequest(new { message = "El PIN no puede tener más de 12 caracteres." });
            user.PinHash = BCrypt.Net.BCrypt.HashPassword(request.Pin.Trim());
        }
        user.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return Ok(new
        {
            id = user.Id.ToString(),
            fullName = user.FullName,
            email = user.Email ?? "",
            role = user.Role,
            isActive = user.IsActive
        });
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(string id)
    {
        if (!Guid.TryParse(id, out var guid))
            return BadRequest(new { message = "Id de usuario inválido." });
        var user = await _db.Users.FindAsync(guid);
        if (user == null)
            return NotFound(new { message = "Usuario no encontrado." });
        var isAdmin = string.Equals(user.Role, "admin", StringComparison.OrdinalIgnoreCase);
        if (isAdmin && user.IsActive)
        {
            var otherActiveAdmins = await _db.Users.CountAsync(u => u.Id != user.Id && u.IsActive && string.Equals(u.Role, "admin", StringComparison.OrdinalIgnoreCase));
            if (otherActiveAdmins == 0)
                return BadRequest(new { message = "No se puede eliminar el último administrador activo." });
        }
        var tokens = await _db.AuthTokens.Where(t => t.UserId == user.Id).ToListAsync();
        _db.AuthTokens.RemoveRange(tokens);
        _db.Users.Remove(user);
        await _db.SaveChangesAsync();
        return NoContent();
    }
}

public class CreateUserRequest
{
    public string FullName { get; set; } = "";
    public string? Email { get; set; }
    public string Pin { get; set; } = "";
    public string? Role { get; set; }
}

public class UpdateUserRequest
{
    public string? FullName { get; set; }
    public string? Email { get; set; }
    public string? Pin { get; set; }
    public string? Role { get; set; }
    public bool? IsActive { get; set; }
}
