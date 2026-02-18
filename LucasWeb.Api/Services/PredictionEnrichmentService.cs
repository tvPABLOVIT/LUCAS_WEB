using System.Globalization;
using System.Text.Json;

namespace LucasWeb.Api.Services;

/// <summary>Enriquece el JSON de días de predicción con clima, festivos y aplica factores de patrones (lluvia, festivos, temperatura).</summary>
public class PredictionEnrichmentService
{
    private readonly IWeatherService _weather;
    private readonly IHolidaysService _holidays;
    private readonly IDetectedPatternsService _patterns;
    private readonly IEventsService _events;

    public PredictionEnrichmentService(IWeatherService weather, IHolidaysService holidays, IDetectedPatternsService patterns, IEventsService events)
    {
        _weather = weather;
        _holidays = holidays;
        _patterns = patterns;
        _events = events;
    }

    /// <summary>Enriquece dailyPredictionsJson: añade clima/festivos por día y aplica rainFactor, holidayFactor, tempFactor según patrones.</summary>
    public async Task<string?> EnrichDailyPredictionsAsync(string? dailyPredictionsJson, DateTime weekStartMonday, decimal? lat, decimal? lon, string? countryCode)
    {
        if (string.IsNullOrWhiteSpace(dailyPredictionsJson)) return dailyPredictionsJson;
        List<JsonElement>? days;
        try { days = JsonSerializer.Deserialize<List<JsonElement>>(dailyPredictionsJson); } catch { return dailyPredictionsJson; }
        if (days == null || days.Count == 0) return dailyPredictionsJson;

        var end = weekStartMonday.AddDays(6);
        var weatherList = lat.HasValue && lon.HasValue ? await _weather.GetWeatherForRangeAsync(weekStartMonday, end, lat, lon) : new List<WeatherDayInfo>();
        var holidayList = await _holidays.GetHolidaysInRangeAsync(weekStartMonday, end, countryCode);
        var eventsList = await _events.GetEventsInRangeAsync(weekStartMonday, end);
        var weatherByDate = weatherList.ToDictionary(w => w.Date.Date);
        var holidayByDate = holidayList.Where(h => h.IsHoliday).ToDictionary(h => h.Date.Date);
        var eventByDate = eventsList.GroupBy(e => e.Date.Date).ToDictionary(g => g.Key, g => g.ToList());

        var rainPattern = await _patterns.GetPatternAsync("Impacto clima lluvioso");
        var holidayPattern = await _patterns.GetPatternAsync("Impacto festivos");
        var tempPattern = await _patterns.GetPatternAsync("Impacto temperatura");
        ParseRainFactor(rainPattern, out var rainFactor, out var rainBlend);
        ParseHolidayFactor(holidayPattern, out var holidayFactorDefault, out var holidayBlend);
        ParseTempFactor(tempPattern, out var tempExtremeFactor, out var tempBlend);

        var enriched = new List<Dictionary<string, object?>>();
        foreach (var d in days)
        {
            var dict = JsonElementToDict(d);
            var dateStr = (dict.ContainsKey("date") ? dict["date"]?.ToString() : null) ?? (dict.ContainsKey("Date") ? dict["Date"]?.ToString() : null);
            if (string.IsNullOrEmpty(dateStr) || !DateTime.TryParse(dateStr, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)) { enriched.Add(dict); continue; }
            var dayDate = date.Date;

            if (weatherByDate.TryGetValue(dayDate, out var w))
            {
                dict["weatherDescription"] = string.IsNullOrWhiteSpace(w.Description) ? "Sin datos" : w.Description;
                dict["weatherCode"] = w.WeatherCode;
                dict["tempMax"] = w.TempMax;
                dict["tempMin"] = w.TempMin;
                dict["precipMm"] = w.PrecipitationSumMm;
                dict["windMaxKmh"] = w.WindSpeedMaxKmh;
                // Lluvia: por código WMO o por precipitación acumulada.
                dict["isRainy"] = w.IsRainy || (w.PrecipitationSumMm.HasValue && w.PrecipitationSumMm.Value >= 0.5m);
            }
            else
                dict["weatherDescription"] = "Sin datos de previsión";
            var isHoliday = holidayByDate.ContainsKey(dayDate);
            if (isHoliday && holidayByDate.TryGetValue(dayDate, out var h)) { dict["isHoliday"] = true; dict["holidayName"] = h.Name; }
            else dict["isHoliday"] = false;

            static decimal GetDec(object? o)
            {
                if (o == null) return 0m;
                if (o is decimal dd) return dd;
                if (o is int ii) return ii;
                if (o is long ll) return ll;
                if (o is double dbl) return (decimal)dbl;
                if (o is float fl) return (decimal)fl;
                if (decimal.TryParse(o.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed)) return parsed;
                return 0m;
            }

            decimal rev = 0;
            if (dict.TryGetValue("revenue", out var rv)) rev = GetDec(rv);
            else if (dict.TryGetValue("predictedRevenue", out rv)) rev = GetDec(rv);
            if (rev > 0)
            {
                dict["revenue_base"] = rev;
                decimal weatherFactor = 1m;
                if (dict.TryGetValue("isRainy", out var rainy) && rainy is true) weatherFactor = 1 + (rainFactor - 1) * rainBlend;
                decimal holidayFactor = 1m;
                if (isHoliday) holidayFactor = 1 + (holidayFactorDefault - 1) * holidayBlend;
                decimal tempFactor = 1m;
                if (dict.TryGetValue("tempMax", out var tMax) && tMax is decimal tm && (tm < 5 || tm > 30)) tempFactor = 1 + (tempExtremeFactor - 1) * tempBlend;
                else if (dict.TryGetValue("tempMin", out var tMin) && tMin is decimal tn && (tn < 5 || tn > 30)) tempFactor = 1 + (tempExtremeFactor - 1) * tempBlend;
                decimal eventFactor = 1m;
                if (eventByDate.TryGetValue(dayDate, out var dayEvents) && dayEvents.Count > 0)
                {
                    decimal ev = 1m;
                    foreach (var evt in dayEvents)
                    {
                        var imp = (evt.Impact ?? "").Trim().ToUpperInvariant();
                        if (imp == "ALTO") { ev = 1.10m; break; }
                        if (imp == "BAJO") ev = 0.90m;
                    }
                    eventFactor = 1 + (ev - 1) * 0.5m;
                }
                var mult = Math.Clamp(weatherFactor * holidayFactor * tempFactor * eventFactor, EnrichmentMultMin, EnrichmentMultMax);

                // Desglose de factores para explicabilidad en UI.
                dict["factors"] = new
                {
                    weatherFactor,
                    holidayFactor,
                    tempFactor,
                    eventFactor,
                    totalFactor = mult
                };

                var newRev = Math.Round(rev * mult, 2);
                dict["revenue"] = newRev;
                dict["predictedRevenue"] = newRev;

                // Escalar rangos y turnos si existen.
                if (dict.TryGetValue("min", out var mn)) dict["min"] = Math.Round(GetDec(mn) * mult, 2);
                if (dict.TryGetValue("max", out var mx)) dict["max"] = Math.Round(GetDec(mx) * mult, 2);
                if (dict.TryGetValue("mediodia", out var m)) dict["mediodia"] = Math.Round(GetDec(m) * mult, 2);
                if (dict.TryGetValue("tarde", out var t)) dict["tarde"] = Math.Round(GetDec(t) * mult, 2);
                if (dict.TryGetValue("noche", out var n)) dict["noche"] = Math.Round(GetDec(n) * mult, 2);
            }
            enriched.Add(dict);
        }
        return JsonSerializer.Serialize(enriched);
    }

