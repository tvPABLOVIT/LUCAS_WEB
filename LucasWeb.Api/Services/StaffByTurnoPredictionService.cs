using LucasWeb.Api.Data;
using LucasWeb.Api.DTOs;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.Text.Json;

namespace LucasWeb.Api.Services;

/// <summary>
/// Calcula el personal necesario (sala y cocina) por turno para cubrir la predicción de facturación.
/// La productividad ideal (50 €/h) es por todo el equipo completo (sala + cocina): 50 € por hora del conjunto.
/// total_people = Ceiling(revenue_turno / (50 * horas_turno)) es el tamaño del equipo (sala+cocina) en ese turno.
/// Se ejecuta después del enriquecimiento; usa ratio histórico (DOW+turno) y límites cómodos. Rellena staffSala y staffCocina por día.
/// </summary>
public class StaffByTurnoPredictionService
{
    private readonly AppDbContext _db;
    private const int MinSamplesPerDowShift = 2;
    private const int HistoricWeeks = 12;
    private static readonly string[] AllowedSchemas = { "1-1", "1-2", "2-1", "2-2", "2-3", "3-2", "3-3" };
    private static readonly decimal ComfortMargin = 1.05m;
    private static readonly decimal DefaultComfortLimit = 350m;
    /// <summary>Si la facturación del día es menor, no se recomienda más de 2 personas en sala ni en cocina por turno (equipo 2-2-2).</summary>
    private const decimal MinRevenueFor3PerShift = 3000m;

    /// <summary>Combinaciones permitidas para Sala (Mediodía-Tarde-Noche). Incluye 3-2-2 y el resto documentado.</summary>
    private static readonly (int M, int T, int N)[] AllowedSala = { (1, 1, 1), (1, 1, 2), (2, 1, 2), (2, 2, 2), (1, 2, 1), (2, 1, 1), (3, 1, 3), (1, 2, 3), (3, 1, 2), (3, 2, 2) };
    /// <summary>Combinaciones permitidas para Cocina (Mediodía-Tarde-Noche). Incluye 3-2-2.</summary>
    private static readonly (int M, int T, int N)[] AllowedCocina = { (1, 1, 1), (2, 1, 2), (1, 1, 2), (2, 2, 2), (3, 1, 3), (1, 2, 3), (3, 2, 2) };

    public StaffByTurnoPredictionService(AppDbContext db) => _db = db;

    private static int Dow(DateTime d) => d.DayOfWeek == DayOfWeek.Sunday ? 6 : (int)d.DayOfWeek - 1;

    private static string NormalizeShift(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return "";
        var x = s.Trim().ToLowerInvariant().Replace("í", "i").Replace("á", "a").Replace("é", "e").Replace("ó", "o");
        if (x.Contains("medio") || x == "midday" || x == "noon") return "mediodia";
        if (x.Contains("tarde")) return "tarde";
        if (x.Contains("noche") || x == "night") return "noche";
        return x;
    }

    /// <summary>Histórico por (DOW, turno): mediana de StaffFloor/StaffKitchen y media de Revenue. Ventana: últimas 12 semanas hasta hoy. Incluye turnos feedback-only (con staff) para mediana; AvgRevenue solo de turnos con Revenue&gt;0.</summary>
    private async Task<Dictionary<(int Dow, string Shift), (int Sala, int Cocina, decimal AvgRevenue)>> GetHistoricStaffByDowShiftAsync(DateTime currentWeekMonday, CancellationToken ct = default)
    {
        var end = DateTime.UtcNow.Date;
        var start = end.AddDays(-7 * HistoricWeeks).Date;

        var rows = await _db.ShiftFeedbacks
            .AsNoTracking()
            .Where(s => s.ExecutionDay != null
                && s.ExecutionDay.Date >= start && s.ExecutionDay.Date <= end
                && s.StaffFloor >= 0 && s.StaffKitchen >= 0
                && (s.Revenue > 0 || (s.ExecutionDay.IsFeedbackOnly && (s.StaffFloor > 0 || s.StaffKitchen > 0))))
            .Select(s => new
            {
                Date = s.ExecutionDay!.Date,
                ShiftName = s.ShiftName,
                s.StaffFloor,
                s.StaffKitchen,
                s.Revenue
            })
            .ToListAsync(ct);

        var byKey = rows
            .GroupBy(r => (Dow(r.Date), NormalizeShift(r.ShiftName)))
            .Where(g => g.Count() >= MinSamplesPerDowShift)
            .ToDictionary(g => g.Key, g =>
            {
                var salaList = g.Select(x => x.StaffFloor).OrderBy(x => x).ToList();
                var cocinaList = g.Select(x => x.StaffKitchen).OrderBy(x => x).ToList();
                int Median(IList<int> list)
                {
                    if (list.Count == 0) return 1;
                    var mid = list.Count / 2;
                    return list.Count % 2 == 1 ? list[mid] : (list[mid - 1] + list[mid]) / 2;
                }
                var sala = Math.Clamp(Median(salaList), 1, 3);
                var cocina = Math.Clamp(Median(cocinaList), 1, 3);
                var withRevenue = g.Where(x => x.Revenue > 0).ToList();
                var avgRevenue = withRevenue.Count > 0 ? withRevenue.Average(x => x.Revenue) : 0m;
                return (sala, cocina, avgRevenue);
            });

        return byKey;
    }

