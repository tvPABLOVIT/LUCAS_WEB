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

    public AnalyticsController(AppDbContext db)
    {
        _db = db;
    }

    /// <summary>
    /// Rango de fechas con datos de ejecución (días con facturación/horas) para restringir selectores de período.
    /// </summary>
    [HttpGet("data-range")]
    public async Task<IActionResult> GetDataRange()
    {
        var range = await _db.ExecutionDays
            .AsNoTracking()
            .Where(d => !d.IsFeedbackOnly && d.TotalRevenue > 0 && d.TotalHoursWorked > 0)
            .Select(d => d.Date)
            .ToListAsync();
        if (range.Count == 0)
            return Ok(new { minDate = (string?)null, maxDate = (string?)null });
        var min = range.Min();
        var max = range.Max();
        var inv = CultureInfo.InvariantCulture;
        return Ok(new { minDate = min.ToString("yyyy-MM-dd", inv), maxDate = max.ToString("yyyy-MM-dd", inv) });
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
        [FromQuery] decimal hotC = 30m,
        [FromQuery(Name = "days")] int? windowDays = null)
    {
        // Usar umbrales guardados en Configuración si existen
        var thKeys = new[] { "WeatherImpactRainyPrecipMm", "WeatherImpactHeavyRainMm", "WeatherImpactWindyKmh", "WeatherImpactColdC", "WeatherImpactHotC" };
        var thSettings = await _db.Settings.Where(s => thKeys.Contains(s.Key)).ToDictionaryAsync(s => s.Key, s => s.Value).ConfigureAwait(false);
        if (thSettings.TryGetValue("WeatherImpactRainyPrecipMm", out var v1) && !string.IsNullOrWhiteSpace(v1) && decimal.TryParse(v1.Replace(",", "."), NumberStyles.Any, CultureInfo.InvariantCulture, out var r)) rainyPrecipMm = r;
        if (thSettings.TryGetValue("WeatherImpactHeavyRainMm", out var v2) && !string.IsNullOrWhiteSpace(v2) && decimal.TryParse(v2.Replace(",", "."), NumberStyles.Any, CultureInfo.InvariantCulture, out var h)) heavyRainMm = h;
        if (thSettings.TryGetValue("WeatherImpactWindyKmh", out var v3) && !string.IsNullOrWhiteSpace(v3) && decimal.TryParse(v3.Replace(",", "."), NumberStyles.Any, CultureInfo.InvariantCulture, out var w)) windyKmh = w;
        if (thSettings.TryGetValue("WeatherImpactColdC", out var v4) && !string.IsNullOrWhiteSpace(v4) && decimal.TryParse(v4.Replace(",", "."), NumberStyles.Any, CultureInfo.InvariantCulture, out var c)) coldC = c;
        if (thSettings.TryGetValue("WeatherImpactHotC", out var v5) && !string.IsNullOrWhiteSpace(v5) && decimal.TryParse(v5.Replace(",", "."), NumberStyles.Any, CultureInfo.InvariantCulture, out var ho)) hotC = ho;

        rainyPrecipMm = rainyPrecipMm > 0 ? rainyPrecipMm : WeatherImpactHelper.DefaultRainyPrecipMm;
        heavyRainMm = heavyRainMm > 0 ? heavyRainMm : WeatherImpactHelper.DefaultHeavyRainMm;
        windyKmh = windyKmh > 0 ? windyKmh : WeatherImpactHelper.DefaultWindyKmh;
        coldC = coldC < hotC ? coldC : WeatherImpactHelper.DefaultColdC;
        hotC = hotC > coldC ? hotC : WeatherImpactHelper.DefaultHotC;
        var inv = CultureInfo.InvariantCulture;
        var end = DateTime.UtcNow.Date;
        var start = end.AddDays(-(windowDays.HasValue && windowDays.Value > 0 ? Math.Clamp(windowDays.Value, 1, 3650) : 180));
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

            // Modo turno: extremo alto/bajo usan Temp promedio del turno (WeatherTempAvg); no hay TempMax/TempMin por turno (mejora D).
            var samples = list
                .Where(x => x.Revenue > 0 && x.Hours > 0)
                .Select(x => new Sample
                {
                    YearMonth = x.Date.ToString("yyyy-MM", inv),
                    Dow = x.Date.DayOfWeek,
                    ShiftName = (x.ShiftName ?? "").Trim(),
                    Revenue = x.Revenue,
                    Productivity = x.Hours > 0 ? x.Revenue / x.Hours : 0,
                    IsRainy = (x.Code.HasValue && WeatherImpactHelper.IsRainCode(x.Code.Value)) || (x.Precip.HasValue && x.Precip.Value >= rainyPrecipMm),
                    IsHeavyRain = x.Precip.HasValue && x.Precip.Value >= heavyRainMm,
                    IsWindy = x.Wind.HasValue && x.Wind.Value >= windyKmh,
                    IsExtremeTempHigh = x.Temp.HasValue && x.Temp.Value > hotC,
                    IsExtremeTempLow = x.Temp.HasValue && x.Temp.Value < coldC
                })
                .ToList();

            return Ok(BuildImpactPayload(samples, start, end, "shift", coverage, rainyPrecipMm, heavyRainMm, windyKmh, coldC, hotC));
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

        // Modo día: extremo alto/bajo se definen por TempMax/TempMin del día (mejora D).
        var daySamples = days
            .Where(x => x.TotalRevenue > 0 && x.TotalHoursWorked > 0)
            .Select(x => new Sample
            {
                YearMonth = x.Date.ToString("yyyy-MM", inv),
                Dow = x.Date.DayOfWeek,
                Revenue = x.TotalRevenue,
                Productivity = x.TotalHoursWorked > 0 ? x.TotalRevenue / x.TotalHoursWorked : 0,
                IsRainy = (x.WeatherCode.HasValue && WeatherImpactHelper.IsRainCode(x.WeatherCode.Value)) || (x.WeatherPrecipMm.HasValue && x.WeatherPrecipMm.Value >= rainyPrecipMm),
                IsHeavyRain = x.WeatherPrecipMm.HasValue && x.WeatherPrecipMm.Value >= heavyRainMm,
                IsWindy = x.WeatherWindMaxKmh.HasValue && x.WeatherWindMaxKmh.Value >= windyKmh,
                IsExtremeTempHigh = x.WeatherTempMax.HasValue && x.WeatherTempMax.Value > hotC,
                IsExtremeTempLow = x.WeatherTempMin.HasValue && x.WeatherTempMin.Value < coldC
            })
            .ToList();

        return Ok(BuildImpactPayload(daySamples, start, end, "day", dayCoverage, rainyPrecipMm, heavyRainMm, windyKmh, coldC, hotC));
    }

    private sealed class Sample
    {
        public string YearMonth { get; set; } = "";
        public DayOfWeek Dow { get; set; }
        public string? ShiftName { get; set; }
        public decimal Revenue { get; set; }
        public decimal Productivity { get; set; }
        public bool IsRainy { get; set; }
        public bool IsHeavyRain { get; set; }
        public bool IsWindy { get; set; }
        public bool IsExtremeTempHigh { get; set; }
        public bool IsExtremeTempLow { get; set; }
    }

    private static readonly (int Dow, string Name)[] DowOrder = { (1, "Lunes"), (2, "Martes"), (3, "Miércoles"), (4, "Jueves"), (5, "Viernes"), (6, "Sábado"), (0, "Domingo") };

    private static object BuildImpactPayload(List<Sample> samples, DateTime start, DateTime end, string groupBy, object? coverage,
        decimal rainyPrecipMm, decimal heavyRainMm, decimal windyKmh, decimal coldC, decimal hotC)
    {
        const decimal TrimFraction = 0.05m; // Mejora B: recorte 5% superior e inferior por Revenue
        var baseSet = samples;
        var rainy = TrimOutliersByRevenue(samples.Where(s => s.IsRainy).ToList(), TrimFraction);
        var dry = TrimOutliersByRevenue(samples.Where(s => !s.IsRainy).ToList(), TrimFraction);
        var heavy = TrimOutliersByRevenue(samples.Where(s => s.IsHeavyRain).ToList(), TrimFraction);
        var windy = TrimOutliersByRevenue(samples.Where(s => s.IsWindy).ToList(), TrimFraction);
        var extremeHigh = TrimOutliersByRevenue(samples.Where(s => s.IsExtremeTempHigh).ToList(), TrimFraction);
        var extremeLow = TrimOutliersByRevenue(samples.Where(s => s.IsExtremeTempLow).ToList(), TrimFraction);
        // Mejora E: baseline temperatura "puro" = solo días con temp en rango normal (ni extremo alto ni bajo)
        var normalTemp = samples.Where(s => !s.IsExtremeTempHigh && !s.IsExtremeTempLow).ToList();
        var notExtremeHigh = TrimOutliersByRevenue(normalTemp, TrimFraction);
        var notExtremeLow = notExtremeHigh;

        Func<Sample, string> keySelector = groupBy == "shift"
            ? (Func<Sample, string>)(s => $"{(int)s.Dow}|{(s.ShiftName ?? "").Trim().ToLowerInvariant()}")
            : (s => $"{(int)s.Dow}");

        var thresholdsUsed = new { rainyPrecipMm, heavyRainMm, windyKmh, coldC, hotC };
        var notWindy = TrimOutliersByRevenue(samples.Where(s => !s.IsWindy).ToList(), TrimFraction);

        return new
        {
            groupBy,
            from = start.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture),
            to = end.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture),
            sampleCount = baseSet.Count,
            coverage,
            thresholdsUsed,
            rainy = Metrics(rainy, dry, keySelector),
            heavyRain = Metrics(heavy, dry, keySelector),
            windy = Metrics(windy, notWindy, keySelector),
            extremeTempHigh = Metrics(extremeHigh, notExtremeHigh, keySelector),
            extremeTempLow = Metrics(extremeLow, notExtremeLow, keySelector),
            rainyByDow = MetricsByDow(rainy, dry),
            heavyRainByDow = MetricsByDow(heavy, dry),
            windyByDow = MetricsByDow(windy, notWindy),
            extremeTempHighByDow = MetricsByDow(extremeHigh, notExtremeHigh),
            extremeTempLowByDow = MetricsByDow(extremeLow, notExtremeLow)
        };
    }

    /// <summary>Mejora B: excluye el trimFraction superior e inferior por Revenue para reducir efecto de outliers.</summary>
    private static List<Sample> TrimOutliersByRevenue(List<Sample> list, decimal trimFraction)
    {
        if (list.Count < 4) return list;
        var sorted = list.OrderBy(x => x.Revenue).ToList();
        var removeCount = Math.Max(1, (int)(list.Count * trimFraction));
        var skip = removeCount;
        var take = list.Count - 2 * removeCount;
        if (take < 2) return list;
        return sorted.Skip(skip).Take(take).ToList();
    }

    /// <summary>Impacto por día de la semana: cada DOW se compara solo con el mismo DOW (lunes con lunes, etc.). Incluye SE aproximado (mejora C).</summary>
    private static List<object> MetricsByDow(List<Sample> group, List<Sample> baseline)
    {
        var result = new List<object>();
        foreach (var (dow, name) in DowOrder)
        {
            var g = group.Where(s => (int)s.Dow == dow).ToList();
            var b = baseline.Where(s => (int)s.Dow == dow).ToList();
            decimal? diffRev = null;
            decimal? diffProd = null;
            decimal? diffRevSE = null;
            decimal? diffProdSE = null;
            if (g.Count > 0 && b.Count > 0)
            {
                var avgG = g.Average(x => x.Revenue);
                var avgB = b.Average(x => x.Revenue);
                if (avgB > 0)
                {
                    diffRev = Math.Round((avgG - avgB) / avgB * 100m, 1);
                    var seRev = StdErrRatio(g.Select(x => x.Revenue).ToList(), b.Select(x => x.Revenue).ToList(), avgG, avgB);
                    if (seRev.HasValue) diffRevSE = Math.Round(seRev.Value * 100m, 1);
                }
                var avgGp = g.Average(x => x.Productivity);
                var avgBp = b.Average(x => x.Productivity);
                if (avgBp > 0)
                {
                    diffProd = Math.Round((avgGp - avgBp) / avgBp * 100m, 1);
                    var seProd = StdErrRatio(g.Select(x => x.Productivity).ToList(), b.Select(x => x.Productivity).ToList(), avgGp, avgBp);
                    if (seProd.HasValue) diffProdSE = Math.Round(seProd.Value * 100m, 1);
                }
            }
            result.Add(new { dow, dowName = name, count = g.Count, baselineCount = b.Count, diffPctRevenue = diffRev, diffPctProductivity = diffProd, diffPctRevenueSE = diffRevSE, diffPctProductivitySE = diffProdSE });
        }
        return result;
    }

    /// <summary>Aproximación SE del ratio (avgG-avgB)/avgB usando delta method: (avgG/avgB)*sqrt((stdG/avgG)²/nG + (stdB/avgB)²/nB).</summary>
    private static decimal? StdErrRatio(List<decimal> g, List<decimal> b, decimal avgG, decimal avgB)
    {
        if (g.Count < 2 || b.Count < 2 || avgB == 0) return null;
        var stdG = StdDev(g);
        var stdB = StdDev(b);
        if (!stdG.HasValue || !stdB.HasValue) return null;
        var v = (double)((stdG.Value / avgG) * (stdG.Value / avgG) / g.Count + (stdB.Value / avgB) * (stdB.Value / avgB) / b.Count);
        if (v <= 0) return null;
        return (decimal)((double)(avgG / avgB) * Math.Sqrt(v));
    }

    private static decimal? StdDev(List<decimal> values)
    {
        if (values.Count < 2) return null;
        var avg = values.Average();
        var sumSq = values.Sum(x => (x - avg) * (x - avg));
        return (decimal)Math.Sqrt((double)sumSq / (values.Count - 1));
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

}
