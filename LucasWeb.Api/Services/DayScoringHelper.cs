using LucasWeb.Api.Models;

namespace LucasWeb.Api.Services;

/// <summary>Scoring a nivel de día: agrega los resultados de cada turno (SGT, estado) para obtener un resumen diario.</summary>
public static class DayScoringHelper
{
    private static readonly string[] ShiftOrder = { "Mediodia", "Tarde", "Noche" };
    private static readonly string[] ShiftLabels = { "mañana", "tarde", "noche" };

    /// <summary>
    /// Calcula el estado del día a partir del promedio SGT.
    /// Usa la misma tabla que los turnos (FeedbackScoring.GetEstadoFromSgt) para que los nombres de estado sean coherentes.
    /// </summary>
    public static string GetDayEstadoFromAvgSgt(double avgSgt)
    {
        if (avgSgt < 6) return "Sin datos";
        var sgtRounded = (int)System.Math.Round(avgSgt);
        var estado = FeedbackScoring.GetEstadoFromSgt(sgtRounded);
        return estado ?? "Sin datos";
    }

    /// <summary>Calcula SGT del día, estado del día y párrafo de resumen diario a partir de los turnos.</summary>
    public static (decimal? DaySgt, string? DayEstado, string? DayResumenDiario) ComputeDayResumen(IEnumerable<ShiftFeedback>? shifts)
    {
        if (shifts == null) return (null, null, null);

        var list = shifts.ToList();
        var sgtByShift = new List<int>();
        var estadoByShift = new List<string>();

        foreach (var name in ShiftOrder)
        {
            var shift = list.FirstOrDefault(s => string.Equals(s.ShiftName, name, StringComparison.OrdinalIgnoreCase));
            var sgt = shift != null ? FeedbackScoring.CalcSgt(shift.FeedbackQ1, shift.FeedbackQ2, shift.FeedbackQ3, shift.FeedbackQ4) : 0;
            if (sgt >= 6)
            {
                sgtByShift.Add(sgt);
                var est = FeedbackScoring.GetEstadoFromSgt(sgt);
                estadoByShift.Add(est ?? "—");
            }
            else
                estadoByShift.Add("—");
        }

        decimal? daySgt = null;
        string? dayEstado = null;
        if (sgtByShift.Count > 0)
        {
            daySgt = (decimal)sgtByShift.Average();
            dayEstado = GetDayEstadoFromAvgSgt((double)daySgt.Value);
        }

        var parts = new List<string>();
        if (!string.IsNullOrEmpty(dayEstado) && dayEstado != "Sin datos")
            parts.Add("Día " + dayEstado.ToLowerInvariant() + ".");

        var evolutionParts = new List<string>();
        for (var i = 0; i < ShiftLabels.Length && i < estadoByShift.Count; i++)
        {
            var e = estadoByShift[i];
            var label = "Por la " + ShiftLabels[i];
            if (e == "—")
                evolutionParts.Add(label + " sin datos");
            else
                evolutionParts.Add(label + " " + e.ToLowerInvariant());
        }
        if (evolutionParts.Count > 0)
            parts.Add(string.Join(", ", evolutionParts) + ".");

        var dayResumenDiario = parts.Count > 0 ? string.Join(" ", parts) : null;
        return (daySgt, dayEstado, dayResumenDiario);
    }

    /// <summary>Genera un párrafo de conclusión narrativa del día (estado, estabilidad, flujo) para las observaciones del dashboard.</summary>
    public static string? BuildDayConclusion(IEnumerable<ShiftFeedback>? shifts)
    {
        if (shifts == null) return null;

        var list = shifts.ToList();
        var estadoByShift = new List<string>();
        var sgtList = new List<int>();
        foreach (var name in ShiftOrder)
        {
            var shift = list.FirstOrDefault(s => string.Equals(s.ShiftName, name, StringComparison.OrdinalIgnoreCase));
            var sgt = shift != null ? FeedbackScoring.CalcSgt(shift.FeedbackQ1, shift.FeedbackQ2, shift.FeedbackQ3, shift.FeedbackQ4) : 0;
            if (sgt >= 6)
            {
                sgtList.Add(sgt);
                var est = FeedbackScoring.GetEstadoFromSgt(sgt);
                estadoByShift.Add(est ?? "—");
            }
            else
                estadoByShift.Add("—");
        }

        var validEstados = estadoByShift.Where(e => e != "—").ToList();
        if (validEstados.Count == 0 || sgtList.Count == 0) return null;

        var avgSgt = sgtList.Average();
        var dayEstado = GetDayEstadoFromAvgSgt(avgSgt);
        if (string.IsNullOrEmpty(dayEstado) || dayEstado == "Sin datos") return null;

        var dayPhrase = dayEstado.ToLowerInvariant() switch
        {
            "tranquilo" => "Fue un día tranquilo",
            "equilibrado" => "Fue un día normal tirando a bueno",
            "productivo" => "Fue un día productivo",
            "exigente" => "Fue un día exigente",
            "crítico" => "Fue un día intenso",
            _ => "Fue un día " + dayEstado.ToLowerInvariant()
        };

        var hasCritical = validEstados.Any(e => e == "Exigente" || e == "Crítico");
        var distinctCount = validEstados.Distinct().Count();
        var stable = distinctCount <= 1 && validEstados.Count >= 2;
        var singleShift = validEstados.Count == 1;
        string stabilityPhrase;
        if (singleShift)
        {
            stabilityPhrase = string.Empty;
        }
        else
        {
            stabilityPhrase = stable
                ? ", estable en todos los turnos"
                : (hasCritical ? ", con variación entre turnos" : ", con variación entre turnos pero controlada");
        }

        string flowPhrase = hasCritical
            ? (validEstados.Any(e => e == "Crítico") ? ", con momentos críticos." : ", con momentos de mayor exigencia.")
            : ", con un flujo de trabajo controlado y sin momentos críticos.";

        return dayPhrase + stabilityPhrase + flowPhrase;
    }
}