    /// <summary>Ratio de facturación para ajustar histórico: clamp [0.7, 1.6] (1.6 permite subir personal en tendencia al alza).</summary>
    private const decimal RevenueRatioMin = 0.7m;
    private const decimal RevenueRatioMax = 1.6m;

    private static decimal GetDec(object? o)
    {
        if (o == null) return 0m;
        if (o is decimal d) return d;
        if (o is int i) return i;
        if (o is long l) return l;
        if (o is double db) return (decimal)db;
        if (o is JsonElement je) return je.GetDecimal();
        if (decimal.TryParse(o.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var p)) return p;
        return 0m;
    }

    /// <summary>
    /// Umbral de facturación por persona: el paso 1→2 es la base (prod×horas); el paso 2→3 requiere un 50% más de facturación que 1→2.
    /// Devuelve el número total de personas (sala+cocina) para el turno según estos umbrales (mín 1, máx 6).
    /// </summary>
    private static int TotalPeopleFromRevenueTiered(decimal revenueTurno, decimal productividadEurHora, decimal horasPorTurno)
    {
        if (productividadEurHora <= 0 || horasPorTurno <= 0) return 1;
        var step1To2 = productividadEurHora * horasPorTurno; // facturación necesaria para subir de 1 a 2
        var step2To3 = step1To2 * 1.5m; // de 2 a 3 requiere al menos un 50% más que de 1 a 2
        // Umbrales acumulados: 1 persona &lt; step1To2; 2 &lt; step1To2+step2To3; 3 &lt; +step2To3; etc.
        var t2 = step1To2;
        var t3 = t2 + step2To3;
        var t4 = t3 + step2To3;
        var t5 = t4 + step2To3;
        var t6 = t5 + step2To3;
        if (revenueTurno < t2) return 1;
        if (revenueTurno < t3) return 2;
        if (revenueTurno < t4) return 3;
        if (revenueTurno < t5) return 4;
        if (revenueTurno < t6) return 5;
        return 6;
    }

