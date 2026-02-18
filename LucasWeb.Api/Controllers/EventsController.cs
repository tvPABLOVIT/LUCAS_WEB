using LucasWeb.Api.Data;
using LucasWeb.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LucasWeb.Api.Controllers;

[ApiController]
[Route("api/events")]
[Authorize(Roles = "admin,manager,master")]
public class EventsController : ControllerBase
{
    private readonly AppDbContext _db;

    public EventsController(AppDbContext db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] string from, [FromQuery] string to)
    {
        if (!DateTime.TryParse(from, out var fromD) || !DateTime.TryParse(to, out var toD))
            return BadRequest(new { message = "Rango inválido (from/to: yyyy-MM-dd)." });
        var start = fromD.Date;
        var end = toD.Date;
        if (end < start) (start, end) = (end, start);
        if ((end - start).TotalDays > 370) end = start.AddDays(370);

        var list = await _db.Events
            .AsNoTracking()
            .Where(e => e.Date >= start && e.Date <= end)
            .OrderBy(e => e.Date)
            .ThenBy(e => e.Name)
            .Select(e => new
            {
                id = e.Id.ToString(),
                date = e.Date.ToString("yyyy-MM-dd"),
                name = e.Name,
                impact = e.Impact,
                description = e.Description,
                source = e.Source
            })
            .ToListAsync();

        return Ok(list);
    }

    public sealed class CreateEventRequest
    {
        public string Date { get; set; } = ""; // yyyy-MM-dd
        public string Name { get; set; } = "";
        public string? Impact { get; set; } // Alto/Medio/Bajo
        public string? Description { get; set; }
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateEventRequest request)
    {
        if (request == null) return BadRequest(new { message = "Body inválido." });
        if (!DateTime.TryParse(request.Date, out var d))
            return BadRequest(new { message = "Fecha inválida (yyyy-MM-dd)." });
        if (string.IsNullOrWhiteSpace(request.Name))
            return BadRequest(new { message = "El nombre es obligatorio." });

        var now = DateTime.UtcNow;
        var ev = new Event
        {
            Id = Guid.NewGuid(),
            Date = d.Date,
            Name = request.Name.Trim(),
            Impact = string.IsNullOrWhiteSpace(request.Impact) ? null : request.Impact.Trim(),
            Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
            Source = "manual",
            CreatedAt = now,
            UpdatedAt = now
        };
        _db.Events.Add(ev);
        await _db.SaveChangesAsync();
        return Ok(new
        {
            id = ev.Id.ToString(),
            date = ev.Date.ToString("yyyy-MM-dd"),
            name = ev.Name,
            impact = ev.Impact,
            description = ev.Description,
            source = ev.Source
        });
    }

    public sealed class UpdateEventRequest
    {
        public string? Name { get; set; }
        public string? Impact { get; set; }
        public string? Description { get; set; }
        public string? Date { get; set; } // opcional
    }

    [HttpPatch("{id}")]
    public async Task<IActionResult> Update(string id, [FromBody] UpdateEventRequest request)
    {
        if (!Guid.TryParse(id, out var guid))
            return BadRequest(new { message = "Id inválido." });
        var ev = await _db.Events.FirstOrDefaultAsync(e => e.Id == guid);
        if (ev == null) return NotFound(new { message = "Evento no encontrado." });

        if (request.Date != null)
        {
            if (!DateTime.TryParse(request.Date, out var d))
                return BadRequest(new { message = "Fecha inválida (yyyy-MM-dd)." });
            ev.Date = d.Date;
        }
        if (request.Name != null)
        {
            var n = request.Name.Trim();
            if (string.IsNullOrWhiteSpace(n)) return BadRequest(new { message = "El nombre no puede quedar vacío." });
            ev.Name = n;
        }
        if (request.Impact != null) ev.Impact = string.IsNullOrWhiteSpace(request.Impact) ? null : request.Impact.Trim();
        if (request.Description != null) ev.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        ev.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        return Ok(new
        {
            id = ev.Id.ToString(),
            date = ev.Date.ToString("yyyy-MM-dd"),
            name = ev.Name,
            impact = ev.Impact,
            description = ev.Description,
            source = ev.Source
        });
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(string id)
    {
        if (!Guid.TryParse(id, out var guid))
            return BadRequest(new { message = "Id inválido." });
        var ev = await _db.Events.FirstOrDefaultAsync(e => e.Id == guid);
        if (ev == null) return NotFound(new { message = "Evento no encontrado." });
        _db.Events.Remove(ev);
        await _db.SaveChangesAsync();
        return NoContent();
    }
}

