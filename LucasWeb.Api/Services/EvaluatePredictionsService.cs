using LucasWeb.Api.Data;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.Text.Json;

namespace LucasWeb.Api.Services;

/// <summary>Evalúa predicciones de semanas cerradas; actualiza bias y MAE por DOW en Settings (PredictionBiasJson, PredictionMaeJson). Usa ventana de últimas N evaluaciones por DOW para que el sistema se adapte a cambios recientes. Evalúa también personal recomendado vs real.</summary>
public class EvaluatePredictionsService : IEvaluatePredictionsService
{
    private const int BiasMaeWindowSize = PredictionBiasMaeWindow.DefaultWindowSize;

    private readonly AppDbContext _db;

    private static string NormalizeShift(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return "";
        var x = name.Trim().ToLowerInvariant().Replace("í", "i").Replace("á", "a");
        if (x.Contains("medio")) return "mediodia";
        if (x.Contains("tarde")) return "tarde";
        if (x.Contains("noche")) return "noche";
        return x;
    }

    public EvaluatePredictionsService(AppDbContext db) => _db = db;

    /// <summary>Domingo pasado = última semana completa.</summary>
    private static DateTime GetLastSunday(DateTime today)
    {
        var d = today.Date;
        while (d.DayOfWeek != DayOfWeek.Sunday) d = d.AddDays(-1);
        return d;
    }

    /// <summary>Lunes 0..6 Domingo.</summary>
    private static int Dow(DateTime d) => (int)d.DayOfWeek == 0 ? 6 : (int)d.DayOfWeek - 1;

    public async Task EvaluateLastWeekIfPendingAsync()
    {
        var today = DateTime.UtcNow.Date;
        var lastSunday = GetLastSunday(today);
        var lastMonday = lastSunday.AddDays(-6);
        var pred = await _db.WeeklyPredictions
            .FirstOrDefaultAsync(p => p.WeekStartMonday == lastMonday && p.CompletedAt == null);
        if (pred == null) return;

        var actualDays = await _db.ExecutionDays
            .Where(e => !e.IsFeedbackOnly && e.Date >= lastMonday && e.Date <= lastSunday)
            .Select(e => new { e.Date, e.TotalRevenue })
            .ToListAsync();
        var actualTotal = actualDays.Sum(d => d.TotalRevenue);
        var byDate = actualDays.ToDictionary(d => d.Date.Date, d => d.TotalRevenue);

        pred.ActualRevenue = actualTotal;
        pred.CompletedAt = DateTime.UtcNow;
        var errorPct = pred.PredictedRevenue.HasValue && pred.PredictedRevenue > 0
            ? Math.Abs(pred.PredictedRevenue.Value - actualTotal) / pred.PredictedRevenue.Value * 100
            : 0;
        var accuracy = Math.Max(0, 100 - errorPct);
        pred.AccuracyMetricsJson = JsonSerializer.Serialize(new
        {
            overall_error_percent = errorPct,
            accuracy_percent = accuracy,
            actual_revenue = actualTotal,
            predicted_revenue = pred.PredictedRevenue
        });

        pred.StaffAccuracyJson = await ComputeStaffAccuracyJsonAsync(lastMonday, lastSunday, pred.DailyPredictionsJson);

        if (string.IsNullOrWhiteSpace(pred.DailyPredictionsJson)) { await _db.SaveChangesAsync(); return; }
        List<JsonElement>? days;
        try { days = JsonSerializer.Deserialize<List<JsonElement>>(pred.DailyPredictionsJson); } catch { await _db.SaveChangesAsync(); return; }
        if (days == null || days.Count == 0) { await _db.SaveChangesAsync(); return; }

        await EnsureSettingsTableAsync();
        var biasJson = await GetSettingValueAsync("PredictionBiasJson");
        var maeJson = await GetSettingValueAsync("PredictionMaeJson");
        PredictionBiasMaeWindow.ParseBiasWithWindow(biasJson, out var bias, out var biasRecent);
        PredictionBiasMaeWindow.ParseMaeWithWindow(maeJson, out var mae, out var maeRecent);

        foreach (var d in days)
        {
            if (!d.TryGetProperty("date", out var dateEl)) continue;
            var dateStr = dateEl.GetString();
            if (string.IsNullOrEmpty(dateStr) || !DateTime.TryParse(dateStr, out var date)) continue;
            var dayDate = date.Date;
            var dow = Dow(dayDate);
            var predRev = 0m;
            if (d.TryGetProperty("revenue", out var r)) predRev = r.GetDecimal();
            else if (d.TryGetProperty("predictedRevenue", out r)) predRev = r.GetDecimal();
            if (predRev <= 0) continue;
            var realRev = byDate.TryGetValue(dayDate, out var rev) ? rev : 0;
            var errorPctDay = (predRev - realRev) / predRev * 100;
            var absError = Math.Abs(predRev - realRev);

            PredictionBiasMaeWindow.UpdateWindow(biasRecent[dow], (double)errorPctDay, BiasMaeWindowSize, out bias[dow]);
            PredictionBiasMaeWindow.UpdateWindow(maeRecent[dow], (double)absError, BiasMaeWindowSize, out mae[dow]);
        }

        await SetSettingValueAsync("PredictionBiasJson", PredictionBiasMaeWindow.SerializeBiasWithWindow(bias, biasRecent));
        await SetSettingValueAsync("PredictionMaeJson", PredictionBiasMaeWindow.SerializeMaeWithWindow(mae, maeRecent));
        await _db.SaveChangesAsync();
    }

