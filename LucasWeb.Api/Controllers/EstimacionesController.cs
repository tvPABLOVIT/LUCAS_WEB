using LucasWeb.Api.Data;
using LucasWeb.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.Text.Json;

namespace LucasWeb.Api.Controllers;

[ApiController]
[Route("api/estimaciones")]
[Authorize]
public class EstimacionesController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly NextWeekPredictionService _predictionService;
    private readonly IWeatherService _weather;
    private readonly IHolidaysService _holidays;
    private readonly IEventsService _events;
    private readonly IDetectedPatternsService _patterns;

    public EstimacionesController(AppDbContext db, NextWeekPredictionService predictionService, IWeatherService weather, IHolidaysService holidays, IEventsService events, IDetectedPatternsService patterns)
    {
        _db = db;
        _predictionService = predictionService;
        _weather = weather;
        _holidays = holidays;
        _events = events;
        _patterns = patterns;
    }

    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] string? weekStart)
    {
        var cache = await _db.EstimacionesCaches.OrderByDescending(c => c.UpdatedAt).FirstOrDefaultAsync();
        if (cache != null && !string.IsNullOrEmpty(cache.JsonPayload))
        {
            try
            {
                var obj = JsonSerializer.Deserialize<JsonElement>(cache.JsonPayload);
                return Ok(obj);
            }
            catch { }
        }
        return await BuildEstimacionesPayloadAsync(weekStart);
    }

    [HttpPost("cache")]
    [Authorize]
    public async Task<IActionResult> SaveCache([FromBody] JsonElement body)
    {
        var cache = await _db.EstimacionesCaches.FirstOrDefaultAsync();
        if (cache == null)
        {
            cache = new Models.EstimacionesCache
            {
                Id = Guid.NewGuid(),
                JsonPayload = "{}",
                UpdatedAt = DateTime.UtcNow
            };
            _db.EstimacionesCaches.Add(cache);
        }
        cache.JsonPayload = body.GetRawText();
        cache.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return Ok();
    }

    /// <summary>Alertas "Qué puede afectar la semana siguiente" (orden y formatos según ANALISIS_BLOQUE_QUE_PUEDE_AFECTAR_SEMANA_SIGUIENTE.md).</summary>
    [HttpGet("alertas")]
    public async Task<IActionResult> GetAlertas()
    {
        var today = DateTime.UtcNow.Date;
        var nextMonday = NextWeekPredictionService.GetNextMonday(today);
        var nextEnd = nextMonday.AddDays(6);
        decimal? nextWeekRevenue = null;
        string? dailyJson = null;
        var pred = await _db.WeeklyPredictions.FirstOrDefaultAsync(p => p.WeekStartMonday == nextMonday);
        if (pred != null && pred.PredictedRevenue.HasValue && pred.PredictedRevenue > 0)
        {
            nextWeekRevenue = pred.PredictedRevenue;
            dailyJson = pred.DailyPredictionsJson;
        }
        else
        {
            var (_, total, daily) = await _predictionService.ComputeLivePredictionAsync();
            if (total > 0) nextWeekRevenue = total;
            dailyJson = daily;
        }

        var prevMonday = nextMonday.AddDays(-7);
        var prevEnd = prevMonday.AddDays(6);
        var prevDays = await _db.ExecutionDays
            .Where(e => !e.IsFeedbackOnly && e.Date >= prevMonday && e.Date <= prevEnd)
            .Select(e => new { e.TotalRevenue, e.TotalHoursWorked })
            .ToListAsync();
        decimal prevWeekRevenue = 0;
        decimal prevWeekHours = 0;
        decimal? prevWeekProductivity = null;
        if (prevDays.Count > 0)
        {
            prevWeekRevenue = prevDays.Sum(d => d.TotalRevenue);
            prevWeekHours = prevDays.Sum(d => d.TotalHoursWorked);
            if (prevWeekHours > 0) prevWeekProductivity = prevWeekRevenue / prevWeekHours;
        }

        decimal? tendenciaPct = null;
        string tendenciaTitulo = "Facturación estable";
        string tendenciaTexto;
        if (!nextWeekRevenue.HasValue || prevWeekRevenue <= 0)
            tendenciaTexto = "No hay datos de la semana anterior para comparar.";
        else
        {
            tendenciaPct = (nextWeekRevenue.Value - prevWeekRevenue) / prevWeekRevenue * 100;
            if (tendenciaPct > 1)
            {
                tendenciaTitulo = "Facturación al alza";
                tendenciaTexto = "Esperamos facturar un " + tendenciaPct.Value.ToString("F0", CultureInfo.InvariantCulture) + "% más que la semana anterior.";
            }
            else if (tendenciaPct < -1)
            {
                tendenciaTitulo = "Facturación a la baja";
                tendenciaTexto = "Esperamos facturar un " + Math.Abs(tendenciaPct.Value).ToString("F0", CultureInfo.InvariantCulture) + "% menos que la semana anterior.";
            }
            else
            {
                tendenciaTexto = "Esperamos facturar en línea con la semana anterior.";
            }
        }

        var sameStart = nextMonday.AddMonths(-1);
        var sameEndPrev = nextEnd.AddMonths(-1);
        var sameDays = await _db.ExecutionDays
            .Where(e => !e.IsFeedbackOnly && e.Date >= sameStart && e.Date <= sameEndPrev)
            .Select(e => e.TotalRevenue)
            .ToListAsync();
        decimal? prevMonthRevenue = sameDays.Count > 0 ? sameDays.Sum() : null;
        var rangoNext = nextMonday.ToString("dd/MM", CultureInfo.InvariantCulture) + " al " + nextEnd.ToString("dd/MM", CultureInfo.InvariantCulture);
        var rangoPrev = sameStart.ToString("dd/MM", CultureInfo.InvariantCulture) + " al " + sameEndPrev.ToString("dd/MM", CultureInfo.InvariantCulture);
        string mismaSemanaTexto;
        if (prevMonthRevenue == null || prevMonthRevenue <= 0)
            mismaSemanaTexto = "No hay datos de las mismas fechas del mes anterior (" + rangoPrev + ") para comparar con la semana siguiente (" + rangoNext + ").";
        else if (nextWeekRevenue.HasValue && nextWeekRevenue > 0)
        {
            var pctMes = (nextWeekRevenue.Value - prevMonthRevenue.Value) / prevMonthRevenue.Value * 100;
            var masMenos = pctMes > 1 ? "un " + pctMes.ToString("F0", CultureInfo.InvariantCulture) + "% más" : pctMes < -1 ? "un " + Math.Abs(pctMes).ToString("F0", CultureInfo.InvariantCulture) + "% menos" : "en línea";
            mismaSemanaTexto = "Esperamos facturar " + masMenos + " que los mismos días del mes pasado. El mes pasado, para esas fechas (" + rangoPrev + ") facturaste " + prevMonthRevenue.Value.ToString("F0", CultureInfo.InvariantCulture) + " €.";
        }
        else
            mismaSemanaTexto = "El mes pasado, para esas fechas (" + rangoPrev + ") facturaste " + prevMonthRevenue.Value.ToString("F0", CultureInfo.InvariantCulture) + " €.";

        var alertas = new List<AlertaItem>();
        var inv = CultureInfo.InvariantCulture;
        var es = new CultureInfo("es-ES");
        const int ordenAlto = 1, ordenMedio = 2, ordenContexto = 3, ordenBajo = 4, ordenMeta = 5;

        // 1. Tendencia solo cuando hay datos para comparar (evita duplicar "no hay datos" con Semana anterior)
        if (nextWeekRevenue.HasValue && prevWeekRevenue > 0)
            alertas.Add(new AlertaItem("tendencia", tendenciaTitulo, tendenciaTexto, ordenAlto, tendenciaPct, "Compara la estimación de la semana siguiente con la facturación real de la semana pasada."));

        // 2. Semana anterior (siempre) - contexto
        if (prevWeekRevenue > 0 || prevWeekHours > 0)
            alertas.Add(new AlertaItem("semanaAnterior", "Semana anterior", "Semana del " + prevMonday.ToString("dd/MM", inv) + " al " + prevEnd.ToString("dd/MM", inv) + ": " + prevWeekRevenue.ToString("F0", inv) + " € facturados, " + prevWeekHours.ToString("F0", inv) + " h trabajadas, " + (prevWeekProductivity?.ToString("F1", inv) ?? "—") + " €/h de productividad.", ordenContexto, null, "Referente real más reciente para calibrar expectativas."));
        else
            alertas.Add(new AlertaItem("semanaAnterior", "Semana anterior", "No hay datos de la semana anterior (" + prevMonday.ToString("dd/MM", inv) + " al " + prevEnd.ToString("dd/MM", inv) + ").", ordenContexto, null, "Referente real más reciente."));

        // 3. Clima (siempre coordenadas de Configuración: LatRestaurante/LonRestaurante)
        decimal? lat = null, lon = null;
        string? countryCode = null;
        var latS = await _db.Settings.AsNoTracking().FirstOrDefaultAsync(s => s.Key == "LatRestaurante");
        var lonS = await _db.Settings.AsNoTracking().FirstOrDefaultAsync(s => s.Key == "LonRestaurante");
        var ccS = await _db.Settings.AsNoTracking().FirstOrDefaultAsync(s => s.Key == "CountryCode");
        if (latS != null && decimal.TryParse(latS.Value.Replace(",", "."), NumberStyles.Any, inv, out var la)) lat = la;
        if (lonS != null && decimal.TryParse(lonS.Value.Replace(",", "."), NumberStyles.Any, inv, out var lo)) lon = lo;
        if (ccS != null) countryCode = ccS.Value;

        var weatherList = await _weather.GetWeatherForRangeAsync(nextMonday, nextEnd, lat, lon);
        if (weatherList.Count > 0)
        {
            var rainyDays = weatherList.Where(w => w.IsRainy || (w.PrecipitationSumMm.HasValue && w.PrecipitationSumMm.Value >= 0.5m)).ToList();
            if (rainyDays.Count > 0)
            {
                var rainText = "Lluvia prevista: " + string.Join(", ", rainyDays.Select(w => DayNameSpanish(w.Date, es) + " " + w.Date.ToString("dd/MM", inv)));
                alertas.Add(new AlertaItem("clima", "Clima semana siguiente", rainText + ".", ordenAlto, null, "La lluvia suele afectar demanda (terraza, paseos).", rainyDays.Select(x => x.Date.ToString("yyyy-MM-dd")).ToList()));
            }
            else
                alertas.Add(new AlertaItem("clima", "Clima semana siguiente", "Sin días de lluvia previstos para la semana siguiente.", ordenAlto, null, "La lluvia suele afectar demanda."));

            var heavyRain = weatherList.Where(w => w.PrecipitationSumMm.HasValue && w.PrecipitationSumMm.Value >= 5m).ToList();
            if (heavyRain.Count >= 1)
            {
                var txt = "Lluvia intensa: " + string.Join(", ", heavyRain.Select(w => DayNameSpanish(w.Date, es) + " " + w.Date.ToString("dd/MM", inv) + " (" + w.PrecipitationSumMm!.Value.ToString("F0", inv) + " mm)"));
                alertas.Add(new AlertaItem("climaLluviaIntensa", "Lluvia intensa", txt + ".", ordenAlto, null, "Con lluvia intensa suele caer terraza y el tráfico a pie.", heavyRain.Select(x => x.Date.ToString("yyyy-MM-dd")).ToList()));
            }

            var windy = weatherList.Where(w => w.WindSpeedMaxKmh.HasValue && w.WindSpeedMaxKmh.Value >= 35m).ToList();
            if (windy.Count >= 1)
            {
                var txt = "Viento fuerte: " + string.Join(", ", windy.Select(w => DayNameSpanish(w.Date, es) + " " + w.Date.ToString("dd/MM", inv) + " (" + w.WindSpeedMaxKmh!.Value.ToString("F0", inv) + " km/h)"));
                alertas.Add(new AlertaItem("climaViento", "Viento", txt + ".", ordenAlto, null, "El viento reduce confort exterior y puede afectar demanda.", windy.Select(x => x.Date.ToString("yyyy-MM-dd")).ToList()));
            }

            var extremeTempDays = weatherList.Where(w => (w.TempMax.HasValue && w.TempMax.Value > 30) || (w.TempMin.HasValue && w.TempMin.Value < 5)).ToList();
            if (extremeTempDays.Count >= 2)
            {
                var calor = extremeTempDays.Count(w => w.TempMax.HasValue && w.TempMax.Value > 30);
                var frio = extremeTempDays.Count(w => w.TempMin.HasValue && w.TempMin.Value < 5);
                var tempTexto = calor >= 2 ? "Ola de calor prevista (" + calor + " días con máx > 30 °C)." : (frio >= 2 ? "Ola de frío prevista (" + frio + " días con mín < 5 °C)." : "Temperaturas extremas previstas (" + extremeTempDays.Count + " días).");
                alertas.Add(new AlertaItem("temperatura", "Temperatura", tempTexto, ordenAlto, null, "Anticipar terraza, climatización y demanda.", extremeTempDays.Select(x => x.Date.ToString("yyyy-MM-dd")).ToList()));
            }
        }

        // Festivos (solo si hay al menos uno con nombre no vacío)
        var holidayList = await _holidays.GetHolidaysInRangeAsync(nextMonday, nextEnd, countryCode);
        var festivosConNombre = holidayList.Where(h => h.IsHoliday && !string.IsNullOrWhiteSpace(h.Name)).ToList();
        if (festivosConNombre.Count > 0)
        {
            var festivosTexto = string.Join(" ", festivosConNombre.Select(f => f.Name + " el " + DayNameSpanish(f.Date, es) + " " + f.Date.ToString("dd/MM", inv) + "."));
            alertas.Add(new AlertaItem("festivos", "Festivos", festivosTexto, ordenAlto, null, "Los festivos cambian demanda y horarios.", festivosConNombre.Select(x => x.Date.ToString("yyyy-MM-dd")).ToList()));
        }

        // Misma semana, mes anterior (siempre)
        decimal? pctMesImpact = null;
        if (prevMonthRevenue.HasValue && prevMonthRevenue > 0 && nextWeekRevenue.HasValue && nextWeekRevenue > 0)
            pctMesImpact = (nextWeekRevenue.Value - prevMonthRevenue.Value) / prevMonthRevenue.Value * 100;
        alertas.Add(new AlertaItem("mismaSemanaMesAnterior", "Misma semana, mes anterior", mismaSemanaTexto, ordenMedio, pctMesImpact, "Estacionalidad: mismas fechas numéricas del mes pasado.", null));

        // Misma semana, año anterior (si hay datos)
        var sameWeekLastYearStart = nextMonday.AddYears(-1);
        var sameWeekLastYearEnd = nextEnd.AddYears(-1);
        var lastYearDays = await _db.ExecutionDays
            .Where(e => !e.IsFeedbackOnly && e.Date >= sameWeekLastYearStart && e.Date <= sameWeekLastYearEnd)
            .Select(e => e.TotalRevenue)
            .ToListAsync();
        if (lastYearDays.Count > 0)
        {
            var lastYearRevenue = lastYearDays.Sum();
            if (lastYearRevenue > 0)
            {
                var textoAno = "Hace un año (misma semana): facturaste " + lastYearRevenue.ToString("F0", inv) + " €.";
                if (nextWeekRevenue.HasValue && nextWeekRevenue > 0)
                {
                    var pctAno = (nextWeekRevenue.Value - lastYearRevenue) / lastYearRevenue * 100;
                    textoAno += " La estimación actual es un " + (pctAno >= 0 ? pctAno.ToString("F0", inv) + "% más" : Math.Abs(pctAno).ToString("F0", inv) + "% menos") + ".";
                }
                decimal? pctAnoImpact = null;
                if (nextWeekRevenue.HasValue && nextWeekRevenue > 0) pctAnoImpact = (nextWeekRevenue.Value - lastYearRevenue) / lastYearRevenue * 100;
                alertas.Add(new AlertaItem("mismaSemanaAnoAnterior", "Misma semana, año anterior", textoAno, ordenMedio, pctAnoImpact, "Estacionalidad interanual.", null));
            }
        }

        // Concentración fin de semana: % facturación en viernes–domingo (desde predicción diaria)
        decimal? totalPredForFinde = null;
        if (!string.IsNullOrWhiteSpace(dailyJson))
        {
            try
            {
                var dailyList = JsonSerializer.Deserialize<List<JsonElement>>(dailyJson);
                if (dailyList != null && dailyList.Count > 0)
                {
                    var withRevenue = dailyList
                        .Select((d, index) =>
                        {
                            var rev = 0m;
                            if (d.TryGetProperty("revenue", out var r)) rev = r.GetDecimal();
                            else if (d.TryGetProperty("predictedRevenue", out r)) rev = r.GetDecimal();
                            return new { Revenue = rev, Index = index };
                        })
                        .Where(x => x.Revenue > 0)
                        .ToList();
                    var totalRev = withRevenue.Sum(x => x.Revenue);
                    var findeRev = withRevenue.Where(x => x.Index == 4 || x.Index == 5 || x.Index == 6).Sum(x => x.Revenue);
                    if (totalRev > 0) totalPredForFinde = findeRev / totalRev * 100;
                }
            }
            catch { }
        }

        // Concentración en fin de semana (con matiz si >60% o <40%)
        if (totalPredForFinde.HasValue)
        {
            var pctFinde = totalPredForFinde.Value;
            var textoFinde = pctFinde.ToString("F0", inv) + "% de la facturación estimada en viernes–domingo.";
            if (pctFinde >= 60)
                textoFinde += " Refuerza mucho el personal en finde.";
            else if (pctFinde < 40)
                textoFinde += " Facturación más repartida en semana.";
            else
                textoFinde += " Útil para reforzar personal en finde.";
            alertas.Add(new AlertaItem(
                "concentracionFinde",
                "Concentración fin de semana",
                textoFinde,
                ordenMedio,
                null,
                "Porcentaje de la estimación que cae en viernes, sábado y domingo.",
                new List<string> { nextMonday.AddDays(4).ToString("yyyy-MM-dd"), nextMonday.AddDays(5).ToString("yyyy-MM-dd"), nextMonday.AddDays(6).ToString("yyyy-MM-dd") }
            ));
        }

        // Coste personal vs estimación
        var prodSetting = await _db.Settings.AsNoTracking().FirstOrDefaultAsync(s => s.Key == "ProductividadIdealEurHora");
        var costeHoraSetting = await _db.Settings.AsNoTracking().FirstOrDefaultAsync(s => s.Key == "CostePersonalPorHora");
        if (nextWeekRevenue.HasValue && nextWeekRevenue > 0 && prodSetting != null && decimal.TryParse(prodSetting.Value.Replace(",", "."), NumberStyles.Any, inv, out var productividad) && productividad > 0 && costeHoraSetting != null && decimal.TryParse(costeHoraSetting.Value.Replace(",", "."), NumberStyles.Any, inv, out var costeHora) && costeHora >= 0)
        {
            var horasEst = nextWeekRevenue.Value / productividad;
            var costeEst = horasEst * costeHora;
            var pctCoste = costeEst / nextWeekRevenue.Value * 100;
            alertas.Add(new AlertaItem("costePersonal", "Coste personal estimado", "Un " + pctCoste.ToString("F1", inv) + "% de la facturación estimada (" + costeEst.ToString("F0", inv) + " € en " + horasEst.ToString("F0", inv) + " h).", ordenMedio, null, "Horas necesarias para la productividad objetivo × coste por hora."));
        }

        // Eventos (solo si hay)
        var eventsInWeek = await _events.GetEventsInRangeAsync(nextMonday, nextEnd);
        if (eventsInWeek.Count > 0)
        {
            var eventosPartes = eventsInWeek.Select(e =>
            {
                var impacto = string.IsNullOrWhiteSpace(e.Impact) || string.Equals(e.Impact, "Medio", StringComparison.OrdinalIgnoreCase) ? "" : ", impacto " + e.Impact;
                return e.Name + " (" + e.Date.ToString("dd/MM", inv) + impacto + ")";
            });
            alertas.Add(new AlertaItem("eventos", "Eventos esta semana", string.Join(". ", eventosPartes) + ".", ordenBajo, null, "Eventos que pueden aumentar o reducir afluencia.", eventsInWeek.Select(x => x.Date.ToString("yyyy-MM-dd")).Distinct().ToList()));
        }

        // Obras (solo si hay y al menos una con descripción)
        var works = await _events.GetWorksNearbyAsync(lat, lon, 300);
        var obrasPartes = works.Select(w => string.IsNullOrEmpty(w.Description) ? "" : w.StartDate.HasValue ? w.Description + " (" + w.StartDate.Value.ToString("dd/MM", inv) + ")" : w.Description).Where(s => !string.IsNullOrEmpty(s)).ToList();
        if (obrasPartes.Count > 0)
            alertas.Add(new AlertaItem("obras", "Obras cerca", string.Join(". ", obrasPartes) + ".", ordenBajo, null, "Obras cercanas pueden afectar acceso e imagen de la zona."));

        // Predicción basada en N semanas
        var weeksUsed = await _predictionService.GetWeeksUsedForPredictionAsync();
        if (weeksUsed > 0)
        {
            var textoN = weeksUsed < 4 ? "Predicción basada en " + weeksUsed + " semanas con datos. Revisa expectativas si la historia es corta." : "Predicción basada en " + weeksUsed + " semanas con datos.";
            alertas.Add(new AlertaItem("nSemanas", "Base de la predicción", textoN, ordenMeta, null, "Número de semanas completas (≥5 días) usadas para calcular la estimación."));
        }

        // Patrones aplicados (resumen si existen)
        var patternRain = await _patterns.GetPatternAsync("Impacto clima lluvioso");
        var patternHoliday = await _patterns.GetPatternAsync("Impacto festivos");
        var patternTemp = await _patterns.GetPatternAsync("Impacto temperatura");
        var patternParts = new List<string>();
        if (patternRain != null && patternRain.Confidence > 0) patternParts.Add("lluvia " + patternRain.Confidence.ToString("F0", inv) + "% conf.");
        if (patternHoliday != null && patternHoliday.Confidence > 0) patternParts.Add("festivos " + patternHoliday.Confidence.ToString("F0", inv) + "% conf.");
        if (patternTemp != null && patternTemp.Confidence > 0) patternParts.Add("temperatura " + patternTemp.Confidence.ToString("F0", inv) + "% conf.");
        if (patternParts.Count > 0)
            alertas.Add(new AlertaItem("patrones", "Patrones aplicados", "Según patrones aprendidos: " + string.Join(", ", patternParts) + ". Se aplican a la predicción diaria.", ordenMeta, null, "Patrones de impacto (lluvia, festivos, temperatura) usados para ajustar la estimación."));

        // Orden: si hay pct estimado, priorizar por |pct| (mayor impacto), luego por OrdenImpacto.
        alertas = alertas
            .OrderByDescending(a => a.Pct.HasValue ? Math.Abs(a.Pct.Value) : -1m)
            .ThenBy(a => a.OrdenImpacto)
            .ThenBy(a => a.Tipo)
            .ToList();
        return Ok(new
        {
            alertas = alertas.Select(a => new
            {
                tipo = a.Tipo,
                titulo = a.Titulo,
                texto = a.Texto,
                ordenImpacto = a.OrdenImpacto,
                pct = a.Pct,
                ayuda = a.Ayuda,
                dates = a.Dates,
                esAviso = a.Tipo == "nSemanas" && a.Texto != null && a.Texto.Contains("Revisa expectativas")
            })
        });
    }

    private sealed class AlertaItem
    {
        public string Tipo { get; }
        public string Titulo { get; }
        public string Texto { get; }
        public int OrdenImpacto { get; }
        public decimal? Pct { get; }
        public string Ayuda { get; }
        public List<string>? Dates { get; }
        public AlertaItem(string tipo, string titulo, string texto, int ordenImpacto, decimal? pct, string ayuda)
            : this(tipo, titulo, texto, ordenImpacto, pct, ayuda, null)
        {
        }
        public AlertaItem(string tipo, string titulo, string texto, int ordenImpacto, decimal? pct, string ayuda, List<string>? dates)
        {
            Tipo = tipo;
            Titulo = titulo;
            Texto = texto;
            OrdenImpacto = ordenImpacto;
            Pct = pct;
            Ayuda = ayuda;
            Dates = dates;
        }
    }

    private static string DayNameSpanish(DateTime date, CultureInfo es)
    {
        var s = date.ToString("dddd", es);
        return string.IsNullOrEmpty(s) ? "" : char.ToUpperInvariant(s[0]) + s[1..];
    }

    [HttpGet("comparativas")]
    public async Task<IActionResult> GetComparativas([FromQuery] string weekStart, [FromQuery] string mode = "plan")
    {
        if (string.IsNullOrWhiteSpace(weekStart) || !DateTime.TryParse(weekStart, out var d))
            return BadRequest(new { message = "weekStart inválido (yyyy-MM-dd)." });

        var monday = GetMonday(d.Date);
        var end = monday.AddDays(6);
        var inv = CultureInfo.InvariantCulture;

        decimal? factObj = null;
        var factObjS = await _db.Settings.AsNoTracking().FirstOrDefaultAsync(s => s.Key == "FacturacionObjetivoSemanal");
        if (factObjS != null && decimal.TryParse((factObjS.Value ?? "").Replace(",", "."), NumberStyles.Any, inv, out var fo) && fo > 0) factObj = fo;

        decimal? prodObj = null;
        var prodObjS = await _db.Settings.AsNoTracking().FirstOrDefaultAsync(s => s.Key == "ProductividadIdealEurHora");
        if (prodObjS != null && decimal.TryParse((prodObjS.Value ?? "").Replace(",", "."), NumberStyles.Any, inv, out var po) && po > 0) prodObj = po;

        var weekDays = await _db.ExecutionDays
            .AsNoTracking()
            .Where(e => !e.IsFeedbackOnly && e.Date >= monday && e.Date <= end)
            .Select(e => new { e.Date, e.TotalRevenue, e.TotalHoursWorked })
            .ToListAsync();
        var actualRevenue = weekDays.Sum(x => x.TotalRevenue);
        var actualHours = weekDays.Sum(x => x.TotalHoursWorked);
        decimal? actualProd = actualHours > 0 ? actualRevenue / actualHours : null;

        var prevMonday = monday.AddDays(-7);
        var prevEnd = prevMonday.AddDays(6);
        var prevDays = await _db.ExecutionDays
            .AsNoTracking()
            .Where(e => !e.IsFeedbackOnly && e.Date >= prevMonday && e.Date <= prevEnd)
            .Select(e => new { e.TotalRevenue, e.TotalHoursWorked })
            .ToListAsync();
        var prevRevenue = prevDays.Sum(x => x.TotalRevenue);
        var prevHours = prevDays.Sum(x => x.TotalHoursWorked);
        decimal? prevProd = prevHours > 0 ? prevRevenue / prevHours : null;

        var sameStart = monday.AddMonths(-1);
        var sameEnd = end.AddMonths(-1);
        var sameDays = await _db.ExecutionDays
            .AsNoTracking()
            .Where(e => !e.IsFeedbackOnly && e.Date >= sameStart && e.Date <= sameEnd)
            .Select(e => e.TotalRevenue)
            .ToListAsync();
        decimal? prevMonthRevenue = sameDays.Count > 0 ? sameDays.Sum() : null;

        decimal? predictedRevenue = null;
        if (string.Equals(mode, "plan", StringComparison.OrdinalIgnoreCase))
        {
            var pred = await _db.WeeklyPredictions.AsNoTracking().FirstOrDefaultAsync(p => p.WeekStartMonday == monday);
            if (pred != null && pred.PredictedRevenue.HasValue && pred.PredictedRevenue > 0)
                predictedRevenue = pred.PredictedRevenue.Value;
            else
            {
                // Fallback: predicción en vivo (sin necesidad de haberla guardado).
                var (weekStartLive, total, _) = await _predictionService.ComputeLivePredictionAsync();
                if (weekStartLive == monday && total > 0) predictedRevenue = total;
            }
        }

        var baseRevenue = string.Equals(mode, "plan", StringComparison.OrdinalIgnoreCase)
            ? predictedRevenue
            : (actualRevenue > 0 ? actualRevenue : (decimal?)null);
        var baseProd = string.Equals(mode, "plan", StringComparison.OrdinalIgnoreCase) ? (decimal?)null : actualProd;

        decimal? vsPrevPct = null;
        if (baseRevenue.HasValue && prevRevenue > 0) vsPrevPct = (baseRevenue.Value - prevRevenue) / prevRevenue * 100;
        decimal? vsPrevMonthPct = null;
        if (baseRevenue.HasValue && prevMonthRevenue.HasValue && prevMonthRevenue.Value > 0) vsPrevMonthPct = (baseRevenue.Value - prevMonthRevenue.Value) / prevMonthRevenue.Value * 100;
        decimal? vsFactObjPct = null;
        if (baseRevenue.HasValue && factObj.HasValue && factObj.Value > 0) vsFactObjPct = (baseRevenue.Value - factObj.Value) / factObj.Value * 100;
        decimal? vsProdObjPct = null;
        if (baseProd.HasValue && prodObj.HasValue && prodObj.Value > 0) vsProdObjPct = (baseProd.Value - prodObj.Value) / prodObj.Value * 100;

        return Ok(new
        {
            mode,
            weekStartMonday = monday.ToString("yyyy-MM-dd"),
            weekEndSunday = end.ToString("yyyy-MM-dd"),
            baseRevenue,
            baseProductivity = baseProd,
            actual = new { revenue = actualRevenue > 0 ? actualRevenue : (decimal?)null, hours = actualHours > 0 ? actualHours : (decimal?)null, productivity = actualProd },
            previousWeek = new { revenue = prevRevenue > 0 ? prevRevenue : (decimal?)null, hours = prevHours > 0 ? prevHours : (decimal?)null, productivity = prevProd, pctRevenue = vsPrevPct },
            previousMonthSameDates = new { revenue = prevMonthRevenue, pctRevenue = vsPrevMonthPct, from = sameStart.ToString("yyyy-MM-dd"), to = sameEnd.ToString("yyyy-MM-dd") },
            objectives = new
            {
                facturacionObjetivoSemanal = factObj,
                productividadObjetivo = prodObj,
                pctRevenue = vsFactObjPct,
                pctProductivity = vsProdObjPct
            }
        });
    }

    [HttpGet("data-quality")]
    public async Task<IActionResult> GetDataQuality([FromQuery] string mode = "plan", [FromQuery] string? weekStart = null)
    {
        var today = DateTime.UtcNow.Date;
        DateTime monday;
        if (!string.IsNullOrWhiteSpace(weekStart) && DateTime.TryParse(weekStart, out var d))
            monday = GetMonday(d.Date);
        else
            monday = string.Equals(mode, "plan", StringComparison.OrdinalIgnoreCase) ? NextWeekPredictionService.GetNextMonday(today) : GetMonday(today);
        var end = monday.AddDays(6);

        var weeksUsed = await _predictionService.GetWeeksUsedForPredictionAsync();

        var days = await _db.ExecutionDays
            .AsNoTracking()
            .Where(e => !e.IsFeedbackOnly && e.Date >= monday && e.Date <= end)
            .Select(e => new { e.Date, e.TotalRevenue, e.TotalHoursWorked, e.WeatherCode, e.WeatherPrecipMm, e.WeatherWindMaxKmh, e.WeatherTempMax, e.WeatherTempMin })
            .ToListAsync();
        var validDays = days.Count(x => x.TotalRevenue > 0 && x.TotalHoursWorked > 0);
        var weatherDaysWithAny = days.Count(x => x.WeatherCode.HasValue || x.WeatherPrecipMm.HasValue || x.WeatherWindMaxKmh.HasValue || x.WeatherTempMax.HasValue || x.WeatherTempMin.HasValue);

        var rangeEnd = DateTime.UtcNow.Date;
        var rangeStart = rangeEnd.AddDays(-84);
        var shifts = await _db.ShiftFeedbacks
            .AsNoTracking()
            .Where(s => s.ExecutionDay != null && !s.ExecutionDay.IsFeedbackOnly && s.ExecutionDay.Date >= rangeStart && s.ExecutionDay.Date <= rangeEnd && s.Revenue > 0)
            .Select(s => new { s.ExecutionDay.Date, s.ShiftName, s.Revenue })
            .ToListAsync();
        var shiftDays = shifts.GroupBy(x => x.Date.Date).Count();

        return Ok(new
        {
            mode,
            weekStartMonday = monday.ToString("yyyy-MM-dd"),
            weekEndSunday = end.ToString("yyyy-MM-dd"),
            weeksUsedForPrediction = weeksUsed,
            weekDaysCount = days.Count,
            weekValidDays = validDays,
            weekWeatherCoverageDays = weatherDaysWithAny,
            shiftHistoryDays = shiftDays
        });
    }

    /// <summary>Evalúa la predicción de la semana pasada: compara con realidad y actualiza bias/MAE por día de la semana.</summary>
    [HttpPost("evaluate-predictions")]
    public async Task<IActionResult> EvaluatePredictions([FromServices] IEvaluatePredictionsService evaluate)
    {
        await evaluate.EvaluateLastWeekIfPendingAsync();
        return Ok(new { ok = true });
    }

    /// <summary>Ejecuta evaluación de predicción de la semana pasada (bias/MAE) y análisis de patrones (lluvia, festivos, temperatura, estacional por DOW).</summary>
    [HttpPost("compute-patterns")]
    public async Task<IActionResult> ComputePatterns([FromServices] IEvaluatePredictionsService evaluate)
    {
        await evaluate.EvaluateLastWeekIfPendingAsync();
        await _patterns.ComputeAndSavePatternsAsync();
        return Ok(new { ok = true });
    }

    /// <summary>Construye payload de estimaciones cuando no hay caché (tablet / cliente sin app Windows).</summary>
    private async Task<IActionResult> BuildEstimacionesPayloadAsync(string? weekStart)
    {
        try
        {
            DateTime start;
            if (string.IsNullOrWhiteSpace(weekStart) || !DateTime.TryParse(weekStart, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out start))
                start = DateTime.UtcNow.Date;
            var monday = GetMonday(start);
            var baseUrl = $"{Request.Scheme}://{Request.Host}";
            using var http = new HttpClient();
            var auth = Request.Headers.Authorization.FirstOrDefault();
            if (!string.IsNullOrEmpty(auth)) http.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", auth);
            var dashUrl = $"{baseUrl}/api/dashboard/week?weekStart={monday:yyyy-MM-dd}";
            var dashResp = await http.GetAsync(dashUrl);
            if (!dashResp.IsSuccessStatusCode) return Ok(new { kpis = new object[] { }, days = new object[] { }, alertas = new object[] { }, resumen = "" });
            var dashJson = await dashResp.Content.ReadAsStringAsync();
            var predUrl = $"{baseUrl}/api/predictions/next-week";
            var predResp = await http.GetAsync(predUrl);
            var predJson = predResp.IsSuccessStatusCode ? await predResp.Content.ReadAsStringAsync() : "{}";
            var alertasUrl = $"{baseUrl}/api/estimaciones/alertas";
            var alertasResp = await http.GetAsync(alertasUrl);
            var alertasJson = alertasResp.IsSuccessStatusCode ? await alertasResp.Content.ReadAsStringAsync() : "{\"alertas\":[]}";
            var dash = default(JsonElement); var pred = default(JsonElement); var alertasObj = default(JsonElement);
            try { dash = JsonSerializer.Deserialize<JsonElement>(dashJson); } catch { }
            try { pred = JsonSerializer.Deserialize<JsonElement>(predJson); } catch { }
            try { alertasObj = JsonSerializer.Deserialize<JsonElement>(alertasJson); } catch { }
            var kpis = new List<object>();
            if (dash.ValueKind == JsonValueKind.Object)
            {
                try
                {
                    if (dash.TryGetProperty("avgRevenueHistoric", out var ar) && ar.ValueKind != JsonValueKind.Null) kpis.Add(new { label = "Facturación promedio semanal", value = ar.GetDecimal().ToString("F0") + " €" });
                    if (dash.TryGetProperty("avgProductivityHistoric", out var ap) && ap.ValueKind != JsonValueKind.Null) kpis.Add(new { label = "Productividad promedio (€/h)", value = ap.GetDecimal().ToString("F1") });
                    if (dash.TryGetProperty("avgHoursHistoric", out var ah) && ah.ValueKind != JsonValueKind.Null) kpis.Add(new { label = "Horas promedio semanal", value = ah.GetDecimal().ToString("F1") });
                    if (dash.TryGetProperty("costePersonalPctVsHistoric", out var cp) && dash.TryGetProperty("costePersonalEurFromContrato", out var ce))
                        kpis.Add(new { label = "Coste personal (vs histórico)", value = cp.GetDecimal().ToString("F1") + " % (" + ce.GetDecimal().ToString("F0") + " €)" });
                }
                catch { }
            }
            var days = new List<object>();
            if (pred.ValueKind == JsonValueKind.Object && pred.TryGetProperty("dailyPredictionsJson", out var dpj) && dpj.ValueKind == JsonValueKind.String)
            {
                try
                {
                    var daysArray = JsonSerializer.Deserialize<JsonElement>(dpj.GetString() ?? "[]");
                    if (daysArray.ValueKind == JsonValueKind.Array)
                        foreach (var d in daysArray.EnumerateArray())
                            days.Add(JsonSerializer.Deserialize<object>(d.GetRawText()) ?? new { });
                }
                catch { }
            }
            var alertas = new List<object>();
            if (alertasObj.ValueKind == JsonValueKind.Object && alertasObj.TryGetProperty("alertas", out var alArr) && alArr.ValueKind == JsonValueKind.Array)
            {
                try
                {
                    foreach (var a in alArr.EnumerateArray())
                        alertas.Add(JsonSerializer.Deserialize<object>(a.GetRawText()) ?? new { });
                }
                catch { }
            }
            var resumen = "";
            try { if (dash.ValueKind == JsonValueKind.Object && dash.TryGetProperty("resumenTexto", out var rt)) resumen = rt.GetString() ?? ""; } catch { }
            return Ok(new { kpis, days, alertas, resumen });
        }
        catch (Exception)
        {
            return Ok(new { kpis = new object[] { }, days = new object[] { }, alertas = new object[] { }, resumen = "" });
        }
    }

    private static DateTime GetMonday(DateTime d)
    {
        var diff = (7 + (d.DayOfWeek - DayOfWeek.Monday)) % 7;
        return d.AddDays(-diff).Date;
    }
}
