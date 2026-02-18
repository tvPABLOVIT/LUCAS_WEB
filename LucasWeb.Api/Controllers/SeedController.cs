using LucasWeb.Api.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LucasWeb.Api.Controllers;

[ApiController]
[Route("api/seed")]
[Authorize]
public class SeedController : ControllerBase
{
    private readonly AppDbContext _db;

    public SeedController(AppDbContext db) => _db = db;

    /// <summary>
    /// Borra todos los días de ejecución y turnos y carga 2 meses (60 días) de datos de prueba (facturación, respuestas Q1–Q4).
    /// Solo admin o master (acceso máximo).
    /// </summary>
    [HttpPost("demo")]
    [Authorize(Roles = "admin,master")]
    public async Task<IActionResult> LoadDemoData()
    {
        await DataSeeder.SeedExecutionDemoDataForceAsync(_db);
        var count = await _db.ExecutionDays.CountAsync();
        var minDate = await _db.ExecutionDays.MinAsync(e => e.Date);
        var maxDate = await _db.ExecutionDays.MaxAsync(e => e.Date);
        return Ok(new
        {
            message = "Datos de prueba cargados: 2 meses (60 días) de ejecución (incluye hoy) con turnos y respuestas Q1–Q4.",
            count,
            minDate = minDate.ToString("yyyy-MM-dd"),
            maxDate = maxDate.ToString("yyyy-MM-dd")
        });
    }

    /// <summary>
    /// Devuelve cuántos días hay en la BD y el rango de fechas (para comprobar que los datos de muestra están).
    /// </summary>
    [HttpGet("status")]
    public async Task<IActionResult> GetStatus()
    {
        var count = await _db.ExecutionDays.CountAsync();
        if (count == 0)
            return Ok(new { count = 0, minDate = (string?)null, maxDate = (string?)null });
        var minDate = await _db.ExecutionDays.MinAsync(e => e.Date);
        var maxDate = await _db.ExecutionDays.MaxAsync(e => e.Date);
        return Ok(new { count, minDate = minDate.ToString("yyyy-MM-dd"), maxDate = maxDate.ToString("yyyy-MM-dd") });
    }
}