    /// <summary>
    /// Mínimo de personal por turno (sala+cocina): umbrales escalonados (1→2 base; 2→3 un 50% más). Sala y cocina por ratio histórico o TotalToCocinaSalaByShift.
    /// </summary>
    private static (int Sala, int Cocina) MinStaffByProductivity(
        decimal revenueTurno,
        decimal productividadEurHora,
        decimal horasPorTurno,
        decimal revenueDiaTotal,
        int dow,
        string shiftName,
        IReadOnlyDictionary<(int Dow, string Shift), (int Sala, int Cocina, decimal AvgRevenue)>? historic)
    {
        if (productividadEurHora <= 0 || horasPorTurno <= 0)
            return (1, 1);
        var totalPeople = TotalPeopleFromRevenueTiered(revenueTurno, productividadEurHora, horasPorTurno);

        int salaMin;
        int cocinaMin;
        if (historic != null && historic.TryGetValue((dow, shiftName), out var hist) && (hist.Sala + hist.Cocina) > 0)
        {
            var ratioSala = hist.Sala / (decimal)(hist.Sala + hist.Cocina);
            salaMin = (int)Math.Ceiling(totalPeople * ratioSala);
            cocinaMin = totalPeople - salaMin;
            if (cocinaMin < 1) { cocinaMin = 1; salaMin = totalPeople - 1; }
            salaMin = Math.Max(1, salaMin);
            // Ajuste por turno: mediodía/noche cocina ≥ sala; tarde cocina ≤ sala (misma cantidad o menos cocina que sala)
            var isTarde = string.Equals(shiftName, "tarde", StringComparison.OrdinalIgnoreCase);
            if (isTarde && cocinaMin > salaMin)
            {
                salaMin = (int)Math.Ceiling(totalPeople / 2m);
                cocinaMin = totalPeople - salaMin;
                if (cocinaMin < 1) { cocinaMin = 1; salaMin = totalPeople - 1; }
            }
            else if (!isTarde && salaMin > cocinaMin)
            {
                cocinaMin = (int)Math.Ceiling(totalPeople / 2m);
                salaMin = totalPeople - cocinaMin;
                if (salaMin < 1) { salaMin = 1; cocinaMin = totalPeople - 1; }
            }
        }
        else
        {
            var (s, c) = SalaCocinaService.TotalToCocinaSalaByShift(totalPeople, shiftName);
            salaMin = s;
            cocinaMin = c;
        }

        // Con equipo 2-2-2 (sala y cocina) se puede facturar hasta 3000 €/día; por debajo no pasar de 2 en sala ni 2 en cocina por turno (misma regla para ambos).
        if (revenueDiaTotal < MinRevenueFor3PerShift)
        {
            salaMin = Math.Min(salaMin, 2);
            cocinaMin = Math.Min(cocinaMin, 2);
        }
        salaMin = Math.Clamp(salaMin, 1, 3);
        cocinaMin = Math.Clamp(cocinaMin, 1, 3);
        return (salaMin, cocinaMin);
    }

    /// <summary>Elige el menor esquema donde €/camarero y €/cocinero no superen el límite (por turno).</summary>
    private static (int Sala, int Cocina)? ApplyComfortLimit(
        decimal revenueTurno,
        IReadOnlyDictionary<string, decimal> comfortBySchema,
        IReadOnlyDictionary<string, decimal> comfortByCocina)
    {
        foreach (var schemaKey in AllowedSchemas)
        {
            var parts = schemaKey.Split('-');
            var S = int.Parse(parts[0], CultureInfo.InvariantCulture);
            var C = int.Parse(parts[1], CultureInfo.InvariantCulture);
            var limitSala = comfortBySchema.TryGetValue(schemaKey, out var ls) ? ls : DefaultComfortLimit;
            var limitCocina = comfortByCocina.TryGetValue(parts[1], out var lc) ? lc : DefaultComfortLimit;
            var eurCam = S > 0 ? revenueTurno / S : 0m;
            var eurCoc = C > 0 ? revenueTurno / C : 0m;
            if (eurCam <= limitSala * ComfortMargin && eurCoc <= limitCocina * ComfortMargin)
                return (S, C);
        }
        return null;
    }

