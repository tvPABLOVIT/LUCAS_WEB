using System.Globalization;
using System.Text.Json;

namespace LucasWeb.Api.Services;

/// <summary>Clima vía Open-Meteo (sin API key).</summary>
public class WeatherService : IWeatherService
{
    private readonly HttpClient _http;
    private const string ForecastUrl = "https://api.open-meteo.com/v1/forecast";
    private const string ArchiveUrl = "https://archive-api.open-meteo.com/v1/archive";
    private const string Timezone = "Europe%2FMadrid";

    public WeatherService(IHttpClientFactory factory)
    {
        _http = factory.CreateClient();
        _http.Timeout = TimeSpan.FromSeconds(10);
    }

    public async Task<IReadOnlyList<WeatherDayInfo>> GetWeatherForRangeAsync(DateTime startDate, DateTime endDate, decimal? latitude, decimal? longitude)
    {
        if (!latitude.HasValue || !longitude.HasValue) return Array.Empty<WeatherDayInfo>();
        var start = startDate.Date.ToString("yyyy-MM-dd");
        var end = endDate.Date.ToString("yyyy-MM-dd");
        // Para días pasados, usar Archive API (más fiable y soporta rangos largos).
        // Para hoy/futuro, usar Forecast API.
        var today = DateTime.UtcNow.Date;
        var baseUrl = endDate.Date <= today.AddDays(-1) ? ArchiveUrl : ForecastUrl;
        var url = $"{baseUrl}?latitude={latitude.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)}&longitude={longitude.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)}&daily=weather_code,temperature_2m_max,temperature_2m_min,precipitation_sum,wind_speed_10m_max&timezone={Timezone}&start_date={start}&end_date={end}";
        try
        {
            var response = await _http.GetAsync(url);
            response.EnsureSuccessStatusCode();
            var json = await response.Content.ReadAsStringAsync();
            var doc = JsonDocument.Parse(json);
            var list = new List<WeatherDayInfo>();
            if (doc.RootElement.TryGetProperty("daily", out var daily))
            {
                var times = GetStringArray(daily, "time");
                var codes = GetIntArray(daily, "weather_code");
                var maxT = GetDecimalArray(daily, "temperature_2m_max");
                var minT = GetDecimalArray(daily, "temperature_2m_min");
                var prec = GetDecimalArray(daily, "precipitation_sum");
                var wind = GetDecimalArray(daily, "wind_speed_10m_max");
                for (var i = 0; i < (times?.Count ?? 0); i++)
                {
                    var date = DateTime.MinValue;
                    if (times != null && i < times.Count && DateTime.TryParse(times[i], CultureInfo.InvariantCulture, DateTimeStyles.None, out var d)) date = d.Date;
                    var code = codes != null && i < codes.Count ? codes[i] : 0;
                    list.Add(new WeatherDayInfo
                    {
                        Date = date,
                        WeatherCode = code,
                        Description = WmoDescription(code),
                        TempMax = maxT != null && i < maxT.Count ? maxT[i] : null,
                        TempMin = minT != null && i < minT.Count ? minT[i] : null,
                        PrecipitationSumMm = prec != null && i < prec.Count ? prec[i] : null,
                        WindSpeedMaxKmh = wind != null && i < wind.Count ? wind[i] : null
                    });
                }
            }
            return list;
        }
        catch
        {
            return Array.Empty<WeatherDayInfo>();
        }
    }

    public async Task<IReadOnlyList<WeatherHourInfo>> GetHourlyWeatherForRangeAsync(DateTime startDate, DateTime endDate, decimal? latitude, decimal? longitude)
    {
        if (!latitude.HasValue || !longitude.HasValue) return Array.Empty<WeatherHourInfo>();
        var start = startDate.Date.ToString("yyyy-MM-dd");
        var end = endDate.Date.ToString("yyyy-MM-dd");
        var today = DateTime.UtcNow.Date;
        var baseUrl = endDate.Date <= today.AddDays(-1) ? ArchiveUrl : ForecastUrl;

        var url = $"{baseUrl}?latitude={latitude.Value.ToString(CultureInfo.InvariantCulture)}&longitude={longitude.Value.ToString(CultureInfo.InvariantCulture)}&hourly=weather_code,temperature_2m,precipitation,wind_speed_10m&timezone={Timezone}&start_date={start}&end_date={end}";
        try
        {
            var response = await _http.GetAsync(url);
            response.EnsureSuccessStatusCode();
            var json = await response.Content.ReadAsStringAsync();
            var doc = JsonDocument.Parse(json);
            var list = new List<WeatherHourInfo>();
            if (doc.RootElement.TryGetProperty("hourly", out var hourly))
            {
                var times = GetStringArray(hourly, "time");
                var codes = GetIntArray(hourly, "weather_code");
                var temps = GetDecimalArray(hourly, "temperature_2m");
                var prec = GetDecimalArray(hourly, "precipitation");
                var wind = GetDecimalArray(hourly, "wind_speed_10m");
                for (var i = 0; i < (times?.Count ?? 0); i++)
                {
                    if (times == null) break;
                    if (!DateTime.TryParse(times[i], CultureInfo.InvariantCulture, DateTimeStyles.None, out var t))
                        continue;
                    list.Add(new WeatherHourInfo
                    {
                        Time = t,
                        WeatherCode = codes != null && i < codes.Count ? codes[i] : 0,
                        Temperature2m = temps != null && i < temps.Count ? temps[i] : null,
                        PrecipitationMm = prec != null && i < prec.Count ? prec[i] : null,
                        WindSpeed10mKmh = wind != null && i < wind.Count ? wind[i] : null
                    });
                }
            }
            return list;
        }
        catch
        {
            return Array.Empty<WeatherHourInfo>();
        }
    }