    private async Task EnsureSettingsTableAsync()
    {
        try { await _db.Database.ExecuteSqlRawAsync("CREATE TABLE IF NOT EXISTS \"Settings\" (\"Key\" TEXT NOT NULL PRIMARY KEY, \"Value\" TEXT NOT NULL, \"UpdatedAt\" TEXT NOT NULL);"); } catch { }
    }

    private async Task<string?> GetSettingValueAsync(string key)
    {
        var s = await _db.Settings.AsNoTracking().FirstOrDefaultAsync(x => x.Key == key);
        return s?.Value;
    }

    private async Task SetSettingValueAsync(string key, string value)
    {
        var s = await _db.Settings.FirstOrDefaultAsync(x => x.Key == key);
        var now = DateTime.UtcNow;
        if (s != null) { s.Value = value; s.UpdatedAt = now; }
        else _db.Settings.Add(new Models.Setting { Key = key, Value = value, UpdatedAt = now });
    }

    /// <summary>Compara personal recomendado (en DailyPredictionsJson) con real (ShiftFeedbacks) y devuelve JSON de métricas.</summary>
    private async Task<string?> ComputeStaffAccuracyJsonAsync(DateTime lastMonday, DateTime lastSunday, string? dailyPredictionsJson)
    {
        if (string.IsNullOrWhiteSpace(dailyPredictionsJson)) return null;
        List<JsonElement>? days;
        try { days = JsonSerializer.Deserialize<List<JsonElement>>(dailyPredictionsJson); } catch { return null; }
        if (days == null || days.Count == 0) return null;

        var actualShifts = await _db.ShiftFeedbacks
            .AsNoTracking()
            .Where(s => s.ExecutionDay != null && !s.ExecutionDay.IsFeedbackOnly
                && s.ExecutionDay.Date >= lastMonday && s.ExecutionDay.Date <= lastSunday
                && s.StaffFloor >= 0 && s.StaffKitchen >= 0)
            .Select(s => new { s.ExecutionDay!.Date, s.ShiftName, s.StaffFloor, s.StaffKitchen })
            .ToListAsync();

        var byDateShift = new Dictionary<(DateTime Date, string Shift), (int Sala, int Cocina)>();
        foreach (var row in actualShifts)
        {
            var shift = NormalizeShift(row.ShiftName);
            if (string.IsNullOrEmpty(shift)) continue;
            var key = (row.Date.Date, shift);
            if (!byDateShift.ContainsKey(key))
                byDateShift[key] = (row.StaffFloor, row.StaffKitchen);
        }

        var shifts = new[] { "mediodia", "tarde", "noche" };
        int totalSalaErr = 0, totalCocinaErr = 0, totalComparisons = 0, exactMatches = 0;
        var byDay = new List<object>();

        foreach (var d in days)
        {
            if (!d.TryGetProperty("date", out var dateEl)) continue;
            var dateStr = dateEl.GetString();
            if (string.IsNullOrEmpty(dateStr) || !DateTime.TryParse(dateStr, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)) continue;
            var dayDate = date.Date;

            int GetPredInt(JsonElement el, string prop)
            {
                if (!el.TryGetProperty(prop, out var v)) return 0;
                if (v.ValueKind == JsonValueKind.Number && v.TryGetInt32(out var i)) return i;
                if (v.ValueKind == JsonValueKind.String && int.TryParse(v.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var p)) return p;
                return 0;
            }

            int predSalaMed = GetPredInt(d, "staffSalaMed"), predSalaTar = GetPredInt(d, "staffSalaTar"), predSalaNoc = GetPredInt(d, "staffSalaNoc");
            int predCocinaMed = GetPredInt(d, "staffCocinaMed"), predCocinaTar = GetPredInt(d, "staffCocinaTar"), predCocinaNoc = GetPredInt(d, "staffCocinaNoc");
            if (predSalaMed == 0 && predSalaTar == 0 && predSalaNoc == 0)
            {
                var staffSalaStr = d.TryGetProperty("staffSala", out var ss) ? ss.GetString() : null;
                if (!string.IsNullOrEmpty(staffSalaStr))
                {
                    var parts = staffSalaStr.Split('-');
                    if (parts.Length >= 3) { predSalaMed = int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var x) ? x : 0; predSalaTar = int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out x) ? x : 0; predSalaNoc = int.TryParse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out x) ? x : 0; }
                }
            }
            if (predCocinaMed == 0 && predCocinaTar == 0 && predCocinaNoc == 0)
            {
                var staffCocinaStr = d.TryGetProperty("staffCocina", out var sc) ? sc.GetString() : null;
                if (!string.IsNullOrEmpty(staffCocinaStr))
                {
                    var parts = staffCocinaStr.Split('-');
                    if (parts.Length >= 3) { predCocinaMed = int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var x) ? x : 0; predCocinaTar = int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out x) ? x : 0; predCocinaNoc = int.TryParse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out x) ? x : 0; }
                }
            }

            int daySalaErr = 0, dayCocinaErr = 0, dayComparisons = 0, dayExact = 0;
            foreach (var shift in shifts)
            {
                if (!byDateShift.TryGetValue((dayDate, shift), out var actual)) continue;
                int predSala = shift == "mediodia" ? predSalaMed : (shift == "tarde" ? predSalaTar : predSalaNoc);
                int predCocina = shift == "mediodia" ? predCocinaMed : (shift == "tarde" ? predCocinaTar : predCocinaNoc);
                daySalaErr += Math.Abs(predSala - actual.Sala);
                dayCocinaErr += Math.Abs(predCocina - actual.Cocina);
                dayComparisons += 2;
                if (predSala == actual.Sala && predCocina == actual.Cocina) dayExact++;
            }
            totalSalaErr += daySalaErr;
            totalCocinaErr += dayCocinaErr;
            totalComparisons += dayComparisons;
            exactMatches += dayExact;
            byDay.Add(new
            {
                date = dayDate.ToString("yyyy-MM-dd"),
                sala_mae_day = dayComparisons > 0 ? Math.Round((double)(daySalaErr + dayCocinaErr) / dayComparisons, 2) : 0,
                exact_match = dayExact
            });
        }

        var salaMae = totalComparisons > 0 ? (double)(totalSalaErr + totalCocinaErr) / totalComparisons : 0;
        var exactMatchPct = totalComparisons > 0 ? Math.Round(100.0 * exactMatches / (totalComparisons / 2.0), 1) : 0;
        if (totalComparisons == 0) exactMatchPct = 0;

        return JsonSerializer.Serialize(new
        {
            sala_mae = Math.Round(salaMae, 2),
            comparisons_count = totalComparisons,
            exact_match_pct = exactMatchPct,
            by_day = byDay
        });
    }
}
