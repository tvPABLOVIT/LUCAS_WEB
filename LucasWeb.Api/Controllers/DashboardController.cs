using LucasWeb.Api.Data;
using LucasWeb.Api.DTOs;
using LucasWeb.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LucasWeb.Api.Controllers;

[ApiController]
[Route("api/dashboard")]
[Authorize]
public class DashboardController : ControllerBase
{
    private const int TrendWeeks = 6;
    private const int TrendMinSamples = 4;
    private static readonly decimal TrendThresholdStrong = 0.10m;
    private static readonly decimal TrendThresholdSlight = 0.05m;
    private static readonly decimal TrendDeadBand = 0.03m;

    private readonly AppDbContext _db;
    private readonly ILogger<DashboardController> _logger;

    public DashboardController(AppDbContext db, ILogger<DashboardController> logger)
    {
        _db = db;
        _logger = logger;
    }

    [HttpGet("daily-revenue")]
    public async Task<ActionResult<List<DailyRevenueItemDto>>> GetDailyRevenue([FromQuery] int days = 30)
    {
        try
        {
            if (days < 1 || days > 365) days = 30;
            var end = DateTime.UtcNow.Date.AddDays(1);
            var start = end.AddDays(-days);
            var list = await _db.ExecutionDays
                .AsNoTracking()
                .Where(e => !e.IsFeedbackOnly && e.Date >= start && e.Date < end)
                .OrderBy(e => e.Date)
                .Select(e => new { e.Date, e.TotalRevenue })
                .ToListAsync();
            var items = list.Select(e => new DailyRevenueItemDto
            {
                Date = e.Date.ToString("yyyy-MM-dd"),
                Revenue = e.TotalRevenue
            }).ToList();
            return Ok(items);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error en GET dashboard/daily-revenue");
            return StatusCode(500, new { message = "Error al cargar la facturación diaria." });
        }
    }

