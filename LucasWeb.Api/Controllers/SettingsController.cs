using LucasWeb.Api.Data;
using LucasWeb.Api.Models;
using LucasWeb.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.Text.Json;

namespace LucasWeb.Api.Controllers;

[ApiController]
[Route("api/settings")]
[Authorize]
public class SettingsController : ControllerBase
{
    /// <summary>Roles que pueden modificar configuración (GET permitido a cualquier autenticado).</summary>
    private const string RolesCanPatch = "admin,manager,master";

    private static readonly string[] AllowedKeys = {
        "ProductividadIdealEurHora", "HorasPorTurno", "CostePersonalPorHora", "HorasSemanalesContrato", "FacturacionObjetivoSemanal",
        "DescuentoFacturacionManualPorcentaje",
        "NombreRestaurante", "DireccionRestaurante", "LatRestaurante", "LonRestaurante", "CountryCode",
        "Empleados",
        "GoogleSheetsUrl", "GoogleCredentialsPath", "GeminiApiKey", "WeatherApiKey",
        "BackendUrl", "UsarTunnelCloudflared", "QuickTunnelUrl", "TunnelToken",
        "PrediccionConservadoraFactor"
    };

    private readonly AppDbContext _db;
    private readonly IGeocodingService _geocoding;

    public SettingsController(AppDbContext db, IGeocodingService geocoding)
    {
        _db = db;
        _geocoding = geocoding;
    }

    [HttpGet]
    public async Task<ActionResult<Dictionary<string, string>>> Get()
    {
        await EnsureSettingsTableAsync();
        var list = await _db.Settings.Where(s => AllowedKeys.Contains(s.Key)).ToListAsync();
        var dict = list.ToDictionary(s => s.Key, s => s.Value);
        foreach (var key in AllowedKeys)
            if (!dict.ContainsKey(key)) dict[key] = "";
        return Ok(dict);
    }