    private const decimal PatternFactorMin = 0.90m;
    private const decimal PatternFactorMax = 1.10m;
    private const decimal EnrichmentMultMin = 0.90m;
    private const decimal EnrichmentMultMax = 1.06m;
    private const decimal MinCountForFullBlend = 10m;
    private const decimal BlendSampleScaleMin = 0.5m;

    private static void ParseRainFactor(PatternData? p, out decimal rainFactor, out decimal rainBlend)
    {
        rainFactor = 1m; rainBlend = 0.5m;
        if (p?.JsonData == null) return;
        try
        {
            var doc = JsonDocument.Parse(p.JsonData);
            if (doc.RootElement.TryGetProperty("rainFactor", out var rf)) rainFactor = Math.Clamp(rf.GetDecimal(), PatternFactorMin, PatternFactorMax);
            else if (doc.RootElement.TryGetProperty("pct_diff", out var pd)) rainFactor = Math.Clamp(1 + pd.GetDecimal() / 100m, PatternFactorMin, PatternFactorMax);
            rainBlend = 0.25m + (p.Confidence / 100m) * 0.5m;
            if (doc.RootElement.TryGetProperty("count_rainy", out var cr) && doc.RootElement.TryGetProperty("count_sunny", out var cs))
            {
                var minCount = Math.Min(cr.GetInt32(), cs.GetInt32());
                rainBlend *= Math.Clamp((decimal)minCount / MinCountForFullBlend, BlendSampleScaleMin, 1m);
            }
        }
        catch { }
    }