    [HttpGet("week")]
    public async Task<ActionResult<DashboardWeekResponse>> GetWeek([FromQuery] string? weekStart, [FromQuery] string? asOf)
    {
        DateTime start;
        if (string.IsNullOrWhiteSpace(weekStart) || !DateTime.TryParse(weekStart, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out start))
            start = GetMonday(DateTime.UtcNow.Date);
        else
            start = GetMonday(start.Date);

        DateTime effectiveAsOf;
        if (string.IsNullOrEmpty(asOf) || !DateTime.TryParse(asOf, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out effectiveAsOf))
            effectiveAsOf = DateTime.UtcNow.Date;
        else
            effectiveAsOf = effectiveAsOf.Date;

        try
        {
        var end = start.AddDays(7);
        var days = await _db.ExecutionDays
            .Include(e => e.ShiftFeedbacks)
            .Where(e => !e.IsFeedbackOnly && e.Date >= start && e.Date < end)
            .OrderBy(e => e.Date)
            .ToListAsync();

        int numDaysToCompare;
        if (effectiveAsOf < start)
            numDaysToCompare = 0;
        else if (effectiveAsOf >= end)
            numDaysToCompare = 7;
        else
            numDaysToCompare = (int)(effectiveAsOf - start).TotalDays + 1;

        var isCurrentWeek = effectiveAsOf >= start && effectiveAsOf < end;

        var daysInRange = numDaysToCompare > 0 ? days.Where(d => d.Date <= effectiveAsOf).ToList() : new List<Models.ExecutionDay>();
        var totalRevenue = daysInRange.Sum(d => d.TotalRevenue);
        var totalRevenueForComparisons = isCurrentWeek && daysInRange.Count > 0
            ? daysInRange.Sum(d => d.RevenueFromExcel == true ? d.TotalRevenue : d.TotalRevenue * 0.909m)
            : totalRevenue;
        var totalHours = daysInRange.Sum(d => d.TotalHoursWorked);
        var avgStaff = daysInRange.Count > 0 ? (decimal)daysInRange.Average(d => d.StaffTotal) : 0;
        decimal? avgProductivity = null;
        if (totalHours > 0)
            avgProductivity = totalRevenue / totalHours;

        var prevStart = start.AddDays(-7);
        var prevEnd = numDaysToCompare > 0 ? prevStart.AddDays(numDaysToCompare) : prevStart;
        var prevDays = await _db.ExecutionDays
            .Where(e => !e.IsFeedbackOnly && e.Date >= prevStart && e.Date < prevEnd)
            .ToListAsync();
        var prevWeekRevenue = prevDays.Count > 0 ? (decimal?)prevDays.Sum(d => d.TotalRevenue) : null;
        decimal? prevWeekProductivity = null;
        var prevHours = prevDays.Sum(d => d.TotalHoursWorked);
        if (prevHours > 0 && prevWeekRevenue.HasValue)
            prevWeekProductivity = prevWeekRevenue.Value / prevHours;

        decimal? avgRevenueHistoric = null;
        decimal? avgProductivityHistoric = null;
        decimal? avgHoursHistoric = null;
        var historicCutoff = start.AddDays(-(TrendWeeks * 7)); // últimas 6 semanas
        var allHistoricDays = await _db.ExecutionDays
            .AsNoTracking()
            .Where(e => !e.IsFeedbackOnly && e.Date >= historicCutoff && e.Date < start)
            .Select(e => new { e.Date, e.TotalRevenue, e.TotalHoursWorked })
            .ToListAsync();
        var weeklyStats = allHistoricDays
            .GroupBy(e => GetMonday(e.Date))
            .Where(g => g.Count() >= 5)
            .Select(g => new { TotalRevenue = g.Sum(x => x.TotalRevenue), TotalHours = g.Sum(x => x.TotalHoursWorked) })
            .ToList();
        if (weeklyStats.Count > 0)
        {
            avgRevenueHistoric = weeklyStats.Average(w => w.TotalRevenue);
            avgHoursHistoric = weeklyStats.Average(w => w.TotalHours);
            var productivities = weeklyStats.Where(w => w.TotalHours > 0).Select(w => w.TotalRevenue / w.TotalHours).ToList();
            if (productivities.Count > 0)
                avgProductivityHistoric = productivities.Average();
        }

        var avgByDayOfWeek = allHistoricDays
            .GroupBy(e => e.Date.DayOfWeek)
            .ToDictionary(g => g.Key, g => g.Average(x => x.TotalRevenue));

        // Tendencia por día de la semana: mitad reciente vs mitad antigua en las últimas 6 semanas. 5 niveles con banda muerta.
        var trendByDayOfWeek = new Dictionary<DayOfWeek, string?>();
        foreach (var dow in new[] { DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday, DayOfWeek.Saturday, DayOfWeek.Sunday })
        {
            var list = allHistoricDays.Where(e => e.Date.DayOfWeek == dow).OrderBy(e => e.Date).ToList();
            string? label = null;
            if (list.Count >= TrendMinSamples)
            {
                var half = list.Count / 2;
                var firstHalf = list.Take(half).Select(e => e.TotalRevenue).ToList();
                var secondHalf = list.Skip(half).Select(e => e.TotalRevenue).ToList();
                var avgFirst = firstHalf.Average();
                var avgSecond = secondHalf.Average();
                if (avgFirst > 0)
                {
                    var pct = (avgSecond - avgFirst) / avgFirst;
                    var pctPct = (int)Math.Round(pct * 100);
                    var pctStr = pctPct >= 0 ? "+" + pctPct + "%" : pctPct + "%";
                    if (Math.Abs(pct) < TrendDeadBand)
                        label = "→ Estable (" + pctStr + ")";
                    else if (pct >= TrendThresholdStrong)
                        label = "↑ Al alza (" + pctStr + ")";
                    else if (pct >= TrendThresholdSlight)
                        label = "↗ Levemente al alza (" + pctStr + ")";
                    else if (pct > -TrendThresholdSlight)
                        label = "→ Estable (" + pctStr + ")";
                    else if (pct > -TrendThresholdStrong)
                        label = "↘ Levemente a la baja (" + pctStr + ")";
                    else
                        label = "↓ A la baja (" + pctStr + ")";
                }
            }
            trendByDayOfWeek[dow] = label;
        }

        var prevWeekStart = start.AddDays(-7);
        var prevWeekDays = await _db.ExecutionDays
            .AsNoTracking()
            .Where(e => !e.IsFeedbackOnly && e.Date >= prevWeekStart && e.Date < start)
            .Select(e => new { e.Date, e.TotalRevenue })
            .ToListAsync();
        var byPrevWeekDate = prevWeekDays.ToDictionary(e => e.Date.ToString("yyyy-MM-dd"), e => e.TotalRevenue);

        var dayNames = new Dictionary<DayOfWeek, string>
        {
            { DayOfWeek.Monday, "Lunes" }, { DayOfWeek.Tuesday, "Martes" }, { DayOfWeek.Wednesday, "Miércoles" },
            { DayOfWeek.Thursday, "Jueves" }, { DayOfWeek.Friday, "Viernes" }, { DayOfWeek.Saturday, "Sábado" },
            { DayOfWeek.Sunday, "Domingo" }
        };

        var shiftOrder = new[] { "Mediodia", "Tarde", "Noche" };
        decimal horasPorTurno = 4;
        var horasPorTurnoStr = await GetSettingValueAsync("HorasPorTurno");
        if (!string.IsNullOrWhiteSpace(horasPorTurnoStr) && decimal.TryParse(horasPorTurnoStr.Replace(",", "."), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var hpt) && hpt >= 1 && hpt <= 24)
            horasPorTurno = hpt;

        var dayItems = days.Select(d =>
        {
            decimal? dayAvg = avgByDayOfWeek.TryGetValue(d.Date.DayOfWeek, out var avg) ? avg : null;
            var trendLabel = trendByDayOfWeek.TryGetValue(d.Date.DayOfWeek, out var tl) ? tl : null;
            string? trendVsPrevWeek = null;
            var prevWeekDateStr = d.Date.AddDays(-7).ToString("yyyy-MM-dd");
            if (byPrevWeekDate.TryGetValue(prevWeekDateStr, out var prevRev) && prevRev > 0)
            {
                var prevPct = (d.TotalRevenue - prevRev) / prevRev;
                var prevPctPct = (int)Math.Round(prevPct * 100);
                trendVsPrevWeek = prevPctPct >= 0 ? "vs sem. ant.: +" + prevPctPct + " %" : "vs sem. ant.: " + prevPctPct + " %";
            }
            string? staffSala = null;
            string? staffCocina = null;
            decimal? calculatedStaffHours = null;
            decimal? plannedHoursFromPdf = null;
            string? plannedHoursBreakdown = null;
            var orderedShifts = shiftOrder.Select(n => d.ShiftFeedbacks?.FirstOrDefault(s => string.Equals(s.ShiftName, n, StringComparison.OrdinalIgnoreCase))).Where(s => s != null).ToList();
            if (orderedShifts.Count > 0)
            {
                staffSala = string.Join("-", orderedShifts.Select(s => s!.StaffFloor));
                staffCocina = string.Join("-", orderedShifts.Select(s => s!.StaffKitchen));
                calculatedStaffHours = orderedShifts.Sum(s => (s!.StaffFloor + s.StaffKitchen) * horasPorTurno);
                var plannedSum = orderedShifts.Sum(s => s!.PlannedHours ?? 0);
                if (plannedSum > 0)
                {
                    plannedHoursFromPdf = plannedSum;
                    var parts = new List<string>();
                    var shiftLabels = new[] { "Mediodía", "Tarde", "Noche" };
                    for (var i = 0; i < orderedShifts.Count && i < shiftLabels.Length; i++)
                    {
                        var ph = orderedShifts[i]!.PlannedHours ?? 0;
                        if (ph > 0)
                            parts.Add(shiftLabels[i] + ": " + ph.ToString("F1", System.Globalization.CultureInfo.InvariantCulture) + " h");
                    }
                    if (parts.Count > 0)
                        plannedHoursBreakdown = string.Join(", ", parts);
                }
            }
            // Horas: reales (Excel) > total del PDF por día (PlannedHoursTotal = suma columna Total del cuadrante) > suma por turno > calculadas
            var plannedDayTotal = d.PlannedHoursTotal ?? 0;
            var plannedHoursFromShifts = d.ShiftFeedbacks?.Sum(s => s.PlannedHours ?? 0) ?? 0;
            var effectiveHours = d.TotalHoursWorked > 0 ? (decimal?)d.TotalHoursWorked
                : (plannedDayTotal > 0 ? (decimal?)plannedDayTotal
                : (plannedHoursFromShifts > 0 ? (decimal?)plannedHoursFromShifts : calculatedStaffHours));
            var effectiveProductivity = (effectiveHours.HasValue && effectiveHours > 0 && d.TotalRevenue > 0) ? d.TotalRevenue / effectiveHours.Value : (decimal?)null;
            int? pctVsAvg = null;
            if (dayAvg.HasValue && dayAvg.Value > 0)
                pctVsAvg = (int)Math.Round((d.TotalRevenue - dayAvg.Value) / dayAvg.Value * 100);

            var dayFeedbackSummary = !string.IsNullOrWhiteSpace(d.Notes)
                ? d.Notes.Trim()
                : FeedbackObservationsHelper.BuildDayFeedbackSummary(d.ShiftFeedbacks ?? Enumerable.Empty<Models.ShiftFeedback>());
            if (string.IsNullOrWhiteSpace(dayFeedbackSummary))
                dayFeedbackSummary = null;

            var (daySgt, dayEstado, dayResumenDiario) = DayScoringHelper.ComputeDayResumen(d.ShiftFeedbacks);
            var dayConclusion = DayScoringHelper.BuildDayConclusion(d.ShiftFeedbacks);

            return new DashboardDayItemDto
            {
                Date = d.Date.ToString("yyyy-MM-dd"),
                DayName = dayNames.TryGetValue(d.Date.DayOfWeek, out var name) ? name : "",
                Revenue = d.TotalRevenue,
                RevenueFromManual = d.RevenueFromExcel != true,
                HoursWorked = d.TotalHoursWorked,
                Productivity = d.TotalHoursWorked > 0 ? d.TotalRevenue / d.TotalHoursWorked : 0,
                StaffTotal = d.StaffTotal,
                StaffSummarySala = staffSala,
                StaffSummaryCocina = staffCocina,
                CalculatedStaffHours = calculatedStaffHours,
                PlannedHoursFromPdf = plannedHoursFromPdf,
                PlannedHoursBreakdown = plannedHoursBreakdown,
                EffectiveHours = effectiveHours,
                EffectiveProductivity = effectiveProductivity,
                AvgRevenueHistoric = dayAvg,
                PctVsAvgHistoric = pctVsAvg,
                TrendLabel = trendLabel,
                TrendVsPrevWeek = trendVsPrevWeek,
                WeatherCode = d.WeatherCode,
                WeatherTempMax = d.WeatherTempMax,
                WeatherTempMin = d.WeatherTempMin,
                WeatherPrecipMm = d.WeatherPrecipMm,
                WeatherWindMaxKmh = d.WeatherWindMaxKmh,
                DayFeedbackSummary = dayFeedbackSummary,
                DaySgt = daySgt,
                DayEstado = dayEstado,
                DayResumenDiario = dayResumenDiario,
                DayConclusion = dayConclusion
            };
        }).ToList();

        var totalEffectiveHours = dayItems.Sum(x => x.EffectiveHours ?? 0);
        var avgProductivityFromEffective = (totalEffectiveHours > 0 && totalRevenue > 0) ? (decimal?)(totalRevenue / totalEffectiveHours) : null;
        var productivityForWeek = avgProductivityFromEffective ?? avgProductivity;

        string? resumenTexto = null;
        string? resumenClasificacion = null;
        if (days.Count > 0)
        {
            if (productivityForWeek.HasValue && productivityForWeek > 80)
                resumenClasificacion = isCurrentWeek ? "🟢 Semana buena (provisional)" : "🟢 Semana buena";
            else if (productivityForWeek.HasValue && productivityForWeek > 50)
                resumenClasificacion = isCurrentWeek ? "🟡 Semana normal (provisional)" : "🟡 Semana normal";
            else
                resumenClasificacion = isCurrentWeek ? "🔴 Semana baja (provisional)" : "🔴 Semana baja";

            var parts = new List<string>();
            if (isCurrentWeek && numDaysToCompare > 0)
                parts.Add($"Hasta hoy ({numDaysToCompare} de 7 días) la facturación ha sido de {totalRevenue:N0} €, con {totalEffectiveHours:N1} horas trabajadas.");
            else
                parts.Add($"En esa semana la facturación total fue de {totalRevenue:N0} €, con {totalEffectiveHours:N1} horas trabajadas.");
            if (productivityForWeek.HasValue)
            {
                if (productivityForWeek > 80)
                    parts.Add($"La productividad media es de {productivityForWeek.Value:N1} €/h, un nivel alto: cada hora trabajada está generando bien en caja.");
                else if (productivityForWeek > 50)
                    parts.Add($"La productividad media se sitúa en {productivityForWeek.Value:N1} €/h, dentro de lo normal.");
                else
                    parts.Add($"La productividad media es de {productivityForWeek.Value:N1} €/h, por debajo de lo habitual; conviene revisar horarios o afluencia.");
            }
            if (prevWeekRevenue.HasValue && prevWeekRevenue.Value > 0)
            {
                var pctVsPrev = (totalRevenueForComparisons - prevWeekRevenue.Value) / prevWeekRevenue.Value;
                var pctPct = (int)Math.Round(pctVsPrev * 100);
                if (pctPct > 5)
                    parts.Add($"Respecto a la semana pasada, la facturación ha subido un {pctPct}%.");
                else if (pctPct < -5)
                    parts.Add($"Respecto a la semana pasada, la facturación ha bajado un {Math.Abs(pctPct)}%.");
                else
                    parts.Add("Respecto a la semana pasada, la facturación se mantiene en niveles similares.");
            }
            if (avgRevenueHistoric.HasValue && avgRevenueHistoric.Value > 0)
            {
                var diffVsMedia = totalRevenueForComparisons - avgRevenueHistoric.Value;
                var pctVsMedia = (int)Math.Round((diffVsMedia / avgRevenueHistoric.Value) * 100);
                if (pctVsMedia > 10)
                    parts.Add($"La media de las últimas doce semanas ronda los {avgRevenueHistoric.Value:N0} €, así que esta semana vas claramente por encima.");
                else if (pctVsMedia < -10)
                    parts.Add($"La media de las últimas doce semanas ronda los {avgRevenueHistoric.Value:N0} €, así que esta semana queda por debajo.");
                else
                    parts.Add($"La media de las últimas doce semanas ronda los {avgRevenueHistoric.Value:N0} €; esta semana va en línea con ese nivel.");
            }
            if (isCurrentWeek && numDaysToCompare > 0)
                parts.Add("(Datos parciales: la semana aún está en curso.)");
            resumenTexto = string.Join(" ", parts);
        }
        else if (isCurrentWeek)
        {
            resumenTexto = "Aún no hay datos esta semana. Los totales y la clasificación aparecerán cuando añadas días desde Registro de ejecución o importes Excel.";
        }
        else if (!isCurrentWeek && days.Count == 0)
        {
            resumenTexto = "Esta semana pasada no tiene días registrados. Puedes añadirlos desde Registro de ejecución (las fechas quedarán en el pasado).";
        }

        decimal? costePersonalEur = null;
        decimal? costePersonalPctFacturacion = null;
        await EnsureSettingsTableAsync();
        var costeHoraSetting = await GetSettingValueAsync("CostePersonalPorHora");
        if (string.IsNullOrWhiteSpace(costeHoraSetting))
            costeHoraSetting = await GetSettingValueAsync("CostoPersonalPorHora");
        decimal? costePersonalEurFromContrato = null;
        decimal? costePersonalPctVsHistoric = null;
        if (!string.IsNullOrWhiteSpace(costeHoraSetting) && decimal.TryParse(costeHoraSetting.Replace(",", "."), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var costePorHora) && costePorHora > 0)
        {
            costePersonalEur = totalEffectiveHours * costePorHora;
            costePersonalPctFacturacion = totalRevenue > 0 ? (costePersonalEur.Value / totalRevenue) * 100 : null;
            var horasContratoSetting = await GetSettingValueAsync("HorasSemanalesContrato");
            if (!string.IsNullOrWhiteSpace(horasContratoSetting) && decimal.TryParse(horasContratoSetting.Replace(",", "."), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var horasContrato) && horasContrato >= 0 && avgRevenueHistoric.HasValue && avgRevenueHistoric.Value > 0)
            {
                costePersonalEurFromContrato = horasContrato * costePorHora;
                costePersonalPctVsHistoric = (costePersonalEurFromContrato.Value / avgRevenueHistoric.Value) * 100;
            }
        }

        decimal? facturacionObjetivo = null;
        var startNormalized = GetMonday(start);
        await EnsureFacturacionObjetivoTableAsync();
        // Preferir SIEMPRE el objetivo configurado en Settings (igual que productividad objetivo).
        // Si no existe/está vacío, usar histórico si lo hubiera.
        try
        {
            var objSetting = await GetFacturacionObjetivoSemanalAsync();
            if (!string.IsNullOrWhiteSpace(objSetting) &&
                decimal.TryParse(objSetting.Replace(",", "."), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var objEur) &&
                objEur > 0 && objEur < 1_000_000)
            {
                facturacionObjetivo = objEur;
            }
        }
        catch { }
        if (!facturacionObjetivo.HasValue)
        {
            var historico = await _db.FacturacionObjetivoSemanas.FindAsync(startNormalized);
            if (historico != null)
                facturacionObjetivo = historico.TargetRevenue;
        }

        decimal? productividadObjetivo = null;
        try
        {
            var prodObjSetting = await GetSettingValueAsync("ProductividadIdealEurHora");
            if (!string.IsNullOrWhiteSpace(prodObjSetting) &&
                decimal.TryParse(prodObjSetting.Replace(",", "."), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var prodObj) &&
                prodObj > 0 && prodObj < 10000)
            {
                productividadObjetivo = prodObj;
            }
        }
        catch { }

        var last30Days = await GetLast30DaysRevenueAsync();
        var todayForWindow = DateTime.UtcNow.Date;
        var windowStart = todayForWindow.AddDays(-29);
        var weekInWindow = start >= windowStart && start <= todayForWindow;
        if (last30Days.Count == 0 && days.Count > 0 && weekInWindow)
            last30Days = days.Select(d => new DailyRevenueItemDto { Date = d.Date.ToString("yyyy-MM-dd"), Revenue = d.TotalRevenue }).ToList();

        return Ok(new DashboardWeekResponse
        {
            TotalRevenue = totalRevenue,
            AvgProductivity = productivityForWeek,
            TotalHours = totalEffectiveHours,
            AvgStaff = avgStaff,
            AvgRevenueHistoric = avgRevenueHistoric,
            AvgProductivityHistoric = avgProductivityHistoric,
            AvgHoursHistoric = avgHoursHistoric,
            CostePersonalEurFromContrato = costePersonalEurFromContrato,
            CostePersonalPctVsHistoric = costePersonalPctVsHistoric,
            PrevWeekRevenue = prevWeekRevenue,
            PrevWeekProductivity = prevWeekProductivity,
            ResumenClasificacion = resumenClasificacion,
            ResumenTexto = resumenTexto,
            CostePersonalEur = costePersonalEur,
            CostePersonalPctFacturacion = costePersonalPctFacturacion,
            FacturacionObjetivo = facturacionObjetivo,
            ProductividadObjetivo = productividadObjetivo,
            Days = dayItems,
            Last30Days = last30Days,
            IsCurrentWeek = isCurrentWeek,
            DaysIncludedCount = numDaysToCompare
        });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error en GET dashboard/week");
            return StatusCode(500, new { message = "Error al cargar el resumen semanal." });
        }
    }