    [HttpPatch]
    [Authorize(Roles = RolesCanPatch)]
    public async Task<IActionResult> Patch([FromBody] Dictionary<string, JsonElement> body)
    {
        if (body == null) return BadRequest();
        await EnsureSettingsTableAsync();
        await EnsureFacturacionObjetivoTableAsync();

        var kvObjetivo = body.FirstOrDefault(kv => string.Equals(kv.Key, "FacturacionObjetivoSemanal", StringComparison.OrdinalIgnoreCase));
        string? oldObjetivo = null;
        string? oldLastWeek = null;
        if (kvObjetivo.Key != null)
        {
            var existingObjetivo = await _db.Settings.FindAsync("FacturacionObjetivoSemanal");
            var existingLastWeek = await _db.Settings.FindAsync("FacturacionObjetivoSemanal_LastWeekStart");
            oldObjetivo = existingObjetivo?.Value;
            oldLastWeek = existingLastWeek?.Value;
        }

        var now = DateTime.UtcNow;
        foreach (var kv in body)
        {
            var keyToUse = AllowedKeys.FirstOrDefault(k => string.Equals(k, kv.Key, StringComparison.OrdinalIgnoreCase));
            if (keyToUse == null) continue;
            var value = kv.Value.ValueKind == JsonValueKind.Number ? kv.Value.GetRawText() : (kv.Value.ValueKind == JsonValueKind.String ? kv.Value.GetString() : kv.Value.GetRawText());
            if (value == null) value = "";
            var existing = await _db.Settings.FindAsync(keyToUse);
            if (existing != null)
            {
                existing.Value = value;
                existing.UpdatedAt = now;
            }
            else
            {
                _db.Settings.Add(new Setting { Key = keyToUse, Value = value, UpdatedAt = now });
            }
        }

        if (kvObjetivo.Key != null && !string.IsNullOrWhiteSpace(oldObjetivo) && decimal.TryParse(oldObjetivo.Replace(",", "."), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var oldTargetEur) && !string.IsNullOrWhiteSpace(oldLastWeek) && DateTime.TryParse(oldLastWeek, out var oldWeekStart))
        {
            var currentWeekStart = GetMonday(now.Date);
            oldWeekStart = oldWeekStart.Date;
            for (var w = oldWeekStart; w < currentWeekStart; w = w.AddDays(7))
            {
                var existing = await _db.FacturacionObjetivoSemanas.FindAsync(w);
                if (existing != null)
                    existing.TargetRevenue = oldTargetEur;
                else
                    _db.FacturacionObjetivoSemanas.Add(new FacturacionObjetivoSemana { WeekStart = w, TargetRevenue = oldTargetEur });
            }
            var lastWeekSetting = await _db.Settings.FindAsync("FacturacionObjetivoSemanal_LastWeekStart");
            var currentWeekStr = currentWeekStart.ToString("yyyy-MM-dd");
            if (lastWeekSetting != null)
                lastWeekSetting.Value = currentWeekStr;
            else
                _db.Settings.Add(new Setting { Key = "FacturacionObjetivoSemanal_LastWeekStart", Value = currentWeekStr, UpdatedAt = now });
        }
        else if (kvObjetivo.Key != null)
        {
            var currentWeekStart = GetMonday(now.Date);
            var lastWeekSetting = await _db.Settings.FindAsync("FacturacionObjetivoSemanal_LastWeekStart");
            var currentWeekStr = currentWeekStart.ToString("yyyy-MM-dd");
            if (lastWeekSetting != null)
                lastWeekSetting.Value = currentWeekStr;
            else
                _db.Settings.Add(new Setting { Key = "FacturacionObjetivoSemanal_LastWeekStart", Value = currentWeekStr, UpdatedAt = now });
        }

        // Si hay dirección pero no coordenadas, geocodificar para obtener lat/lon (clima)
        var address = GetStringFromBody(body, "DireccionRestaurante");
        var latSent = GetStringFromBody(body, "LatRestaurante");
        var lonSent = GetStringFromBody(body, "LonRestaurante");
        var countryCode = GetStringFromBody(body, "CountryCode");
        if (!string.IsNullOrWhiteSpace(address) && (string.IsNullOrWhiteSpace(latSent) || string.IsNullOrWhiteSpace(lonSent)))
        {
            var (lat, lon) = await _geocoding.GetCoordinatesAsync(address.Trim(), string.IsNullOrWhiteSpace(countryCode) ? null : countryCode.Trim());
            if (lat.HasValue && lon.HasValue)
            {
                var inv = CultureInfo.InvariantCulture;
                await SetSettingAsync("LatRestaurante", lat.Value.ToString(inv), now);
                await SetSettingAsync("LonRestaurante", lon.Value.ToString(inv), now);
            }
        }

        await _db.SaveChangesAsync();
        return Ok();
    }

    private static string? GetStringFromBody(Dictionary<string, JsonElement> body, string key)
    {
        var kv = body.FirstOrDefault(b => string.Equals(b.Key, key, StringComparison.OrdinalIgnoreCase));
        if (kv.Key == null) return null;
        var v = kv.Value.ValueKind == JsonValueKind.Number ? kv.Value.GetRawText() : kv.Value.GetString();
        return v?.Trim() ?? "";
    }

    private async Task SetSettingAsync(string key, string value, DateTime updatedAt)
    {
        var existing = await _db.Settings.FindAsync(key);
        if (existing != null)
        {
            existing.Value = value;
            existing.UpdatedAt = updatedAt;
        }
        else
            _db.Settings.Add(new Setting { Key = key, Value = value, UpdatedAt = updatedAt });
    }

    private static DateTime GetMonday(DateTime d)
    {
        var diff = (7 + (d.DayOfWeek - DayOfWeek.Monday)) % 7;
        return d.AddDays(-diff).Date;
    }

    private async Task EnsureFacturacionObjetivoTableAsync()
    {
        try
        {
            await _db.Database.ExecuteSqlRawAsync(
                "CREATE TABLE IF NOT EXISTS \"FacturacionObjetivoSemanas\" (\"WeekStart\" TEXT NOT NULL PRIMARY KEY, \"TargetRevenue\" REAL NOT NULL);");
        }
        catch { }
    }

    private async Task EnsureSettingsTableAsync()
    {
        try
        {
            await _db.Database.ExecuteSqlRawAsync(
                "CREATE TABLE IF NOT EXISTS \"Settings\" (\"Key\" TEXT NOT NULL PRIMARY KEY, \"Value\" TEXT NOT NULL, \"UpdatedAt\" TEXT NOT NULL);");
        }
        catch
        {
            // Ignorar si ya existe o no es SQLite
        }
    }
}