    /// <summary>Dado el JSON de días de predicción, devuelve el mismo JSON con staffSala, staffCocina, staffSource por día. Devuelve null si el JSON es inválido.</summary>
    public async Task<string?> FillStaffRecommendationsJsonAsync(
        DateTime weekStartMonday,
        string? dailyPredictionsJson,
        decimal productividadEurHora,
        decimal horasPorTurno,
        StaffRevenueComfortResult? comfort,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(dailyPredictionsJson)) return dailyPredictionsJson;
        List<JsonElement>? list;
        try { list = JsonSerializer.Deserialize<List<JsonElement>>(dailyPredictionsJson); } catch { return dailyPredictionsJson; }
        if (list == null || list.Count == 0) return dailyPredictionsJson;
        var days = list.Select(JsonElementToDict).ToList();
        await FillStaffRecommendationsAsync(weekStartMonday, days, productividadEurHora, horasPorTurno, comfort, cancellationToken);
        return JsonSerializer.Serialize(days);
    }

    private static Dictionary<string, object?> JsonElementToDict(JsonElement e)
    {
        var d = new Dictionary<string, object?>();
        foreach (var p in e.EnumerateObject())
        {
            d[p.Name] = p.Value.ValueKind switch
            {
                JsonValueKind.String => p.Value.GetString(),
                JsonValueKind.Number => p.Value.TryGetInt32(out var i) ? i : (object)p.Value.GetDecimal(),
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                JsonValueKind.Null => null,
                _ => p.Value.GetRawText()
            };
        }
        return d;
    }

    /// <summary>Rellena en cada día del listado los campos staffSalaMed/Tar/Noc y staffCocinaMed/Tar/Noc (y opcionalmente staffSource).</summary>
    public async Task FillStaffRecommendationsAsync(
        DateTime weekStartMonday,
        IList<Dictionary<string, object?>> days,
        decimal productividadEurHora,
        decimal horasPorTurno,
        StaffRevenueComfortResult? comfort,
        CancellationToken cancellationToken = default)
    {
        if (days == null || days.Count == 0) return;
        if (productividadEurHora <= 0) productividadEurHora = 50m;
        if (horasPorTurno <= 0) horasPorTurno = 4m;

        var historic = await GetHistoricStaffByDowShiftAsync(weekStartMonday, cancellationToken);

        var comfortBySchema = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
        var comfortByCocina = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
        if (comfort != null)
        {
            foreach (var s in comfort.Schemas.Where(x => x.ComfortLimitApprox.HasValue))
                comfortBySchema[s.Schema] = s.ComfortLimitApprox!.Value;
            foreach (var c in comfort.CocinaSchemas.Where(x => x.ComfortLimitApprox.HasValue))
                comfortByCocina[c.Schema] = c.ComfortLimitApprox!.Value;
        }

        foreach (var day in days)
        {
            var dateStr = (day.GetValueOrDefault("date") ?? day.GetValueOrDefault("Date"))?.ToString();
            if (string.IsNullOrEmpty(dateStr) || !DateTime.TryParse(dateStr, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
                continue;
            var dow = Dow(date.Date);
            var rev = GetDec(day.GetValueOrDefault("revenue") ?? day.GetValueOrDefault("predictedRevenue"));
            var revMin = GetDec(day.GetValueOrDefault("min"));
            var med = GetDec(day.GetValueOrDefault("mediodia"));
            var tar = GetDec(day.GetValueOrDefault("tarde"));
            var noc = GetDec(day.GetValueOrDefault("noche"));

            // Para diagramar al equipo usamos la parte más baja del rango (min), así no sobre-dimensionamos si la facturación baja.
            decimal revenueDiaForStaff = rev;
            decimal medForStaff = med, tarForStaff = tar, nocForStaff = noc;
            if (revMin > 0 && rev > 0)
            {
                revenueDiaForStaff = revMin;
                var ratio = revMin / rev;
                medForStaff = Math.Round(med * ratio, 2);
                tarForStaff = Math.Round(tar * ratio, 2);
                nocForStaff = Math.Round(noc * ratio, 2);
            }

            var (salaMed, cocinaMed, salaTar, cocinaTar, salaNoc, cocinaNoc) = (0, 0, 0, 0, 0, 0);
            var sources = new List<string>();

            foreach (var (shiftName, revTurno) in new[] { ("mediodia", medForStaff), ("tarde", tarForStaff), ("noche", nocForStaff) })
            {
                var (sala, cocina, source) = ResolveStaffForShift(dow, shiftName, revTurno, revenueDiaForStaff, productividadEurHora, horasPorTurno, historic, comfortBySchema, comfortByCocina);
                if (shiftName == "mediodia") { salaMed = sala; cocinaMed = cocina; }
                else if (shiftName == "tarde") { salaTar = sala; cocinaTar = cocina; }
                else { salaNoc = sala; cocinaNoc = cocina; }
                if (!string.IsNullOrEmpty(source)) sources.Add(source);
            }

            // Ajustar a combinaciones permitidas, respetando el orden de facturación (más facturación → al menos tantas personas; evita 1 en mediodía y 2 en tarde si se factura más al mediodía).
            var (salaMedF, salaTarF, salaNocF) = SnapToAllowed((salaMed, salaTar, salaNoc), AllowedSala, medForStaff, tarForStaff, nocForStaff);
            var (cocinaMedF, cocinaTarF, cocinaNocF) = SnapToAllowed((cocinaMed, cocinaTar, cocinaNoc), AllowedCocina, medForStaff, tarForStaff, nocForStaff);

            day["staffSalaMed"] = salaMedF;
            day["staffSalaTar"] = salaTarF;
            day["staffSalaNoc"] = salaNocF;
            day["staffCocinaMed"] = cocinaMedF;
            day["staffCocinaTar"] = cocinaTarF;
            day["staffCocinaNoc"] = cocinaNocF;
            day["staffSala"] = $"{salaMedF}-{salaTarF}-{salaNocF}";
            day["staffCocina"] = $"{cocinaMedF}-{cocinaTarF}-{cocinaNocF}";
            day["staffSource"] = sources.Contains("historic") ? (sources.All(x => x == "historic") ? "historic" : "mixed") : "heuristic";
        }
    }

    /// <summary>Devuelve la combinación permitida más cercana a (m,t,n), consistente con el orden de facturación por turno (revMed, revTar, revNoc): si hay más facturación en un turno, ese turno debe tener al menos tantas personas que los de menor facturación.</summary>
    private static (int M, int T, int N) SnapToAllowed((int M, int T, int N) value, (int M, int T, int N)[] allowed, decimal revMed, decimal revTar, decimal revNoc)
    {
        var (m, t, n) = value;
        int Dist(int a, int b, int c)
        {
            return Math.Abs(a - m) + Math.Abs(b - t) + Math.Abs(c - n);
        }
        bool Covers(int a, int b, int c) => a >= m && b >= t && c >= n;
        int Sum(int a, int b, int c) => a + b + c;

        // Solo combinaciones consistentes con el orden de facturación: más facturación → al menos tantas personas (evita 1 mediodía y 2 tarde cuando se factura más al mediodía).
        bool Consistent(int a, int b, int c)
        {
            if (revMed >= revTar && a < b) return false;
            if (revTar >= revNoc && b < c) return false;
            if (revMed >= revNoc && a < c) return false;
            if (revTar >= revMed && b < a) return false;
            if (revNoc >= revTar && c < b) return false;
            if (revNoc >= revMed && c < a) return false;
            return true;
        }

        var candidates = allowed.Where(x => Consistent(x.M, x.T, x.N)).ToArray();
        var pool = candidates.Length > 0 ? candidates : allowed;

        var covering = pool.Where(x => Covers(x.M, x.T, x.N)).ToList();
        if (covering.Count > 0)
        {
            var best = covering.OrderBy(x => Sum(x.M, x.T, x.N)).ThenBy(x => Dist(x.M, x.T, x.N)).First();
            return best;
        }
        var closest = pool.OrderBy(x => Dist(x.M, x.T, x.N)).ThenBy(x => Sum(x.M, x.T, x.N)).First();
        return closest;
    }

    private static (int Sala, int Cocina, string Source) ResolveStaffForShift(
        int dow,
        string shiftName,
        decimal revenueTurno,
        decimal revenueDiaTotal,
        decimal productividadEurHora,
        decimal horasPorTurno,
        Dictionary<(int Dow, string Shift), (int Sala, int Cocina, decimal AvgRevenue)> historic,
        Dictionary<string, decimal> comfortBySchema,
        Dictionary<string, decimal> comfortByCocina)
    {
        var (salaMin, cocinaMin) = MinStaffByProductivity(revenueTurno, productividadEurHora, horasPorTurno, revenueDiaTotal, dow, shiftName, historic);

        if (historic.TryGetValue((dow, shiftName), out var hist))
        {
            var sala = hist.Sala;
            var cocina = hist.Cocina;
            if (hist.AvgRevenue > 0 && revenueTurno > 0)
            {
                var ratio = revenueTurno / hist.AvgRevenue;
                ratio = Math.Clamp(ratio, RevenueRatioMin, RevenueRatioMax);
                sala = Math.Clamp((int)Math.Round(hist.Sala * ratio, MidpointRounding.AwayFromZero), 1, 3);
                cocina = Math.Clamp((int)Math.Round(hist.Cocina * ratio, MidpointRounding.AwayFromZero), 1, 3);
            }
            sala = Math.Max(sala, salaMin);
            cocina = Math.Max(cocina, cocinaMin);
            sala = Math.Clamp(sala, 1, 3);
            cocina = Math.Clamp(cocina, 1, 3);
            return (sala, cocina, "historic");
        }

        var fromComfort = ApplyComfortLimit(revenueTurno, comfortBySchema, comfortByCocina);
        if (fromComfort.HasValue)
        {
            var (s, c) = fromComfort.Value;
            s = Math.Max(s, salaMin);
            c = Math.Max(c, cocinaMin);
            return (Math.Clamp(s, 1, 3), Math.Clamp(c, 1, 3), "heuristic");
        }

        return (Math.Clamp(salaMin, 1, 3), Math.Clamp(cocinaMin, 1, 3), "heuristic");
    }
}