    private static DateTime GetMonday(DateTime d)
    {
        var diff = (7 + (d.DayOfWeek - DayOfWeek.Monday)) % 7;
        return d.AddDays(-diff).Date;
    }

    private async Task<List<DailyRevenueItemDto>> GetLast30DaysRevenueAsync()
    {
        var list = new List<DailyRevenueItemDto>();
        try
        {
            var today = DateTime.UtcNow.Date;
            var start = today.AddDays(-29);
            var raw = await _db.ExecutionDays
                .AsNoTracking()
                .Where(e => !e.IsFeedbackOnly && e.Date >= start && e.Date <= today)
                .Select(e => new { e.Date, e.TotalRevenue })
                .ToListAsync();
            // Clave por string "yyyy-MM-dd" para evitar diferencias de DateTime.Kind (Utc vs Unspecified)
            var byDate = raw.ToDictionary(e => e.Date.ToString("yyyy-MM-dd"), e => e.TotalRevenue);
            for (var i = 0; i < 30; i++)
            {
                var d = start.AddDays(i);
                var dateStr = d.ToString("yyyy-MM-dd");
                var revenue = byDate.TryGetValue(dateStr, out var r) ? r : 0;
                list.Add(new DailyRevenueItemDto { Date = dateStr, Revenue = revenue });
            }
        }
        catch
        {
            // ignorar
        }
        return list;
    }