    private static void ParseHolidayFactor(PatternData? p, out decimal holidayFactorDefault, out decimal holidayBlend)
    {
        holidayFactorDefault = 1m; holidayBlend = 0.5m;
        if (p?.JsonData == null) return;
        try
        {
            var doc = JsonDocument.Parse(p.JsonData);
            if (doc.RootElement.TryGetProperty("holidayFactor", out var hf)) holidayFactorDefault = Math.Clamp(hf.GetDecimal(), PatternFactorMin, PatternFactorMax);
            else if (doc.RootElement.TryGetProperty("pct_diff", out var pd)) holidayFactorDefault = Math.Clamp(1 + pd.GetDecimal() / 100m, PatternFactorMin, PatternFactorMax);
            holidayBlend = 0.25m + (p.Confidence / 100m) * 0.5m;
            if (doc.RootElement.TryGetProperty("count_holiday", out var ch) && doc.RootElement.TryGetProperty("count_not_holiday", out var cnh))
            {
                var minCount = Math.Min(ch.GetInt32(), cnh.GetInt32());
                holidayBlend *= Math.Clamp((decimal)minCount / MinCountForFullBlend, BlendSampleScaleMin, 1m);
            }
        }
        catch { }
    }

    private static void ParseTempFactor(PatternData? p, out decimal tempExtremeFactor, out decimal tempBlend)
    {
        tempExtremeFactor = 1m; tempBlend = 0.5m;
        if (p?.JsonData == null) return;
        try
        {
            var doc = JsonDocument.Parse(p.JsonData);
            if (doc.RootElement.TryGetProperty("tempFactor", out var tf)) tempExtremeFactor = Math.Clamp(tf.GetDecimal(), PatternFactorMin, PatternFactorMax);
            else if (doc.RootElement.TryGetProperty("pct_diff", out var pd)) tempExtremeFactor = Math.Clamp(1 + pd.GetDecimal() / 100m, PatternFactorMin, PatternFactorMax);
            tempBlend = 0.25m + (p.Confidence / 100m) * 0.5m;
            if (doc.RootElement.TryGetProperty("count_extreme", out var ce) && doc.RootElement.TryGetProperty("count_mild", out var cm))
            {
                var minCount = Math.Min(ce.GetInt32(), cm.GetInt32());
                tempBlend *= Math.Clamp((decimal)minCount / MinCountForFullBlend, BlendSampleScaleMin, 1m);
            }
        }
        catch { }
    }

    private static Dictionary<string, object?> JsonElementToDict(JsonElement e)
    {
        var d = new Dictionary<string, object?>();
        foreach (var p in e.EnumerateObject())
        {
            d[p.Name] = p.Value.ValueKind switch
            {
                JsonValueKind.String => p.Value.GetString(),
                JsonValueKind.Number => p.Value.TryGetInt32(out var i) ? i : p.Value.GetDecimal(),
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                JsonValueKind.Null => null,
                _ => p.Value.GetRawText()
            };
        }
        return d;
    }
}
