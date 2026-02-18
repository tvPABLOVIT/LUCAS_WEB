using LucasWeb.Api.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace LucasWeb.Api.Controllers;

[ApiController]
[Route("api/database")]
[Authorize(Roles = "admin,master")]
public class DatabaseController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IWebHostEnvironment _env;

    public DatabaseController(AppDbContext db, IWebHostEnvironment env)
    {
        _db = db;
        _env = env;
    }

    [HttpGet("info")]
    public IActionResult Info()
    {
        try
        {
            var dbPath = GetDbFilePath();
            var exists = System.IO.File.Exists(dbPath);
            var size = exists ? new FileInfo(dbPath).Length : 0;
            var backupsDir = GetBackupsDir();
            var backups = Directory.Exists(backupsDir) ? Directory.EnumerateFiles(backupsDir, "*.db").Count() : 0;
            return Ok(new
            {
                db_path = dbPath,
                exists,
                size_bytes = size,
                backups_count = backups
            });
        }
        catch (Exception ex)
        {
            return Ok(new { db_path = "", exists = false, size_bytes = 0, backups_count = 0, message = ex.Message });
        }
    }

    /// <summary>
    /// Lista de copias de seguridad (archivos .db) guardadas en el servidor.
    /// </summary>
    [HttpGet("backups")]
    public ActionResult<IEnumerable<object>> ListBackups()
    {
        var backupsDir = GetBackupsDir();
        if (!Directory.Exists(backupsDir)) return Ok(Array.Empty<object>());

        var items = Directory.EnumerateFiles(backupsDir, "*.db")
            .Select(p => new FileInfo(p))
            .OrderByDescending(f => f.LastWriteTimeUtc)
            .Select(f => new
            {
                name = f.Name,
                path = f.Name, // por compatibilidad con el frontend: el valor que se envía a /restore
                size_bytes = f.Length,
                modified_utc = f.LastWriteTimeUtc.ToString("o")
            })
            .ToList();

        return Ok(items);
    }

    /// <summary>
    /// Crear copia de seguridad (copia física del SQLite).
    /// </summary>
    [HttpPost("backup")]
    public IActionResult CreateBackup()
    {
        var dbPath = GetDbFilePath();
        if (!System.IO.File.Exists(dbPath))
            return NotFound(new { message = "No se encontró el archivo de base de datos." });

        var backupsDir = GetBackupsDir();
        Directory.CreateDirectory(backupsDir);

        var ts = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
        var fileName = $"manageros_{ts}.db";
        var dest = Path.Combine(backupsDir, fileName);

        System.IO.File.Copy(dbPath, dest, overwrite: false);
        return Ok(new { message = "Copia creada.", name = fileName });
    }

    /// <summary>
    /// Restaurar desde copia (reemplaza el archivo SQLite). Si hay conexiones abiertas puede fallar;
    /// en ese caso, devuelve error indicando reiniciar el servicio.
    /// </summary>
    [HttpPost("restore")]
    public IActionResult Restore([FromBody] RestoreRequest? request)
    {
        var raw = (request?.Path ?? "").Trim();
        if (string.IsNullOrEmpty(raw))
            return BadRequest(new { message = "Seleccione una copia (path requerido)." });

        // Seguridad: solo se permite restaurar desde el directorio de backups; aceptar únicamente nombre de archivo.
        var fileName = Path.GetFileName(raw);
        if (string.IsNullOrEmpty(fileName) || fileName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            return BadRequest(new { message = "Nombre de copia inválido." });

        var backupsDir = GetBackupsDir();
        var backupPath = Path.Combine(backupsDir, fileName);
        if (!System.IO.File.Exists(backupPath))
            return NotFound(new { message = "Copia no encontrada." });

        var dbPath = GetDbFilePath();
        try
        {
            // Intentar liberar pools/locks de SQLite.
            try { _db.Database.CloseConnection(); } catch { }
            try { SqliteConnection.ClearAllPools(); } catch { }

            // Guardar una copia del actual por si acaso.
            if (System.IO.File.Exists(dbPath))
            {
                var ts = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
                var safety = Path.Combine(backupsDir, $"before_restore_{ts}.db");
                try { System.IO.File.Copy(dbPath, safety, overwrite: false); } catch { /* best effort */ }
            }

            System.IO.File.Copy(backupPath, dbPath, overwrite: true);
            return Ok(new { message = "Restauración completada. Si notas errores, reinicia el servicio.", restored = fileName });
        }
        catch (IOException)
        {
            return Conflict(new { message = "No se pudo restaurar: la base de datos está en uso. Detén/reinicia el backend y vuelve a intentar." });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "Error al restaurar: " + ex.Message });
        }
    }

    /// <summary>
    /// Vacía la BD de días de ejecución, turnos, predicciones, etc. Los usuarios se mantienen.
    /// </summary>
    [HttpPost("clean")]
    public async Task<IActionResult> Clean()
    {
        var tokens = await _db.AuthTokens.ToListAsync();
        _db.AuthTokens.RemoveRange(tokens);
        var shifts = await _db.ShiftFeedbacks.ToListAsync();
        _db.ShiftFeedbacks.RemoveRange(shifts);
        var days = await _db.ExecutionDays.ToListAsync();
        _db.ExecutionDays.RemoveRange(days);
        await _db.WeeklyPredictions.ExecuteDeleteAsync();
        await _db.Recommendations.ExecuteDeleteAsync();
        await _db.EstimacionesCaches.ExecuteDeleteAsync();
        await _db.DetectedPatterns.ExecuteDeleteAsync();
        await _db.Events.ExecuteDeleteAsync();
        await _db.FacturacionObjetivoSemanas.ExecuteDeleteAsync();
        await _db.SaveChangesAsync();
        return Ok(new { message = "Base de datos limpiada. Los usuarios se mantienen." });
    }

    private string GetBackupsDir() => Path.Combine(_env.ContentRootPath, "backups");

    private string GetDbFilePath()
    {
        var cs = _db.Database.GetDbConnection().ConnectionString;
        try
        {
            var b = new SqliteConnectionStringBuilder(cs);
            var ds = b.DataSource;
            if (string.IsNullOrWhiteSpace(ds))
                return Path.Combine(_env.ContentRootPath, "manageros.db");
            return Path.IsPathRooted(ds) ? ds : Path.Combine(_env.ContentRootPath, ds);
        }
        catch
        {
            // Fallback
            return Path.Combine(_env.ContentRootPath, "manageros.db");
        }
    }
}

public class RestoreRequest
{
    public string? Path { get; set; }
}
