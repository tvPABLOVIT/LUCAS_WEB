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
[Route("api/predictions")]
[Authorize]
public class PredictionsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly NextWeekPredictionService _livePrediction;
    private readonly PredictionEnrichmentService _enrichment;
    private readonly StaffByTurnoPredictionService _staffByTurno;
    private readonly EnsurePredictionForWeekService _ensurePrediction;

    public PredictionsController(AppDbContext db, NextWeekPredictionService livePrediction, PredictionEnrichmentService enrichment, StaffByTurnoPredictionService staffByTurno, EnsurePredictionForWeekService ensurePrediction)
    {
        _db = db;
        _livePrediction = livePrediction;
        _enrichment = enrichment;
        _staffByTurno = staffByTurno;
        _ensurePrediction = ensurePrediction;
    }

    /// <summary>Historial de precisión: últimas N semanas evaluadas (predicción vs real) para ver evolución del error y sesgo.</summary>
    [HttpGet("accuracy-history")]
    public async Task<IActionResult> GetAccuracyHistory([FromQuery] int limit = 20)
    {
        var take = Math.Clamp(limit, 1, 52);
        var list = await _db.WeeklyPredictions
            .AsNoTracking()
            .Where(p => p.CompletedAt != null && p.PredictedRevenue.HasValue && p.PredictedRevenue > 0)
            .OrderByDescending(p => p.WeekStartMonday)
            .Take(take)
            .Select(p => new { p.WeekStartMonday, p.PredictedRevenue, p.ActualRevenue, p.AccuracyMetricsJson, p.StaffAccuracyJson })
            .ToListAsync();

        var items = new List<object>();
        foreach (var p in list)
        {
            decimal? errorPct = null;
            decimal? accuracyPct = null;
            if (!string.IsNullOrWhiteSpace(p.AccuracyMetricsJson))
            {
                try
                {
                    var doc = JsonDocument.Parse(p.AccuracyMetricsJson);
                    if (doc.RootElement.TryGetProperty("overall_error_percent", out var ep)) errorPct = ep.GetDecimal();
                    if (doc.RootElement.TryGetProperty("accuracy_percent", out var ap)) accuracyPct = ap.GetDecimal();
                }
                catch { }
            }
            decimal? staffSalaMae = null;
            decimal? staffExactMatchPct = null;
            if (!string.IsNullOrWhiteSpace(p.StaffAccuracyJson))
            {
                try
                {
                    var staffDoc = JsonDocument.Parse(p.StaffAccuracyJson);
                    if (staffDoc.RootElement.TryGetProperty("sala_mae", out var sm)) staffSalaMae = sm.GetDecimal();
                    if (staffDoc.RootElement.TryGetProperty("exact_match_pct", out var em)) staffExactMatchPct = em.GetDecimal();
                }
                catch { }
            }
            items.Add(new
            {
                weekStartMonday = p.WeekStartMonday.ToString("yyyy-MM-dd"),
                predictedRevenue = p.PredictedRevenue,
                actualRevenue = p.ActualRevenue,
                errorPercent = errorPct,
                accuracyPercent = accuracyPct,
                staffSalaMae = staffSalaMae,
                staffExactMatchPct = staffExactMatchPct
            });
        }
        return Ok(new { weeks = items });
    }

    /// <summary>
    /// Devuelve la predicción guardada para una semana. Si la semana es la actual o la siguiente y no existe, la genera y guarda (para que al empezar el lunes ya esté guardada y se use en comparativas).
    /// </summary>
    [HttpGet("by-week")]
    public async Task<IActionResult> GetByWeek([FromQuery] string weekStart)
    {
        if (string.IsNullOrWhiteSpace(weekStart) || !DateTime.TryParse(weekStart, out var d))
            return BadRequest(new { message = "weekStart inválido (yyyy-MM-dd)." });

        var monday = d.Date;
        // Normalizar a lunes (si entra una fecha no-lunes, se ajusta hacia el lunes de esa semana).
        var diff = (7 + (monday.DayOfWeek - DayOfWeek.Monday)) % 7;
        monday = monday.AddDays(-diff).Date;

        await _ensurePrediction.EnsurePredictionForWeekAsync(monday);

        var pred = await _db.WeeklyPredictions.AsNoTracking().FirstOrDefaultAsync(p => p.WeekStartMonday == monday);
        if (pred == null || string.IsNullOrWhiteSpace(pred.DailyPredictionsJson))
            return Ok(new
            {
                weekStartMonday = monday.ToString("yyyy-MM-dd"),
                isSavedPrediction = false,
                totalRevenue = (decimal?)null,
                dailyPredictionsJson = (string?)null,
                actualRevenue = (decimal?)null,
                completedAt = (string?)null,
                accuracyMetricsJson = (string?)null,
                createdAt = (string?)null
            });

        decimal? totalRevenue = pred.PredictedRevenue;
        var dailyJson = pred.DailyPredictionsJson;
        try
        {
            var days = JsonSerializer.Deserialize<JsonElement>(dailyJson);
            if (days.ValueKind == JsonValueKind.Array)
            {
                var sum = 0m;
                foreach (var day in days.EnumerateArray())
                {
                    if (day.TryGetProperty("revenue", out var r) || day.TryGetProperty("predictedRevenue", out r))
                        sum += r.GetDecimal();
                }
                if (sum > 0) totalRevenue = sum;
            }
        }
        catch { }

        var prod = await GetProductividadObjetivoAsync();
        var horasSetting = await _db.Settings.AsNoTracking().FirstOrDefaultAsync(s => s.Key == "HorasPorTurno");
        var horas = 4m;
        if (horasSetting != null && decimal.TryParse(horasSetting.Value?.Replace(",", "."), NumberStyles.Any, CultureInfo.InvariantCulture, out var hh)) horas = hh;
            dailyJson = await _staffByTurno.FillStaffRecommendationsJsonAsync(monday, dailyJson, prod, horas) ?? dailyJson;

        return Ok(new
        {
            weekStartMonday = monday.ToString("yyyy-MM-dd"),
            isSavedPrediction = true,
            totalRevenue,
            dailyPredictionsJson = dailyJson,
            actualRevenue = pred.ActualRevenue,
            completedAt = pred.CompletedAt?.ToString("yyyy-MM-dd"),
            accuracyMetricsJson = pred.AccuracyMetricsJson,
            createdAt = pred.CreatedAt.ToString("yyyy-MM-dd")
        });
    }

    [HttpGet("next-week")]
    public async Task<IActionResult> GetNextWeek([FromQuery] decimal? lat, [FromQuery] decimal? lon, [FromQuery] string? countryCode)
    {
        var nextMonday = NextWeekPredictionService.GetNextMonday(DateTime.UtcNow.Date);
        await _ensurePrediction.EnsurePredictionForWeekAsync(nextMonday);
        string? dailyJson = null;
        decimal? totalRevenue = null;
        string? historicalStatsJson = null;
        var isSavedPrediction = false;
        var pred = await _db.WeeklyPredictions.FirstOrDefaultAsync(p => p.WeekStartMonday == nextMonday);
        if (pred != null && !string.IsNullOrWhiteSpace(pred.DailyPredictionsJson))
        {
            try
            {
                var days = JsonSerializer.Deserialize<JsonElement>(pred.DailyPredictionsJson);
                var sum = 0m;
                if (days.ValueKind == JsonValueKind.Array)
                    foreach (var d in days.EnumerateArray())
                        if (d.TryGetProperty("revenue", out var r) || d.TryGetProperty("predictedRevenue", out r))
                            sum += r.GetDecimal();
                totalRevenue = sum <= 0 && pred.PredictedRevenue.HasValue ? pred.PredictedRevenue.Value : sum;
                dailyJson = pred.DailyPredictionsJson;
                historicalStatsJson = pred.HistoricalStatsJson;
                isSavedPrediction = true;
            }
            catch { }
        }
        if (dailyJson == null)
        {
            var (weekStart, total, daily) = await _livePrediction.ComputeLivePredictionAsync();
            if (daily == null)
                return Ok(new { weekStartMonday = weekStart.ToString("yyyy-MM-dd"), totalRevenue = (decimal?)null, isSavedPrediction = false, days = Array.Empty<object>() });
            dailyJson = daily;
            totalRevenue = total;
        }
        // Clima: siempre LatRestaurante/LonRestaurante de Configuración (sin fallback).
        if (!lat.HasValue || !lon.HasValue)
        {
            var latSetting = await _db.Settings.AsNoTracking().FirstOrDefaultAsync(s => s.Key == "LatRestaurante");
            var lonSetting = await _db.Settings.AsNoTracking().FirstOrDefaultAsync(s => s.Key == "LonRestaurante");
            if (latSetting != null && decimal.TryParse(latSetting.Value.Replace(",", "."), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var la)) lat = la;
            if (lonSetting != null && decimal.TryParse(lonSetting.Value.Replace(",", "."), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var lo)) lon = lo;
        }
        if (string.IsNullOrWhiteSpace(countryCode))
        {
            var cc = await _db.Settings.AsNoTracking().FirstOrDefaultAsync(s => s.Key == "CountryCode");
            if (cc != null && !string.IsNullOrWhiteSpace(cc.Value)) countryCode = cc.Value;
            else countryCode = "ES";
        }
        dailyJson = await _enrichment.EnrichDailyPredictionsAsync(dailyJson, nextMonday, lat, lon, countryCode);
        if (dailyJson != null)
        {
            try
            {
                var arr = JsonSerializer.Deserialize<JsonElement>(dailyJson);
                if (arr.ValueKind == JsonValueKind.Array)
                {
                    var sum = 0m;
                    foreach (var day in arr.EnumerateArray())
                    {
                        if (day.TryGetProperty("revenue", out var r)) sum += r.GetDecimal();
                        else if (day.TryGetProperty("predictedRevenue", out r)) sum += r.GetDecimal();
                    }
                    if (sum > 0) totalRevenue = sum;
                }
            }
            catch { }
            var prod = await GetProductividadObjetivoAsync();
            var horasSetting = await _db.Settings.AsNoTracking().FirstOrDefaultAsync(s => s.Key == "HorasPorTurno");
            var horas = 4m;
            if (horasSetting != null && decimal.TryParse(horasSetting.Value?.Replace(",", "."), NumberStyles.Any, CultureInfo.InvariantCulture, out var hh)) horas = hh;
            dailyJson = await _staffByTurno.FillStaffRecommendationsJsonAsync(nextMonday, dailyJson, prod, horas) ?? dailyJson;
        }
        return Ok(new
        {
            weekStartMonday = nextMonday.ToString("yyyy-MM-dd"),
            totalRevenue,
            isSavedPrediction,
            dailyPredictionsJson = dailyJson,
            historicalStatsJson
        });
    }

    /// <summary>Calcula la predicción de la semana siguiente (en vivo + clima, festivos, eventos) y la guarda en WeeklyPredictions.</summary>
    [HttpPost("next-week/save")]
    public async Task<IActionResult> SaveNextWeekPrediction()
    {
        var nextMonday = NextWeekPredictionService.GetNextMonday(DateTime.UtcNow.Date);
        var (_, total, dailyJson) = await _livePrediction.ComputeLivePredictionAsync();
        if (string.IsNullOrWhiteSpace(dailyJson))
            return Ok(new { saved = false, message = "No hay suficientes datos históricos para calcular la predicción." });

        decimal? lat = null, lon = null;
        var latSetting = await _db.Settings.AsNoTracking().FirstOrDefaultAsync(s => s.Key == "LatRestaurante");
        var lonSetting = await _db.Settings.AsNoTracking().FirstOrDefaultAsync(s => s.Key == "LonRestaurante");
        if (latSetting != null && decimal.TryParse(latSetting.Value?.Replace(",", "."), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var la)) lat = la;
        if (lonSetting != null && decimal.TryParse(lonSetting.Value?.Replace(",", "."), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var lo)) lon = lo;
        // Clima: solo coordenadas de Configuración (sin fallback).
        var ccSetting = await _db.Settings.AsNoTracking().FirstOrDefaultAsync(s => s.Key == "CountryCode");
        var countryCode = ccSetting?.Value?.Trim() ?? "ES";

        dailyJson = await _enrichment.EnrichDailyPredictionsAsync(dailyJson, nextMonday, lat, lon, countryCode);
        if (string.IsNullOrWhiteSpace(dailyJson))
            return Ok(new { saved = false, message = "Error al enriquecer la predicción." });

        var prod = await GetProductividadObjetivoAsync();
        var horasSetting = await _db.Settings.AsNoTracking().FirstOrDefaultAsync(s => s.Key == "HorasPorTurno");
        var horas = 4m;
        if (horasSetting != null && decimal.TryParse(horasSetting.Value?.Replace(",", "."), NumberStyles.Any, CultureInfo.InvariantCulture, out var hh)) horas = hh;
        dailyJson = await _staffByTurno.FillStaffRecommendationsJsonAsync(nextMonday, dailyJson, prod, horas) ?? dailyJson;

        decimal totalRevenue = 0;
        try
        {
            var arr = JsonSerializer.Deserialize<JsonElement>(dailyJson);
            if (arr.ValueKind == JsonValueKind.Array)
                foreach (var day in arr.EnumerateArray())
                {
                    if (day.TryGetProperty("revenue", out var r)) totalRevenue += r.GetDecimal();
                    else if (day.TryGetProperty("predictedRevenue", out r)) totalRevenue += r.GetDecimal();
                }
        }
        catch { }

        var pred = await _db.WeeklyPredictions.FirstOrDefaultAsync(p => p.WeekStartMonday == nextMonday);
        if (pred == null)
        {
            pred = new WeeklyPrediction
            {
                Id = Guid.NewGuid(),
                WeekStartMonday = nextMonday,
                PredictedRevenue = totalRevenue,
                DailyPredictionsJson = dailyJson,
                CreatedAt = DateTime.UtcNow
            };
            _db.WeeklyPredictions.Add(pred);
        }
        else
        {
            pred.PredictedRevenue = totalRevenue;
            pred.DailyPredictionsJson = dailyJson;
        }
        await _db.SaveChangesAsync();
        return Ok(new { saved = true, weekStartMonday = nextMonday.ToString("yyyy-MM-dd"), totalRevenue });
    }

    private async Task<decimal> GetProductividadObjetivoAsync()
    {
        const decimal WeeksPerMonth = 4.33m;
        var factObjMensual = await _db.Settings.AsNoTracking().FirstOrDefaultAsync(s => s.Key == "FacturacionObjetivoMensual");
        decimal? factObj = null;
        if (factObjMensual != null && decimal.TryParse((factObjMensual.Value ?? "").Replace(",", "."), NumberStyles.Any, CultureInfo.InvariantCulture, out var foMonthly) && foMonthly > 0)
            factObj = foMonthly / WeeksPerMonth;
        if (!factObj.HasValue)
        {
            var factObjS = await _db.Settings.AsNoTracking().FirstOrDefaultAsync(s => s.Key == "FacturacionObjetivoSemanal");
            if (factObjS != null && decimal.TryParse((factObjS.Value ?? "").Replace(",", "."), NumberStyles.Any, CultureInfo.InvariantCulture, out var fo) && fo > 0) factObj = fo;
        }
        var totalHoras = await GetTotalHorasSemanalesContratoAsync();
        if (factObj.HasValue && factObj.Value > 0 && totalHoras > 0)
            return factObj.Value / totalHoras;
        var prodObjS = await _db.Settings.AsNoTracking().FirstOrDefaultAsync(s => s.Key == "ProductividadIdealEurHora");
        if (prodObjS != null && decimal.TryParse((prodObjS.Value ?? "").Replace(",", "."), NumberStyles.Any, CultureInfo.InvariantCulture, out var po) && po > 0) return po;
        return 50m;
    }

    private async Task<decimal> GetTotalHorasSemanalesContratoAsync()
    {
        var horasSetting = await _db.Settings.AsNoTracking().Where(s => s.Key == "HorasSemanalesContrato").Select(s => s.Value).FirstOrDefaultAsync();
        if (!string.IsNullOrWhiteSpace(horasSetting) && decimal.TryParse(horasSetting.Replace(",", "."), NumberStyles.Any, CultureInfo.InvariantCulture, out var h) && h > 0)
            return h;
        var empleadosJson = await _db.Settings.AsNoTracking().Where(s => s.Key == "Empleados").Select(s => s.Value).FirstOrDefaultAsync();
        if (string.IsNullOrWhiteSpace(empleadosJson)) return 0;
        try
        {
            using var doc = JsonDocument.Parse(empleadosJson);
            if (doc.RootElement.ValueKind != JsonValueKind.Array) return 0;
            decimal sum = 0;
            foreach (var el in doc.RootElement.EnumerateArray())
            {
                if (el.TryGetProperty("hours", out var hoursEl) && hoursEl.TryGetDecimal(out var hours))
                    sum += hours;
            }
            return sum;
        }
        catch { return 0; }
    }
}
