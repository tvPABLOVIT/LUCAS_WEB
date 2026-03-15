using LucasWeb.Api.Data;
using LucasWeb.Api.Models;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.Text.Json;

namespace LucasWeb.Api.Services;

/// <summary>
/// Asegura que exista una predicción guardada para la semana indicada (solo semana actual o siguiente).
/// Si no existe, la genera (en vivo), enriquece con clima/festivos/eventos, rellena personal y la guarda.
/// </summary>
public class EnsurePredictionForWeekService
{
    private readonly AppDbContext _db;
    private readonly NextWeekPredictionService _livePrediction;
    private readonly PredictionEnrichmentService _enrichment;
    private readonly StaffByTurnoPredictionService _staffByTurno;

    public EnsurePredictionForWeekService(
        AppDbContext db,
        NextWeekPredictionService livePrediction,
        PredictionEnrichmentService enrichment,
        StaffByTurnoPredictionService staffByTurno)
    {
        _db = db;
        _livePrediction = livePrediction;
        _enrichment = enrichment;
        _staffByTurno = staffByTurno;
    }

    /// <summary>
    /// Si la semana es la actual o la siguiente y no hay predicción guardada, la genera y la guarda.
    /// Devuelve true si ahora hay predicción disponible (ya existía o se acaba de crear).
    /// </summary>
    public async Task<bool> EnsurePredictionForWeekAsync(DateTime monday)
    {
        var today = DateTime.UtcNow.Date;
        var currentWeekMonday = GetMonday(today);
        var nextMonday = NextWeekPredictionService.GetNextMonday(today);
        monday = GetMonday(monday);

        if (monday != currentWeekMonday && monday != nextMonday)
            return false;

        var existing = await _db.WeeklyPredictions
            .FirstOrDefaultAsync(p => p.WeekStartMonday == monday);
        if (existing != null && !string.IsNullOrWhiteSpace(existing.DailyPredictionsJson))
            return true;

        var (_, total, dailyJson) = await _livePrediction.ComputeLivePredictionAsync(monday);
        if (string.IsNullOrWhiteSpace(dailyJson))
            return false;

        decimal? lat = null, lon = null;
        var latSetting = await _db.Settings.AsNoTracking().FirstOrDefaultAsync(s => s.Key == "LatRestaurante");
        var lonSetting = await _db.Settings.AsNoTracking().FirstOrDefaultAsync(s => s.Key == "LonRestaurante");
        if (latSetting != null && decimal.TryParse(latSetting.Value?.Replace(",", "."), NumberStyles.Any, CultureInfo.InvariantCulture, out var la)) lat = la;
        if (lonSetting != null && decimal.TryParse(lonSetting.Value?.Replace(",", "."), NumberStyles.Any, CultureInfo.InvariantCulture, out var lo)) lon = lo;
        var ccSetting = await _db.Settings.AsNoTracking().FirstOrDefaultAsync(s => s.Key == "CountryCode");
        var countryCode = ccSetting?.Value?.Trim() ?? "ES";

        dailyJson = await _enrichment.EnrichDailyPredictionsAsync(dailyJson, monday, lat, lon, countryCode);
        if (string.IsNullOrWhiteSpace(dailyJson))
            return false;

        var prod = await GetProductividadObjetivoAsync();
        var horasSetting = await _db.Settings.AsNoTracking().FirstOrDefaultAsync(s => s.Key == "HorasPorTurno");
        var horas = 4m;
        if (horasSetting != null && decimal.TryParse(horasSetting.Value?.Replace(",", "."), NumberStyles.Any, CultureInfo.InvariantCulture, out var hh)) horas = hh;
        dailyJson = await _staffByTurno.FillStaffRecommendationsJsonAsync(monday, dailyJson, prod, horas) ?? dailyJson;

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

        if (existing == null)
        {
            existing = new WeeklyPrediction
            {
                Id = Guid.NewGuid(),
                WeekStartMonday = monday,
                PredictedRevenue = totalRevenue,
                DailyPredictionsJson = dailyJson,
                CreatedAt = DateTime.UtcNow
            };
            _db.WeeklyPredictions.Add(existing);
        }
        else
        {
            existing.PredictedRevenue = totalRevenue;
            existing.DailyPredictionsJson = dailyJson;
        }
        await _db.SaveChangesAsync();
        return true;
    }

    private static DateTime GetMonday(DateTime d)
    {
        var diff = (7 + (d.DayOfWeek - DayOfWeek.Monday)) % 7;
        return d.AddDays(-diff).Date;
    }

    private async Task<decimal> GetProductividadObjetivoAsync()
    {
        const decimal WeeksPerMonth = 4.33m;
        var factObjMensual = await _db.Settings.AsNoTracking().FirstOrDefaultAsync(s => s.Key == "FacturacionObjetivoMensual");
        decimal? factObj = null;
        if (factObjMensual != null && decimal.TryParse((factObjMensual.Value ?? "").Replace(",", "."), System.Globalization.NumberStyles.Any, CultureInfo.InvariantCulture, out var foMonthly) && foMonthly > 0)
            factObj = foMonthly / WeeksPerMonth;
        if (!factObj.HasValue)
        {
            var factObjS = await _db.Settings.AsNoTracking().FirstOrDefaultAsync(s => s.Key == "FacturacionObjetivoSemanal");
            if (factObjS != null && decimal.TryParse((factObjS.Value ?? "").Replace(",", "."), System.Globalization.NumberStyles.Any, CultureInfo.InvariantCulture, out var fo) && fo > 0) factObj = fo;
        }
        var totalHoras = await GetTotalHorasSemanalesContratoAsync();
        if (factObj.HasValue && factObj.Value > 0 && totalHoras > 0)
            return factObj.Value / totalHoras;
        var prodObjS = await _db.Settings.AsNoTracking().FirstOrDefaultAsync(s => s.Key == "ProductividadIdealEurHora");
        if (prodObjS != null && decimal.TryParse((prodObjS.Value ?? "").Replace(",", "."), System.Globalization.NumberStyles.Any, CultureInfo.InvariantCulture, out var po) && po > 0) return po;
        return 50m;
    }

    private async Task<decimal> GetTotalHorasSemanalesContratoAsync()
    {
        var horasSetting = await _db.Settings.AsNoTracking().Where(s => s.Key == "HorasSemanalesContrato").Select(s => s.Value).FirstOrDefaultAsync();
        if (!string.IsNullOrWhiteSpace(horasSetting) && decimal.TryParse(horasSetting.Replace(",", "."), System.Globalization.NumberStyles.Any, CultureInfo.InvariantCulture, out var h) && h > 0)
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
