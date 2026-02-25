using LucasWeb.Api.Data;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace LucasWeb.Api.Services;

/// <summary>
/// Calcula la predicción de la semana siguiente en vivo cuando no hay fila en WeeklyPredictions.
/// Criterio conservador hostelería: tendencia y enriquecimiento simétricos, sin +1% fijo, pendiente por DOW en ambos sentidos.
/// </summary>
public class NextWeekPredictionService
{
    private const int MinDaysPerWeekForTrend = 6;
    private const int MinDaysPerWeekFallback = 5;
    private const decimal TrendPctClamp = 15m;
    /// <summary>Ventana en días para base por DOW y overall reciente (12 semanas). Si hay menos de 5 días en ventana, se usa todo el histórico.</summary>
    private const int BaseAndOverallRecentDays = 84;
    private const decimal RecentLevelFactorMin = 0.90m;
    private const decimal RecentLevelFactorMax = 1.02m;
    private const int RecentDaysCount = 14;
    private const decimal BiasFactorMin = 0.93m;
    private const decimal BiasFactorMax = 1.04m;

    private readonly AppDbContext _db;

    public NextWeekPredictionService(AppDbContext db) => _db = db;

    public static DateTime GetNextMonday(DateTime from)
    {
        var d = from;
        while (d.DayOfWeek != DayOfWeek.Monday)
            d = d.AddDays(1);
        if (d <= from) d = d.AddDays(7);
        return d.Date;
    }

    private static DateTime GetMonday(DateTime d)
    {
        var diff = (7 + (d.DayOfWeek - DayOfWeek.Monday)) % 7;
        return d.AddDays(-diff).Date;
    }

    /// <summary>Día de la semana 0=Lunes, 6=Domingo (convención del documento).</summary>
    private static int Dow(DateTime d) => (int)d.DayOfWeek == 0 ? 6 : (int)d.DayOfWeek - 1;

    private sealed class ShiftWeights
    {
        public decimal Mediodia { get; set; }
        public decimal Tarde { get; set; }
        public decimal Noche { get; set; }
        public string Source { get; set; } = "default";
    }

    private static string NormalizeShiftName(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return "";
        // Normalizar nombres comunes: Mediodía/Mediodia, Tarde, Noche.
        var x = s.Trim().ToLowerInvariant();
        x = x.Replace("í", "i").Replace("á", "a").Replace("é", "e").Replace("ó", "o").Replace("ú", "u");
        if (x.Contains("medio")) return "mediodia";
        if (x.Contains("tarde")) return "tarde";
        if (x.Contains("noche")) return "noche";
        return x;
    }

    private async Task<ShiftWeights[]> GetShiftWeightsByDowAsync(DateTime currentWeekMonday)
    {
        // Calcula distribución histórica (últimas 12 semanas) a partir de ShiftFeedbacks.
        // Si no hay suficiente histórico por DOW, cae a distribución por defecto.
        var result = new ShiftWeights[7];
        for (var i = 0; i < 7; i++) result[i] = new ShiftWeights { Mediodia = 0.33m, Tarde = 0.33m, Noche = 0.34m, Source = "default" };

        var end = currentWeekMonday.AddDays(-1).Date;
        var start = end.AddDays(-84).Date;

        var rows = await _db.ShiftFeedbacks
            .AsNoTracking()
            .Where(s => s.ExecutionDay != null && !s.ExecutionDay.IsFeedbackOnly && s.ExecutionDay.Date >= start && s.ExecutionDay.Date <= end && s.Revenue > 0)
            .Select(s => new { Date = s.ExecutionDay.Date, s.ShiftName, s.Revenue })
            .ToListAsync();

        if (rows.Count < 20) return result;

        // Por día: calcular share por turno (Revenue / sumRevenueDelDía).
        var byDay = rows
            .GroupBy(r => r.Date.Date)
            .Select(g =>
            {
                var tot = g.Sum(x => x.Revenue);
                if (tot <= 0) return null;
                var med = g.Where(x => NormalizeShiftName(x.ShiftName) == "mediodia").Sum(x => x.Revenue) / tot;
                var tar = g.Where(x => NormalizeShiftName(x.ShiftName) == "tarde").Sum(x => x.Revenue) / tot;
                var noc = g.Where(x => NormalizeShiftName(x.ShiftName) == "noche").Sum(x => x.Revenue) / tot;
                return new { Date = g.Key, Med = med, Tar = tar, Noc = noc };
            })
            .Where(x => x != null)
            .ToList()!;

        if (byDay.Count < 20) return result;

        var grouped = byDay.GroupBy(x => Dow(x!.Date)).ToDictionary(g => g.Key, g => g.ToList());
        foreach (var kv in grouped)
        {
            var dow = kv.Key;
            var list = kv.Value;
            if (list.Count < 6) continue; // mínimo por DOW
            var medAvg = (decimal)list.Average(x => (double)x!.Med);
            var tarAvg = (decimal)list.Average(x => (double)x!.Tar);
            var nocAvg = (decimal)list.Average(x => (double)x!.Noc);
            // Normalizar para que sumen 1 (por seguridad) y evitar negativos
            medAvg = Math.Max(0, medAvg);
            tarAvg = Math.Max(0, tarAvg);
            nocAvg = Math.Max(0, nocAvg);
            var sum = medAvg + tarAvg + nocAvg;
            if (sum <= 0.01m) continue;
            medAvg /= sum;
            tarAvg /= sum;
            nocAvg /= sum;
            result[dow] = new ShiftWeights { Mediodia = medAvg, Tarde = tarAvg, Noche = nocAvg, Source = "historic" };
        }

        return result;
    }