    public async Task<IReadOnlyList<WeatherShiftInfo>> GetShiftWeatherForRangeAsync(DateTime startDate, DateTime endDate, decimal? latitude, decimal? longitude)
    {
        var hours = await GetHourlyWeatherForRangeAsync(startDate, endDate, latitude, longitude);
        if (hours.Count == 0) return Array.Empty<WeatherShiftInfo>();

        // Ventanas por turno (hora local): [12-16), [16-20), [20-24)
        var windows = new[]
        {
            new { Name = "Mediodia", StartHour = 12, EndHour = 16 },
            new { Name = "Tarde", StartHour = 16, EndHour = 20 },
            new { Name = "Noche", StartHour = 20, EndHour = 24 }
        };

        var byDate = hours
            .Where(h => h.Time != DateTime.MinValue)
            .GroupBy(h => h.Time.Date)
            .ToDictionary(g => g.Key, g => g.ToList());

        var result = new List<WeatherShiftInfo>();
        for (var d = startDate.Date; d <= endDate.Date; d = d.AddDays(1))
        {
            if (!byDate.TryGetValue(d, out var dayHours) || dayHours.Count == 0) continue;
            foreach (var w in windows)
            {
                var slice = dayHours.Where(h => h.Time.Hour >= w.StartHour && h.Time.Hour < w.EndHour).ToList();
                if (slice.Count == 0) continue;

                var code = PickMostSevereCode(slice.Select(x => x.WeatherCode));
                var temps = slice.Where(x => x.Temperature2m.HasValue).Select(x => x.Temperature2m!.Value).ToList();
                var prec = slice.Where(x => x.PrecipitationMm.HasValue).Select(x => x.PrecipitationMm!.Value).ToList();
                var wind = slice.Where(x => x.WindSpeed10mKmh.HasValue).Select(x => x.WindSpeed10mKmh!.Value).ToList();

                result.Add(new WeatherShiftInfo
                {
                    Date = d,
                    ShiftName = w.Name,
                    WeatherCode = code,
                    Description = WmoDescription(code),
                    TempAvg = temps.Count > 0 ? Math.Round(temps.Average(), 2) : null,
                    PrecipMm = prec.Count > 0 ? Math.Round(prec.Sum(), 2) : null,
                    WindMaxKmh = wind.Count > 0 ? Math.Round(wind.Max(), 2) : null
                });
            }
        }
        return result;
    }

    private static int PickMostSevereCode(IEnumerable<int> codes)
    {
        var best = 0;
        var bestScore = -1;
        foreach (var c in codes)
        {
            var s = SeverityScore(c);
            if (s > bestScore) { bestScore = s; best = c; }
        }
        return best;
    }

    private static int SeverityScore(int code)
    {
        // Score aproximado: tormenta > nieve/granizo > lluvia fuerte > lluvia > llovizna > niebla > nublado > despejado
        if (code is 99 or 96 or 95) return 100;
        if (code is 86 or 85 or 77) return 90;
        if (code is 75 or 73 or 71) return 85;
        if (code is 67 or 66) return 82;
        if (code is 65) return 80;
        if (code is 82) return 78;
        if (code is 63 or 81) return 70;
        if (code is 61 or 80) return 60;
        if (code is 57 or 56) return 55;
        if (code is 55) return 50;
        if (code is 53) return 45;
        if (code is 51) return 40;
        if (code is 48 or 45) return 30;
        if (code is 3) return 20;
        if (code is 2) return 15;
        if (code is 1) return 10;
        if (code is 0) return 0;
        return 5;
    }

    private static List<string>? GetStringArray(JsonElement parent, string name)
    {
        if (!parent.TryGetProperty(name, out var arr) || arr.ValueKind != JsonValueKind.Array) return null;
        var list = new List<string>();
        foreach (var e in arr.EnumerateArray())
            list.Add(e.GetString() ?? "");
        return list;
    }

    private static List<int>? GetIntArray(JsonElement parent, string name)
    {
        if (!parent.TryGetProperty(name, out var arr) || arr.ValueKind != JsonValueKind.Array) return null;
        var list = new List<int>();
        foreach (var e in arr.EnumerateArray())
            list.Add(e.TryGetInt32(out var v) ? v : 0);
        return list;
    }

    private static List<decimal>? GetDecimalArray(JsonElement parent, string name)
    {
        if (!parent.TryGetProperty(name, out var arr) || arr.ValueKind != JsonValueKind.Array) return null;
        var list = new List<decimal>();
        foreach (var e in arr.EnumerateArray())
            list.Add(e.TryGetDecimal(out var v) ? v : 0);
        return list;
    }

    private static string WmoDescription(int code)
    {
        return code switch
        {
            0 => "Despejado",
            1 => "Mayormente despejado",
            2 => "Parcialmente nublado",
            3 => "Nublado",
            45 => "Niebla",
            48 => "Niebla escarchada",
            51 => "Llovizna ligera",
            53 => "Llovizna",
            55 => "Llovizna densa",
            56 => "Llovizna helada ligera",
            57 => "Llovizna helada densa",
            61 => "Lluvia ligera",
            63 => "Lluvia moderada",
            65 => "Lluvia fuerte",
            66 => "Lluvia helada ligera",
            67 => "Lluvia helada fuerte",
            71 => "Nieve ligera",
            73 => "Nieve moderada",
            75 => "Nieve fuerte",
            77 => "Granizo",
            80 => "Chubascos ligeros",
            81 => "Chubascos moderados",
            82 => "Chubascos fuertes",
            85 => "Chubascos de nieve ligeros",
            86 => "Chubascos de nieve fuertes",
            95 => "Tormenta",
            96 => "Tormenta con granizo",
            99 => "Tormenta fuerte con granizo",
            _ => "Código " + code
        };
    }
}
