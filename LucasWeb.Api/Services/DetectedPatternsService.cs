using LucasWeb.Api.Data;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace LucasWeb.Api.Services;

/// <summary>Patrones aprendidos desde histórico (lluvia, festivos, temperatura). Criterio conservador: mínimo 6 muestras por grupo y |pct_diff| ≥ 15%.</summary>
public class DetectedPatternsService : IDetectedPatternsService
{
    private const int MinSamplesPerGroup = 6;
    private const decimal MinPctDiffThreshold = 15m;

    private readonly AppDbContext _db;

    public DetectedPatternsService(AppDbContext db) => _db = db;

    public async Task<PatternData?> GetPatternAsync(string type, string? key = null)
    {
        var q = _db.DetectedPatterns.AsNoTracking().Where(p => p.Type == type);
        if (!string.IsNullOrEmpty(key)) q = q.Where(p => p.Key == key);
        var e = await q.OrderByDescending(p => p.UpdatedAt).FirstOrDefaultAsync();
        return e == null ? null : new PatternData { Type = e.Type, Key = e.Key, JsonData = e.JsonData, Confidence = e.Confidence };
    }

    public async Task SavePatternAsync(string type, string? key, string jsonData, decimal confidence)
    {
        var existing = await _db.DetectedPatterns
            .FirstOrDefaultAsync(p => p.Type == type && (key == null ? p.Key == null : p.Key == key));
        var now = DateTime.UtcNow;
        if (existing != null)
        {
            existing.JsonData = jsonData;
            existing.Confidence = confidence;
            existing.UpdatedAt = now;
        }
        else
        {
            _db.DetectedPatterns.Add(new Models.DetectedPattern
            {
                Id = Guid.NewGuid(),
                Type = type,
                Key = key,
                JsonData = jsonData,
                Confidence = confidence,
                CreatedAt = now,
                UpdatedAt = now
            });
        }
        await _db.SaveChangesAsync();
    }