    public async Task<(DateTime WeekStartMonday, decimal PredictedRevenue, string? DailyPredictionsJson)> ComputeLivePredictionAsync()
    {
        var today = DateTime.UtcNow.Date;
        var currentWeekMonday = GetMonday(today);
        var nextMonday = GetNextMonday(today);

        var historicDays = await _db.ExecutionDays
            .AsNoTracking()
            .Where(e => !e.IsFeedbackOnly && e.Date < currentWeekMonday && e.TotalRevenue > 0 && e.TotalHoursWorked > 0)
            .Select(e => new { e.Date, e.TotalRevenue })
            .ToListAsync();

        if (historicDays.Count < 5)
            return (nextMonday, 0, null);

        var weeklyGroups = historicDays
            .GroupBy(e => GetMonday(e.Date))
            .Where(g => g.Count() >= MinDaysPerWeekForTrend)
            .OrderByDescending(g => g.Key)
            .Take(8)
            .ToList();
        if (weeklyGroups.Count < 2)
        {
            weeklyGroups = historicDays
                .GroupBy(e => GetMonday(e.Date))
                .Where(g => g.Count() >= MinDaysPerWeekFallback)
                .OrderByDescending(g => g.Key)
                .Take(8)
                .ToList();
        }
        if (weeklyGroups.Count < 2)
            return (nextMonday, 0, null);

        var last4Weeks = weeklyGroups.Take(4).SelectMany(g => g).ToList();
        var prev4Weeks = weeklyGroups.Skip(4).Take(4).SelectMany(g => g).ToList();
        if (prev4Weeks.Count == 0)
            return (nextMonday, 0, null);

        decimal overallAvg = (decimal)historicDays.Average(x => x.TotalRevenue);
        var recentHistoricDays = historicDays.OrderByDescending(x => x.Date).Take(BaseAndOverallRecentDays).ToList();
        decimal overallAvgRecent = recentHistoricDays.Count >= 5 ? (decimal)recentHistoricDays.Average(x => x.TotalRevenue) : overallAvg;
        var daysForBase = recentHistoricDays.Count >= 5 ? recentHistoricDays : historicDays;
        decimal overallForBase = recentHistoricDays.Count >= 5 ? overallAvgRecent : overallAvg;

        decimal sumLast4 = last4Weeks.Sum(x => x.TotalRevenue);
        decimal sumPrev4 = prev4Weeks.Sum(x => x.TotalRevenue);
        decimal trendPct = sumPrev4 > 0 ? (sumLast4 - sumPrev4) / sumPrev4 * 100 : 0;
        decimal trendPctClamped = Math.Clamp(trendPct, -TrendPctClamp, TrendPctClamp);
        decimal trendFactor = 1 + (trendPctClamped / 100m) * 0.35m;

        var (biasByDow, maeByDow) = await GetBiasAndMaeAsync();

        var lastRecentDays = historicDays.OrderByDescending(x => x.Date).Take(RecentDaysCount).ToList();
        decimal recentAvg = lastRecentDays.Count > 0 ? (decimal)lastRecentDays.Average(x => x.TotalRevenue) : overallAvgRecent;
        decimal recentLevelFactor = overallAvgRecent > 0 ? recentAvg / overallAvgRecent : 1;
        recentLevelFactor = Math.Clamp(recentLevelFactor, RecentLevelFactorMin, RecentLevelFactorMax);

        var byDow = daysForBase.GroupBy(x => Dow(x.Date)).ToDictionary(g => g.Key, g => g.Select(x => x.TotalRevenue).ToList());
        decimal[] avgByDow = new decimal[7];
        decimal[] stdByDow = new decimal[7];
        for (int i = 0; i < 7; i++)
        {
            if (byDow.TryGetValue(i, out var list) && list.Any())
            {
                avgByDow[i] = (decimal)list.Average();
                var variance = list.Average(v => (double)((v - avgByDow[i]) * (v - avgByDow[i])));
                stdByDow[i] = avgByDow[i] > 0 ? (decimal)(Math.Sqrt(variance) / (double)avgByDow[i]) : 0.15m;
            }
            else
            {
                avgByDow[i] = overallForBase;
                stdByDow[i] = 0.15m;
            }
        }

        var byMonth = daysForBase.GroupBy(x => x.Date.Month).ToDictionary(g => g.Key, g => (decimal)g.Average(x => x.TotalRevenue));

        // Pendiente por DOW: slope = (más reciente − más antiguo) / (n−1); se aplica en ambos sentidos (subida y bajada)
        decimal[] slopeByDow = new decimal[7];
        var last4ByDow = last4Weeks.GroupBy(x => Dow(x.Date)).ToDictionary(g => g.Key, g => g.OrderByDescending(x => x.Date).Select(x => x.TotalRevenue).ToList());
        for (int i = 0; i < 7; i++)
        {
            if (last4ByDow.TryGetValue(i, out var list) && list.Count > 1)
            {
                decimal slope = (list[0] - list[list.Count - 1]) / (list.Count - 1);
                slopeByDow[i] = slope * 0.35m;
            }
        }

        decimal conservadorFactor = await GetPrediccionConservadoraFactorAsync();

        var dayNames = new[] { "Lunes", "Martes", "Miércoles", "Jueves", "Viernes", "Sábado", "Domingo" };
        var daily = new List<object>();
        decimal totalPredicted = 0;

        var shiftWeights = await GetShiftWeightsByDowAsync(currentWeekMonday);

        for (int i = 0; i < 7; i++)
        {
            var dayDate = nextMonday.AddDays(i);
            int dow = Dow(dayDate);
            decimal monthAvg = 1m;
            if (byMonth.TryGetValue(dayDate.Month, out var monthRev) && overallForBase > 0)
            {
                monthAvg = monthRev / overallForBase;
                monthAvg = Math.Clamp(monthAvg, 0.9m, 1.1m);
            }

            decimal biasClamped = Math.Clamp((decimal)biasByDow[dow], -20, 20);
            decimal biasFactor = Math.Clamp(1 - (biasClamped / 100m) * 0.35m, BiasFactorMin, BiasFactorMax);

            decimal dayAvg = avgByDow[dow] * trendFactor * recentLevelFactor * monthAvg * biasFactor;
            dayAvg += slopeByDow[dow];
            dayAvg = Math.Max(0, Math.Round(dayAvg, 2));

            decimal stdEur = avgByDow[dow] > 0 ? stdByDow[dow] * avgByDow[dow] : 0.15m * dayAvg;
            decimal learnedMae = (decimal)maeByDow[dow];
            decimal halfBand = Math.Max(1.5m * stdEur, 1.5m * learnedMae);
            decimal min = Math.Max(0, dayAvg - halfBand);
            decimal max = dayAvg + halfBand;
            min = Math.Max(min, 0.85m * dayAvg);
            max = Math.Min(max, 1.15m * dayAvg);
            min = Math.Round(min, 2);
            max = Math.Round(max, 2);

            var w = shiftWeights[dow];
            // Reparto por turno (med/tar/noc) con pesos; noc ajusta por redondeo para que sume exactamente.
            var med = Math.Round(dayAvg * w.Mediodia, 2);
            var tar = Math.Round(dayAvg * w.Tarde, 2);
            var noc = Math.Round(dayAvg - med - tar, 2);
            if (noc < 0) { noc = Math.Max(0, noc); } // por seguridad

            if (conservadorFactor != 1m)
            {
                dayAvg = Math.Round(dayAvg * conservadorFactor, 2);
                min = Math.Round(min * conservadorFactor, 2);
                max = Math.Round(max * conservadorFactor, 2);
                med = Math.Round(med * conservadorFactor, 2);
                tar = Math.Round(tar * conservadorFactor, 2);
                noc = Math.Round(noc * conservadorFactor, 2);
            }
            totalPredicted += dayAvg;

            daily.Add(new
            {
                date = dayDate.ToString("yyyy-MM-dd"),
                dayName = dayNames[dow],
                revenue = dayAvg,
                predictedRevenue = dayAvg,
                min,
                max,
                mediodia = med,
                tarde = tar,
                noche = noc,
                shiftDistributionSource = w.Source,
                shiftDistribution = new { mediodia = w.Mediodia, tarde = w.Tarde, noche = w.Noche }
            });
        }

        var json = JsonSerializer.Serialize(daily);
        return (nextMonday, totalPredicted, json);
    }

