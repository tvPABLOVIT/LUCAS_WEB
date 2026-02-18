using LucasWeb.Api.Data;
using LucasWeb.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Globalization;

namespace LucasWeb.Api.Controllers;

[ApiController]
[Route("api/analytics")]
[Authorize]
public class AnalyticsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IStaffRevenueComfortService _staffComfort;

    public AnalyticsController(AppDbContext db, IStaffRevenueComfortService staffComfort)
    {
        _db = db;
        _staffComfort = staffComfort;
    }

    /// <summary>
    /// Agregados por esquema de personal (sala-cocina) y banda de facturación por camarero.
    /// Sirve para ver "límite cómodo" por esquema (ej. 1-1) y cuándo conviene añadir personal.
    /// </summary>
    [HttpGet("staff-revenue-comfort")]
    public async Task<IActionResult> GetStaffRevenueComfort([FromQuery] int? minShifts, CancellationToken cancellationToken)
    {
        var result = await _staffComfort.GetAggregatesAsync(minShifts, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Impacto del clima en histórico (facturación/productividad) por día o por turno.
    /// </summary>
    [HttpGet("weather-impact")]
    public async Task<IActionResult> GetWeatherImpact(
        [FromQuery] string? from,
        [FromQuery] string? to,
        [FromQuery] string groupBy = "day",
        [FromQuery] decimal rainyPrecipMm = 0.5m,
        [FromQuery] decimal heavyRainMm = 5m,
        [FromQuery] decimal windyKmh = 35m,
        [FromQuery] decimal coldC = 5m,
        [FromQuery] decimal hotC = 30m)
    {
        var inv = CultureInfo.InvariantCulture;
        var end = DateTime.UtcNow.Date;
        var start = end.AddDays(-180);
        if (!string.IsNullOrWhiteSpace(to) && DateTime.TryParse(to, inv, DateTimeStyles.None, out var t)) end = t.Date;
        if (!string.IsNullOrWhiteSpace(from) && DateTime.TryParse(from, inv, DateTimeStyles.None, out var f)) start = f.Date;
        if (start > end) (start, end) = (end, start);
        if ((end - start).TotalDays > 3650) start = end.AddDays(-3650);

        groupBy = (groupBy ?? "day").Trim().ToLowerInvariant();

        if (groupBy == "shift")
        {
            var list = await _db.ShiftFeedbacks
                .AsNoTracking()
                .Where(s => s.ExecutionDay != null && !s.ExecutionDay.IsFeedbackOnly && s.ExecutionDay.Date >= start && s.ExecutionDay.Date <= end)
                .Select(s => new
                {
                    Date = s.ExecutionDay.Date,
                    s.ShiftName,
                    Revenue = s.Revenue,
                    Hours = s.HoursWorked,
                    Code = s.WeatherCode,
                    Temp = s.WeatherTempAvg,
                    Precip = s.WeatherPrecipMm,
                    Wind = s.WeatherWindMaxKmh
                })
                .ToListAsync();

            var coverage = new
            {
                withAnyWeather = list.Count(x => x.Code.HasValue || x.Temp.HasValue || x.Precip.HasValue || x.Wind.HasValue),
                withCode = list.Count(x => x.Code.HasValue),
                withTemp = list.Count(x => x.Temp.HasValue),
                withPrecip = list.Count(x => x.Precip.HasValue),
                withWind = list.Count(x => x.Wind.HasValue)
            };

            var samples = list
                .Where(x => x.Revenue > 0 && x.Hours > 0)
                .Select(x => new Sample
                {
                    Dow = x.Date.DayOfWeek,
                    ShiftName = (x.ShiftName ?? "").Trim(),
                    Revenue = x.Revenue,
                    Productivity = x.Hours > 0 ? x.Revenue / x.Hours : 0,
                    IsRainy = (x.Code.HasValue && IsRainCode(x.Code.Value)) || (x.Precip.HasValue && x.Precip.Value >= rainyPrecipMm),
                    IsHeavyRain = x.Precip.HasValue && x.Precip.Value >= heavyRainMm,
                    IsWindy = x.Wind.HasValue && x.Wind.Value >= windyKmh,
                    IsExtremeTemp = x.Temp.HasValue && (x.Temp.Value < coldC || x.Temp.Value > hotC)
                })
                .ToList();

            return Ok(BuildImpactPayload(samples, start, end, "shift", coverage));
        }

        // day (default)
        var days = await _db.ExecutionDays
            .AsNoTracking()
            .Where(d => !d.IsFeedbackOnly && d.Date >= start && d.Date <= end)
            .Select(d => new
            {
                d.Date,
                d.TotalRevenue,
                d.TotalHoursWorked,
                d.WeatherCode,
                d.WeatherTempMax,
                d.WeatherTempMin,
                d.WeatherPrecipMm,
                d.WeatherWindMaxKmh
            })
            .ToListAsync();

        var dayCoverage = new
        {
            withAnyWeather = days.Count(x => x.WeatherCode.HasValue || x.WeatherTempMax.HasValue || x.WeatherTempMin.HasValue || x.WeatherPrecipMm.HasValue || x.WeatherWindMaxKmh.HasValue),
            withCode = days.Count(x => x.WeatherCode.HasValue),
            withTemp = days.Count(x => x.WeatherTempMax.HasValue || x.WeatherTempMin.HasValue),
            withPrecip = days.Count(x => x.WeatherPrecipMm.HasValue),
            withWind = days.Count(x => x.WeatherWindMaxKmh.HasValue)
        };

        var daySamples = days
            .Where(x => x.TotalRevenue > 0 && x.TotalHoursWorked > 0)
            .Select(x =>
            {
                var isExtreme = (x.WeatherTempMax.HasValue && x.WeatherTempMax.Value > hotC) ||
                                (x.WeatherTempMin.HasValue && x.WeatherTempMin.Value < coldC);
                return new Sample
                {
                    Dow = x.Date.DayOfWeek,
                    Revenue = x.TotalRevenue,
                    Productivity = x.TotalHoursWorked > 0 ? x.TotalRevenue / x.TotalHoursWorked : 0,
                    IsRainy = (x.WeatherCode.HasValue && IsRainCode(x.WeatherCode.Value)) || (x.WeatherPrecipMm.HasValue && x.WeatherPrecipMm.Value >= rainyPrecipMm),
                    IsHeavyRain = x.WeatherPrecipMm.HasValue && x.WeatherPrecipMm.Value >= heavyRainMm,
                    IsWindy = x.WeatherWindMaxKmh.HasValue && x.WeatherWindMaxKmh.Value >= windyKmh,
                    IsExtremeTemp = isExtreme
                };
            })
            .ToList();

        return Ok(BuildImpactPayload(daySamples, start, end, "day", dayCoverage));
    }

    private sealed class Sample
    {
        public DayOfWeek Dow { get; set; }
        public string? ShiftName { get; set; }
        public decimal Revenue { get; set; }
        public decimal Productivity { get; set; }
        public bool IsRainy { get; set; }
        public bool IsHeavyRain { get; set; }
        public bool IsWindy { get; set; }
        public bool IsExtremeTemp { get; set; }
    }

    private static object BuildImpactPayload(List<Sample> samples, DateTime start, DateTime end, string groupBy, object? coverage)
    {
        var baseSet = samples;
        var rainy = samples.Where(s => s.IsRainy).ToList();
        var dry = samples.Where(s => !s.IsRainy).ToList();
        var heavy = samples.Where(s => s.IsHeavyRain).ToList();
        var windy = samples.Where(s => s.IsWindy).ToList();
        var extreme = samples.Where(s => s.IsExtremeTemp).ToList();

        Func<Sample, string> keySelector = groupBy == "shift"
            ? (s => $"{(int)s.Dow}|{(s.ShiftName ?? "").Trim().ToLowerInvariant()}")
            : (s => $"{(int)s.Dow}");

        return new
        {
            groupBy,
            from = start.ToString("yyyy-MM-dd"),
            to = end.ToString("yyyy-MM-dd"),
            sampleCount = baseSet.Count,
            coverage,
            rainy = Metrics(rainy, dry, keySelector),
            heavyRain = Metrics(heavy, dry, keySelector),
            windy = Metrics(windy, samples.Where(s => !s.IsWindy).ToList(), keySelector),
            extremeTemp = Metrics(extreme, samples.Where(s => !s.IsExtremeTemp).ToList(), keySelector)
        };
    }

    private static object Metrics(List<Sample> group, List<Sample> baseline, Func<Sample, string> keySelector)
    {
        var gRev = group.Select(x => x.Revenue).ToList();
        var gProd = group.Select(x => x.Productivity).ToList();

        var gAvgRev = Avg(gRev);
        var gAvgProd = Avg(gProd);

        // Baseline normalizado por día de semana (y por turno si aplica):
        // expectedBaseline = sum_k avgBaseline(k) * countGroup(k) / totalGroup
        var baselineByKey = baseline
            .GroupBy(keySelector)
            .ToDictionary(g => g.Key, g => new
            {
                AvgRevenue = g.Count() > 0 ? (decimal?)g.Average(x => x.Revenue) : null,
                AvgProductivity = g.Count() > 0 ? (decimal?)g.Average(x => x.Productivity) : null,
                Count = g.Count()
            });
        var groupCounts = group.GroupBy(keySelector).ToDictionary(g => g.Key, g => g.Count());

        decimal? expectedRev = null;
        decimal? expectedProd = null;
        var covered = 0;
        if (group.Count > 0)
        {
            decimal sumRev = 0;
            decimal sumProd = 0;
            var sumW = 0;
            foreach (var kv in groupCounts)
            {
                var k = kv.Key;
                var w = kv.Value;
                if (baselineByKey.TryGetValue(k, out var b) && b.AvgRevenue.HasValue && b.AvgProductivity.HasValue)
                {
                    sumRev += b.AvgRevenue.Value * w;
                    sumProd += b.AvgProductivity.Value * w;
                    sumW += w;
                    covered += w;
                }
            }
            if (sumW > 0)
            {
                expectedRev = Math.Round(sumRev / sumW, 2);
                expectedProd = Math.Round(sumProd / sumW, 2);
            }
        }

        return new
        {
            count = group.Count,
            baselineCount = baseline.Count,
            baselineMatchedCount = covered,
            baselineMatchedAvgRevenue = expectedRev,
            baselineMatchedAvgProductivity = expectedProd,
            avgRevenue = gAvgRev,
            medianRevenue = Median(gRev),
            avgProductivity = gAvgProd,
            medianProductivity = Median(gProd),
            // Dif % vs baseline normalizado por DOW (y turno si aplica)
            diffPctRevenue = (expectedRev.HasValue && expectedRev.Value > 0 && gAvgRev.HasValue) ? (gAvgRev.Value - expectedRev.Value) / expectedRev.Value * 100 : (decimal?)null,
            diffPctProductivity = (expectedProd.HasValue && expectedProd.Value > 0 && gAvgProd.HasValue) ? (gAvgProd.Value - expectedProd.Value) / expectedProd.Value * 100 : (decimal?)null
        };
    }

    private static decimal? Avg(List<decimal> values) => values.Count == 0 ? null : Math.Round(values.Average(), 2);

    private static decimal? Median(List<decimal> values)
    {
        if (values.Count == 0) return null;
        var sorted = values.OrderBy(x => x).ToList();
        var mid = sorted.Count / 2;
        if (sorted.Count % 2 == 1) return Math.Round(sorted[mid], 2);
        return Math.Round((sorted[mid - 1] + sorted[mid]) / 2m, 2);
    }

    private static bool IsRainCode(int code) => code is >= 51 and <= 67 or >= 71 and <= 77 or >= 80 and <= 82 or 95 or 96;
}
