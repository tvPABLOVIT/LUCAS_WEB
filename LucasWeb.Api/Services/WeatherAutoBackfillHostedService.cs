using LucasWeb.Api.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using System.Globalization;

namespace LucasWeb.Api.Services;

/// <summary>
/// Rellena automáticamente el clima histórico en ExecutionDays para poder analizar
/// cómo afectan lluvia/viento/temperaturas a la facturación.
/// - Se ejecuta al arrancar y luego 1 vez al día.
/// - Solo rellena campos faltantes (no machaca datos existentes).
/// </summary>
public class WeatherAutoBackfillHostedService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;

    public WeatherAutoBackfillHostedService(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Pequeño delay para no competir con el arranque inicial.
        try { await Task.Delay(TimeSpan.FromSeconds(6), stoppingToken); } catch { }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunOnceAsync(stoppingToken);
            }
            catch
            {
                // No tumbar la API por fallos de clima.
            }

            try
            {
                await Task.Delay(TimeSpan.FromHours(24), stoppingToken);
            }
            catch
            {
                // cancelado
            }
        }
    }

    private async Task RunOnceAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var weather = scope.ServiceProvider.GetRequiredService<IWeatherService>();

        var inv = CultureInfo.InvariantCulture;
        decimal? lat = null;
        decimal? lon = null;
        var latS = await db.Settings.AsNoTracking().Where(s => s.Key == "LatRestaurante").Select(s => s.Value).FirstOrDefaultAsync(ct);
        var lonS = await db.Settings.AsNoTracking().Where(s => s.Key == "LonRestaurante").Select(s => s.Value).FirstOrDefaultAsync(ct);
        if (!string.IsNullOrWhiteSpace(latS) && decimal.TryParse(latS.Replace(",", "."), NumberStyles.Any, inv, out var la)) lat = la;
        if (!string.IsNullOrWhiteSpace(lonS) && decimal.TryParse(lonS.Replace(",", "."), NumberStyles.Any, inv, out var lo)) lon = lo;
        if (!lat.HasValue || !lon.HasValue) return;

        // Lookback razonable para ir cubriendo histórico; solo actualiza faltantes.
        var end = DateTime.UtcNow.Date;
        var start = end.AddDays(-180);

        var pending = await db.ExecutionDays
            .Where(d => !d.IsFeedbackOnly && d.Date >= start && d.Date <= end)
            .Where(d => !(d.WeatherCode.HasValue &&
                          d.WeatherTempMax.HasValue &&
                          d.WeatherTempMin.HasValue &&
                          d.WeatherPrecipMm.HasValue &&
                          d.WeatherWindMaxKmh.HasValue))
            .OrderBy(d => d.Date)
            .ToListAsync(ct);

        var pendingShifts = await db.ShiftFeedbacks
            .Include(s => s.ExecutionDay)
            .Where(s => s.ExecutionDay != null && !s.ExecutionDay.IsFeedbackOnly && s.ExecutionDay.Date >= start && s.ExecutionDay.Date <= end)
            .Where(s => !(s.WeatherCode.HasValue &&
                          s.WeatherTempAvg.HasValue &&
                          s.WeatherPrecipMm.HasValue &&
                          s.WeatherWindMaxKmh.HasValue))
            .OrderBy(s => s.ExecutionDay.Date)
            .ToListAsync(ct);

        if (pending.Count == 0 && pendingShifts.Count == 0) return;

        var chunkSize = 31;
        for (var chunkStart = start.Date; chunkStart <= end.Date; chunkStart = chunkStart.AddDays(chunkSize))
        {
            var chunkEnd = chunkStart.AddDays(chunkSize - 1);
            if (chunkEnd > end.Date) chunkEnd = end.Date;

            var weatherList = await weather.GetWeatherForRangeAsync(chunkStart, chunkEnd, lat, lon);
            var shiftList = await weather.GetShiftWeatherForRangeAsync(chunkStart, chunkEnd, lat, lon);
            if (weatherList.Count == 0 && shiftList.Count == 0) continue;

            var byDate = weatherList
                .Where(w => w.Date != DateTime.MinValue)
                .ToDictionary(w => w.Date.Date, w => w);

            var byShift = shiftList
                .Where(w => w.Date != DateTime.MinValue && !string.IsNullOrWhiteSpace(w.ShiftName))
                .GroupBy(w => new { D = w.Date.Date, S = w.ShiftName.Trim() })
                .ToDictionary(g => (g.Key.D, g.Key.S), g => g.First());

            var any = false;
            foreach (var d in pending)
            {
                if (d.Date.Date < chunkStart || d.Date.Date > chunkEnd) continue;
                if (!byDate.TryGetValue(d.Date.Date, out var w)) continue;

                if (!d.WeatherCode.HasValue) { d.WeatherCode = w.WeatherCode; any = true; }
                if (!d.WeatherTempMax.HasValue && w.TempMax.HasValue) { d.WeatherTempMax = w.TempMax.Value; any = true; }
                if (!d.WeatherTempMin.HasValue && w.TempMin.HasValue) { d.WeatherTempMin = w.TempMin.Value; any = true; }
                if (!d.WeatherPrecipMm.HasValue && w.PrecipitationSumMm.HasValue) { d.WeatherPrecipMm = w.PrecipitationSumMm.Value; any = true; }
                if (!d.WeatherWindMaxKmh.HasValue && w.WindSpeedMaxKmh.HasValue) { d.WeatherWindMaxKmh = w.WindSpeedMaxKmh.Value; any = true; }

                var tempRep = w.TempMax ?? w.TempMin;
                if (!d.WeatherTemp.HasValue && tempRep.HasValue) { d.WeatherTemp = tempRep.Value; any = true; }
            }

            foreach (var s in pendingShifts)
            {
                var date = s.ExecutionDay.Date.Date;
                if (date < chunkStart || date > chunkEnd) continue;
                var name = (s.ShiftName ?? "").Trim();
                if (string.IsNullOrWhiteSpace(name)) continue;
                if (!byShift.TryGetValue((date, name), out var sw)) continue;

                if (!s.WeatherCode.HasValue) { s.WeatherCode = sw.WeatherCode; any = true; }
                if (!s.WeatherTempAvg.HasValue && sw.TempAvg.HasValue) { s.WeatherTempAvg = sw.TempAvg.Value; any = true; }
                if (!s.WeatherPrecipMm.HasValue && sw.PrecipMm.HasValue) { s.WeatherPrecipMm = sw.PrecipMm.Value; any = true; }
                if (!s.WeatherWindMaxKmh.HasValue && sw.WindMaxKmh.HasValue) { s.WeatherWindMaxKmh = sw.WindMaxKmh.Value; any = true; }
            }

            if (any) await db.SaveChangesAsync(ct);
        }
    }
}