    /// <summary>Número de semanas usadas para la predicción (≥6 días por semana, fallback ≥5; máx 8).</summary>
    public async Task<int> GetWeeksUsedForPredictionAsync()
    {
        var today = DateTime.UtcNow.Date;
        var currentWeekMonday = GetMonday(today);
        var historicDays = await _db.ExecutionDays
            .AsNoTracking()
            .Where(e => !e.IsFeedbackOnly && e.Date < currentWeekMonday && e.TotalRevenue > 0 && e.TotalHoursWorked > 0)
            .Select(e => new { e.Date, e.TotalRevenue })
            .ToListAsync();
        if (historicDays.Count < 5) return 0;
        var weeklyGroups = historicDays
            .GroupBy(e => GetMonday(e.Date))
            .Where(g => g.Count() >= MinDaysPerWeekForTrend)
            .OrderByDescending(g => g.Key)
            .Take(8)
            .ToList();
        if (weeklyGroups.Count < 2)
        {
            weeklyGroups = historicDays
                .GroupBy(e => GetMonday(e.Date))
                .Where(g => g.Count() >= MinDaysPerWeekFallback)
                .OrderByDescending(g => g.Key)
                .Take(8)
                .ToList();
        }
        return weeklyGroups.Count;
    }

    /// <summary>Factor (0,01–2]: &lt;1 baja las predicciones, &gt;1 las sube (tendencia al alza). Vacío o 1 = sin ajuste. Por defecto 0,97.</summary>
    private async Task<decimal> GetPrediccionConservadoraFactorAsync()
    {
        var s = await _db.Settings.AsNoTracking().FirstOrDefaultAsync(x => x.Key == "PrediccionConservadoraFactor");
        if (s?.Value == null) return 0.97m;
        if (decimal.TryParse(s.Value.Replace(",", "."), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var v) && v > 0m && v <= 2m)
            return v;
        return 0.97m;
    }

    private async Task<(double[] bias, double[] mae)> GetBiasAndMaeAsync()
    {
        var bias = new double[7];
        var mae = new double[7];
        var biasSetting = await _db.Settings.AsNoTracking().FirstOrDefaultAsync(s => s.Key == "PredictionBiasJson");
        var maeSetting = await _db.Settings.AsNoTracking().FirstOrDefaultAsync(s => s.Key == "PredictionMaeJson");
        if (biasSetting?.Value != null)
            try
            {
                var doc = JsonDocument.Parse(biasSetting.Value);
                if (doc.RootElement.TryGetProperty("avg", out var a))
                    for (var i = 0; i < 7 && i < a.GetArrayLength(); i++) bias[i] = a[i].GetDouble();
            }
            catch { }
        if (maeSetting?.Value != null)
            try
            {
                var doc = JsonDocument.Parse(maeSetting.Value);
                if (doc.RootElement.TryGetProperty("avg_mae", out var a))
                    for (var i = 0; i < 7 && i < a.GetArrayLength(); i++) mae[i] = a[i].GetDouble();
            }
            catch { }
        return (bias, mae);
    }
}