    public async Task ComputeAndSavePatternsAsync()
    {
        var days = await _db.ExecutionDays
            .AsNoTracking()
            .Where(e => !e.IsFeedbackOnly && e.TotalRevenue > 0 && e.TotalHoursWorked > 0)
            .Select(e => new { e.Date, e.TotalRevenue, e.WeatherCode, e.IsHoliday, e.WeatherTemp, e.WeatherPrecipMm, e.WeatherTempMax, e.WeatherTempMin })
            .ToListAsync();
        if (days.Count < 10) return;

        var dayNames = new[] { "Lunes", "Martes", "Miércoles", "Jueves", "Viernes", "Sábado", "Domingo" };
        var dow = (DateTime d) => (int)d.DayOfWeek == 0 ? 6 : (int)d.DayOfWeek - 1;
        const decimal rainyPrecipMm = WeatherImpactHelper.DefaultRainyPrecipMm;
        const decimal coldC = WeatherImpactHelper.DefaultColdC;
        const decimal hotC = WeatherImpactHelper.DefaultHotC;

        for (int i = 0; i < 7; i++)
        {
            var list = days.Where(d => dow(d.Date) == i).Select(d => d.TotalRevenue).ToList();
            if (list.Count < 2) continue;
            var avg = (decimal)list.Average(x => (double)x);
            var variance = list.Average(x => (double)((x - avg) * (x - avg)));
            var std = list.Count >= 3 ? (decimal)Math.Sqrt(variance) : 0;
            var conf = Math.Min(95, 50 + list.Count);
            await SavePatternAsync("Estacional", dayNames[i], JsonSerializer.Serialize(new { avg_revenue = avg, std_dev = (double)std, count = list.Count }), conf);
        }

        // Lluvia: mismo criterio que API (código WMO o precip ≥ 0,5 mm). Baseline "dry" normalizado por DOW.
        var rainy = days.Where(d => (d.WeatherCode.HasValue && WeatherImpactHelper.IsRainCode(d.WeatherCode.Value)) || (d.WeatherPrecipMm.HasValue && d.WeatherPrecipMm.Value >= rainyPrecipMm)).ToList();
        var dry = days.Where(d => !((d.WeatherCode.HasValue && WeatherImpactHelper.IsRainCode(d.WeatherCode.Value)) || (d.WeatherPrecipMm.HasValue && d.WeatherPrecipMm.Value >= rainyPrecipMm))).ToList();
        if (rainy.Count >= MinSamplesPerGroup && dry.Count >= MinSamplesPerGroup)
        {
            var avgRainy = (decimal)rainy.Average(d => (double)d.TotalRevenue);
            decimal? expectedDry = ExpectedRevenueByDow(dry, rainy, d => d.Date, d => d.TotalRevenue, dow);
            if (expectedDry.HasValue && expectedDry.Value > 0)
            {
                var pctDiff = (avgRainy - expectedDry.Value) / expectedDry.Value * 100;
                if (Math.Abs(pctDiff) >= MinPctDiffThreshold)
                {
                    var rainFactor = 1 + Math.Clamp(pctDiff / 100m, -0.2m, 0.2m);
                    var conf = Math.Min(90m, 50 + Math.Min(rainy.Count, dry.Count));
                    await SavePatternAsync("Impacto clima lluvioso", null, JsonSerializer.Serialize(new { pct_diff = pctDiff, rainFactor, avg_rainy = avgRainy, expected_baseline = expectedDry, count_rainy = rainy.Count, count_dry = dry.Count, normalized_by_dow = true }), conf);
                }
            }
        }

        var holiday = days.Where(d => d.IsHoliday).ToList();
        var notHoliday = days.Where(d => !d.IsHoliday).ToList();
        if (holiday.Count >= MinSamplesPerGroup && notHoliday.Count >= MinSamplesPerGroup)
        {
            var avgH = (decimal)holiday.Average(d => (double)d.TotalRevenue);
            var avgNH = (decimal)notHoliday.Average(d => (double)d.TotalRevenue);
            if (avgNH > 0)
            {
                var pctDiff = (avgH - avgNH) / avgNH * 100;
                if (Math.Abs(pctDiff) >= MinPctDiffThreshold)
                {
                    var holidayFactor = 1 + Math.Clamp(pctDiff / 100m, -0.3m, 0.3m);
                    var conf = Math.Min(90m, 50 + Math.Min(holiday.Count, notHoliday.Count));
                    await SavePatternAsync("Impacto festivos", null, JsonSerializer.Serialize(new { pct_diff = pctDiff, holidayFactor, count_holiday = holiday.Count, count_not_holiday = notHoliday.Count }), conf);
                }
            }
        }

        // Temperatura: mismos umbrales que API (< 5 °C o > 30 °C = extrema). Baseline "no extrema" normalizado por DOW.
        var extreme = days.Where(d =>
            (d.WeatherTempMax.HasValue && d.WeatherTempMax.Value > hotC) ||
            (d.WeatherTempMin.HasValue && d.WeatherTempMin.Value < coldC) ||
            (d.WeatherTemp.HasValue && (d.WeatherTemp.Value < coldC || d.WeatherTemp.Value > hotC))).ToList();
        var notExtreme = days.Where(d => !(
            (d.WeatherTempMax.HasValue && d.WeatherTempMax.Value > hotC) ||
            (d.WeatherTempMin.HasValue && d.WeatherTempMin.Value < coldC) ||
            (d.WeatherTemp.HasValue && (d.WeatherTemp.Value < coldC || d.WeatherTemp.Value > hotC)))).ToList();
        if (extreme.Count >= MinSamplesPerGroup && notExtreme.Count >= MinSamplesPerGroup)
        {
            var avgExt = (decimal)extreme.Average(d => (double)d.TotalRevenue);
            decimal? expectedMild = ExpectedRevenueByDow(notExtreme, extreme, d => d.Date, d => d.TotalRevenue, dow);
            if (expectedMild.HasValue && expectedMild.Value > 0)
            {
                var pctDiff = (avgExt - expectedMild.Value) / expectedMild.Value * 100;
                if (Math.Abs(pctDiff) >= MinPctDiffThreshold)
                {
                    var tempFactor = 1 + Math.Clamp(pctDiff / 100m, -0.2m, 0.2m);
                    var conf = Math.Min(90m, 50 + Math.Min(extreme.Count, notExtreme.Count));
                    await SavePatternAsync("Impacto temperatura", null, JsonSerializer.Serialize(new { pct_diff = pctDiff, tempFactor, count_extreme = extreme.Count, count_not_extreme = notExtreme.Count, normalized_by_dow = true }), conf);
                }
            }
        }
    }

    /// <summary>Expected revenue del grupo "baseline" ponderado por la distribución DOW del grupo "target" (misma lógica que AnalyticsController Metrics).</summary>
    private static decimal? ExpectedRevenueByDow<T>(List<T> baseline, List<T> target, Func<T, DateTime> getDate, Func<T, decimal> getRevenue, Func<DateTime, int> dow)
    {
        var baselineByDow = baseline.GroupBy(d => dow(getDate(d))).ToDictionary(g => g.Key, g => g.ToList());
        var targetCountByDow = target.GroupBy(d => dow(getDate(d))).ToDictionary(g => g.Key, g => g.Count());
        decimal sumRev = 0;
        var sumW = 0;
        foreach (var kv in targetCountByDow)
        {
            if (!baselineByDow.TryGetValue(kv.Key, out var list) || list.Count == 0) continue;
            var avgBaseline = list.Average(x => (double)getRevenue(x));
            sumRev += (decimal)avgBaseline * kv.Value;
            sumW += kv.Value;
        }
        if (sumW == 0) return null;
        return Math.Round(sumRev / sumW, 2);
    }
}