    private async Task EnsureSettingsTableAsync()
    {
        try
        {
            await _db.Database.ExecuteSqlRawAsync(
                "CREATE TABLE IF NOT EXISTS \"Settings\" (\"Key\" TEXT NOT NULL PRIMARY KEY, \"Value\" TEXT NOT NULL, \"UpdatedAt\" TEXT NOT NULL);");
        }
        catch
        {
            // Ignorar si ya existe o no es SQLite
        }
    }

    private async Task<string?> GetSettingValueAsync(string key)
    {
        try
        {
            var setting = await _db.Settings
                .AsNoTracking()
                .Where(s => s.Key == key)
                .Select(s => s.Value)
                .FirstOrDefaultAsync();
            return setting;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>Lee Facturación objetivo semanal desde Settings (EF + raw SQL por si la clave varía).</summary>
    private async Task<string?> GetFacturacionObjetivoSemanalAsync()
    {
        var fromEf = await GetSettingValueAsync("FacturacionObjetivoSemanal");
        if (!string.IsNullOrWhiteSpace(fromEf)) return fromEf;
        try
        {
            await EnsureSettingsTableAsync();
            var conn = _db.Database.GetDbConnection();
            if (conn.State != System.Data.ConnectionState.Open)
                await conn.OpenAsync();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT Value FROM Settings WHERE LOWER(TRIM(Key)) = 'facturacionobjetivosemanal' LIMIT 1";
            var raw = await cmd.ExecuteScalarAsync();
            return raw?.ToString();
        }
        catch
        {
            return null;
        }
    }

    private async Task EnsureFacturacionObjetivoTableAsync()
    {
        try
        {
            await _db.Database.ExecuteSqlRawAsync(
                "CREATE TABLE IF NOT EXISTS \"FacturacionObjetivoSemanas\" (\"WeekStart\" TEXT NOT NULL PRIMARY KEY, \"TargetRevenue\" REAL NOT NULL);");
        }
        catch { }
    }
}
