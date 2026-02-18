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
            .Select(e => new { e.Date, e.TotalRevenue, e.WeatherCode, e.IsHoliday, e.WeatherTemp })
            .ToListAsync();
        if (days.Count < 10) return;

        var dayNames = new[] { "Lunes", "Martes", "Miércoles", "Jueves", "Viernes", "Sábado", "Domingo" };
        var dow = (DateTime d) => (int)d.DayOfWeek == 0 ? 6 : (int)d.DayOfWeek - 1;

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

        var overallAvg = (decimal)days.Average(d => (double)d.TotalRevenue);

        var rainy = days.Where(d => d.WeatherCode.HasValue && IsRainCode(d.WeatherCode.Value)).ToList();
        var sunny = days.Where(d => d.WeatherCode.HasValue && (d.WeatherCode.Value == 0 || d.WeatherCode.Value == 1 || d.WeatherCode.Value == 2)).ToList();
        if (rainy.Count >= MinSamplesPerGroup && sunny.Count >= MinSamplesPerGroup)
        {
            var avgRainy = (decimal)rainy.Average(d => (double)d.TotalRevenue);
            var avgSunny = (decimal)sunny.Average(d => (double)d.TotalRevenue);
            if (avgSunny > 0)
            {
                var pctDiff = (avgRainy - avgSunny) / avgSunny * 100;
                if (Math.Abs(pctDiff) >= MinPctDiffThreshold)
                {
                    var rainFactor = 1 + Math.Clamp(pctDiff / 100m, -0.2m, 0.2m);
                    var conf = Math.Min(90m, 50 + Math.Min(rainy.Count, sunny.Count));
                    await SavePatternAsync("Impacto clima lluvioso", null, JsonSerializer.Serialize(new { pct_diff = pctDiff, rainFactor, avg_rainy = avgRainy, avg_sunny = avgSunny, count_rainy = rainy.Count, count_sunny = sunny.Count }), conf);
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

        var extreme = days.Where(d => !d.WeatherTemp.HasValue || d.WeatherTemp < 5 || d.WeatherTemp > 30).ToList();
        var mild = days.Where(d => d.WeatherTemp.HasValue && d.WeatherTemp >= 15 && d.WeatherTemp <= 25).ToList();
        if (extreme.Count >= MinSamplesPerGroup && mild.Count >= MinSamplesPerGroup)
        {
            var avgExt = (decimal)extreme.Average(d => (double)d.TotalRevenue);
            var avgMild = (decimal)mild.Average(d => (double)d.TotalRevenue);
            if (avgMild > 0)
            {
                var pctDiff = (avgExt - avgMild) / avgMild * 100;
                if (Math.Abs(pctDiff) >= MinPctDiffThreshold)
                {
                    var tempFactor = 1 + Math.Clamp(pctDiff / 100m, -0.2m, 0.2m);
                    var conf = Math.Min(90m, 50 + Math.Min(extreme.Count, mild.Count));
                    await SavePatternAsync("Impacto temperatura", null, JsonSerializer.Serialize(new { pct_diff = pctDiff, tempFactor, count_extreme = extreme.Count, count_mild = mild.Count }), conf);
                }
            }
        }
    }

    private static bool IsRainCode(int code) => code is >= 51 and <= 67 or >= 71 and <= 77 or >= 80 and <= 82 or 95 or 96;
}
