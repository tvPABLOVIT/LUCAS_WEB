using LucasWeb.Api.Data;
using LucasWeb.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Globalization;

namespace LucasWeb.Api.Controllers;

[ApiController]
[Route("api/weather")]
[Authorize]
public class WeatherController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IWeatherService _weather;
    private readonly IGeocodingService _geocoding;
    private readonly ILogger<WeatherController> _logger;

    public WeatherController(AppDbContext db, IWeatherService weather, IGeocodingService geocoding, ILogger<WeatherController> logger)
    {
        _db = db;
        _weather = weather;
        _geocoding = geocoding;
        _logger = logger;
    }

    /// <summary>
    /// Geocodifica una dirección y devuelve lat/lon para mostrar en Configuración.
    /// </summary>
    [HttpGet("geocode")]
    [Authorize(Roles = "admin,manager,master")]
    public async Task<IActionResult> Geocode([FromQuery] string? address, [FromQuery] string? countryCode = null)
    {
        if (string.IsNullOrWhiteSpace(address) || address.Trim().Length < 2)
            return BadRequest(new { message = "Indica una dirección (mín. 2 caracteres)." });
        var (lat, lon) = await _geocoding.GetCoordinatesAsync(address.Trim(), string.IsNullOrWhiteSpace(countryCode) ? null : countryCode.Trim());
        if (!lat.HasValue || !lon.HasValue)
            return Ok(new { lat = (decimal?)null, lon = (decimal?)null, message = "No se encontraron coordenadas para esta dirección." });
        var inv = System.Globalization.CultureInfo.InvariantCulture;
        return Ok(new { lat = lat.Value.ToString(inv), lon = lon.Value.ToString(inv) });
    }

    /// <summary>
    /// Indica si hay coordenadas configuradas (para diagnóstico). No expone los valores.
    /// </summary>
    [HttpGet("has-location")]
    [Authorize(Roles = "admin,manager,master,user")]
    public async Task<IActionResult> HasLocation()
    {
        var latS = await _db.Settings.AsNoTracking().Where(s => s.Key == "LatRestaurante").Select(s => s.Value).FirstOrDefaultAsync();
        var lonS = await _db.Settings.AsNoTracking().Where(s => s.Key == "LonRestaurante").Select(s => s.Value).FirstOrDefaultAsync();
        var hasLat = !string.IsNullOrWhiteSpace(latS) && decimal.TryParse(latS.Replace(",", "."), NumberStyles.Any, CultureInfo.InvariantCulture, out _);
        var hasLon = !string.IsNullOrWhiteSpace(lonS) && decimal.TryParse(lonS.Replace(",", "."), NumberStyles.Any, CultureInfo.InvariantCulture, out _);
        return Ok(new { hasLocation = hasLat && hasLon });
    }

    /// <summary>
    /// Obtiene clima para una fecha (día + por turno). Usa LatRestaurante/LonRestaurante de Configuración.
    /// </summary>
    [HttpGet("for-date")]
    [Authorize(Roles = "admin,manager,master,user")]
    public async Task<IActionResult> GetWeatherForDate([FromQuery] string? date)
    {
        if (string.IsNullOrWhiteSpace(date) || !DateTime.TryParse(date, out var d))
            return BadRequest(new { message = "Fecha inválida (yyyy-MM-dd)" });
        var inv = CultureInfo.InvariantCulture;
        decimal? lat = null;
        decimal? lon = null;
        var latS = await _db.Settings.AsNoTracking().Where(s => s.Key == "LatRestaurante").Select(s => s.Value).FirstOrDefaultAsync();
        var lonS = await _db.Settings.AsNoTracking().Where(s => s.Key == "LonRestaurante").Select(s => s.Value).FirstOrDefaultAsync();
        if (!string.IsNullOrWhiteSpace(latS) && decimal.TryParse(latS.Replace(",", "."), NumberStyles.Any, inv, out var la)) lat = la;
        if (!string.IsNullOrWhiteSpace(lonS) && decimal.TryParse(lonS.Replace(",", "."), NumberStyles.Any, inv, out var lo)) lon = lo;
        if (!lat.HasValue || !lon.HasValue)
            return Ok(new { day = (object?)null, shifts = Array.Empty<object>(), reason = "no_location", message = "Configura latitud y longitud del restaurante en Configuración." });

        var dayStart = d.Date;
        var dayEnd = d.Date;
        var weatherList = await _weather.GetWeatherForRangeAsync(dayStart, dayEnd, lat, lon);
        var shiftList = await _weather.GetShiftWeatherForRangeAsync(dayStart, dayEnd, lat, lon);
        var dayInfo = weatherList.FirstOrDefault(w => w.Date.Date == dayStart);
        object? dayObj = null;
        if (dayInfo != null)
            dayObj = new { weather_code = dayInfo.WeatherCode, weather_temp_max = dayInfo.TempMax, weather_temp_min = dayInfo.TempMin, weather_precip_mm = dayInfo.PrecipitationSumMm, weather_wind_max_kmh = dayInfo.WindSpeedMaxKmh };
        var shiftsArr = shiftList
            .Where(s => s.Date.Date == dayStart)
            .Select(s => new { shift_name = s.ShiftName, weather_code = s.WeatherCode, weather_temp_avg = s.TempAvg, weather_precip_mm = s.PrecipMm, weather_wind_max_kmh = s.WindMaxKmh })
            .ToArray<object>();
        // Si hay coordenadas pero no datos (ej. API externa falló o fecha fuera de rango), indicar reason
        var reason = (dayObj == null && shiftsArr.Length == 0) ? "no_data" : (string?)null;
        if (reason == "no_data")
            _logger.LogWarning("Clima: coordenadas OK pero Open-Meteo no devolvió datos para {Date}. Comprueba que la fecha esté dentro del rango (Forecast ~16 días desde hoy en el servidor).", date);
        return Ok(new { day = dayObj, shifts = shiftsArr, reason });
    }

    /// <summary>
    /// Rellena clima para días existentes (no feedback-only) en el rango [hoy-days+1 .. hoy].
    /// Usa LatRestaurante/LonRestaurante si existen; si no, no hace cambios.
    /// </summary>
    [HttpPost("backfill")]
    [Authorize(Roles = "admin,manager,master")]
    public async Task<IActionResult> Backfill([FromQuery] int days = 90, [FromQuery] bool force = false)
    {
        if (days < 1) days = 1;
        if (days > 3650) days = 3650; // hasta 10 años (se procesa por bloques)

        var inv = CultureInfo.InvariantCulture;
        decimal? lat = null;
        decimal? lon = null;
        var latS = await _db.Settings.AsNoTracking().Where(s => s.Key == "LatRestaurante").Select(s => s.Value).FirstOrDefaultAsync();
        var lonS = await _db.Settings.AsNoTracking().Where(s => s.Key == "LonRestaurante").Select(s => s.Value).FirstOrDefaultAsync();
        if (!string.IsNullOrWhiteSpace(latS) && decimal.TryParse(latS.Replace(",", "."), NumberStyles.Any, inv, out var la)) lat = la;
        if (!string.IsNullOrWhiteSpace(lonS) && decimal.TryParse(lonS.Replace(",", "."), NumberStyles.Any, inv, out var lo)) lon = lo;

        if (!lat.HasValue || !lon.HasValue)
            return BadRequest(new { message = "Falta ubicación. Configure LatRestaurante y LonRestaurante en Settings." });

        // Por defecto incluimos hoy; si el usuario quiere solo días "cerrados", que pida days hasta ayer.
        var end = DateTime.UtcNow.Date;
        var start = end.AddDays(-(days - 1));

        // Turnos a actualizar (misma ventana de días)
        var shiftsToUpdate = await _db.ShiftFeedbacks
            .Include(s => s.ExecutionDay)
            .Where(s => s.ExecutionDay != null && !s.ExecutionDay.IsFeedbackOnly && s.ExecutionDay.Date >= start && s.ExecutionDay.Date <= end)
            .OrderBy(s => s.ExecutionDay.Date)
            .ToListAsync();

        if (!force)
        {
            shiftsToUpdate = shiftsToUpdate
                .Where(s => !(s.WeatherCode.HasValue &&
                              s.WeatherTempAvg.HasValue &&
                              s.WeatherPrecipMm.HasValue &&
                              s.WeatherWindMaxKmh.HasValue))
                .ToList();
        }

        var daysToUpdate = await _db.ExecutionDays
            .Where(d => !d.IsFeedbackOnly && d.Date >= start && d.Date <= end)
            .OrderBy(d => d.Date)
            .ToListAsync();

        if (!force)
        {
            daysToUpdate = daysToUpdate
                .Where(d => !(d.WeatherCode.HasValue &&
                              d.WeatherTempMax.HasValue &&
                              d.WeatherTempMin.HasValue &&
                              d.WeatherPrecipMm.HasValue &&
                              d.WeatherWindMaxKmh.HasValue))
                .ToList();
        }

        if (daysToUpdate.Count == 0 && shiftsToUpdate.Count == 0)
            return Ok(new { message = "Clima ya completo (no hay días/turnos pendientes).", updatedDays = 0, updatedShifts = 0, start = start.ToString("yyyy-MM-dd"), end = end.ToString("yyyy-MM-dd") });

        var updatedDays = 0;
        var updatedShifts = 0;
        // Procesar en bloques para rangos largos (Open-Meteo recomienda rangos razonables)
        var chunkSize = 31;
        for (var chunkStart = start.Date; chunkStart <= end.Date; chunkStart = chunkStart.AddDays(chunkSize))
        {
            var chunkEnd = chunkStart.AddDays(chunkSize - 1);
            if (chunkEnd > end.Date) chunkEnd = end.Date;

            var weatherList = await _weather.GetWeatherForRangeAsync(chunkStart, chunkEnd, lat, lon);
            var shiftWeatherList = await _weather.GetShiftWeatherForRangeAsync(chunkStart, chunkEnd, lat, lon);
            if (weatherList.Count == 0 && shiftWeatherList.Count == 0) continue;

            var byDate = weatherList
                .Where(w => w.Date != DateTime.MinValue)
                .ToDictionary(w => w.Date.Date, w => w);

            var byDateShift = shiftWeatherList
                .Where(w => w.Date != DateTime.MinValue && !string.IsNullOrWhiteSpace(w.ShiftName))
                .GroupBy(w => new { D = w.Date.Date, S = w.ShiftName.Trim() })
                .ToDictionary(g => (g.Key.D, g.Key.S), g => g.First());

            foreach (var d in daysToUpdate)
            {
                if (d.Date.Date < chunkStart || d.Date.Date > chunkEnd) continue;
                if (!byDate.TryGetValue(d.Date.Date, out var w)) continue;

                var changed = false;
                if (d.WeatherCode != w.WeatherCode) { d.WeatherCode = w.WeatherCode; changed = true; }
                if (w.TempMax.HasValue && d.WeatherTempMax != w.TempMax.Value) { d.WeatherTempMax = w.TempMax.Value; changed = true; }
                if (w.TempMin.HasValue && d.WeatherTempMin != w.TempMin.Value) { d.WeatherTempMin = w.TempMin.Value; changed = true; }
                if (w.PrecipitationSumMm.HasValue && d.WeatherPrecipMm != w.PrecipitationSumMm.Value) { d.WeatherPrecipMm = w.PrecipitationSumMm.Value; changed = true; }
                if (w.WindSpeedMaxKmh.HasValue && d.WeatherWindMaxKmh != w.WindSpeedMaxKmh.Value) { d.WeatherWindMaxKmh = w.WindSpeedMaxKmh.Value; changed = true; }

                // Compatibilidad: WeatherTemp se usa en UI como resumen.
                var tempRep = w.TempMax ?? w.TempMin;
                if (tempRep.HasValue && d.WeatherTemp != tempRep.Value) { d.WeatherTemp = tempRep.Value; changed = true; }

                if (changed) updatedDays++;
            }

            foreach (var s in shiftsToUpdate)
            {
                var date = s.ExecutionDay.Date.Date;
                if (date < chunkStart || date > chunkEnd) continue;
                var shiftName = (s.ShiftName ?? "").Trim();
                if (string.IsNullOrWhiteSpace(shiftName)) continue;
                if (!byDateShift.TryGetValue((date, shiftName), out var sw)) continue;

                var changed = false;
                if (s.WeatherCode != sw.WeatherCode) { s.WeatherCode = sw.WeatherCode; changed = true; }
                if (sw.TempAvg.HasValue && s.WeatherTempAvg != sw.TempAvg.Value) { s.WeatherTempAvg = sw.TempAvg.Value; changed = true; }
                if (sw.PrecipMm.HasValue && s.WeatherPrecipMm != sw.PrecipMm.Value) { s.WeatherPrecipMm = sw.PrecipMm.Value; changed = true; }
                if (sw.WindMaxKmh.HasValue && s.WeatherWindMaxKmh != sw.WindMaxKmh.Value) { s.WeatherWindMaxKmh = sw.WindMaxKmh.Value; changed = true; }
                if (changed) updatedShifts++;
            }
        }

        if (updatedDays > 0 || updatedShifts > 0) await _db.SaveChangesAsync();
        return Ok(new { message = "Clima actualizado.", updatedDays, updatedShifts, start = start.ToString("yyyy-MM-dd"), end = end.ToString("yyyy-MM-dd"), force });
    }
}

