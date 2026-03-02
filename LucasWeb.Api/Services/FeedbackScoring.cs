namespace LucasWeb.Api.Services;

/// <summary>
/// Dificultad y confort por turno para analytics (límite cómodo, predicción de personal).
/// — DifficultyScore 1–5: métrica de DIFICULTAD; se persiste en ShiftFeedback y se usa en StaffRevenueComfortService.
/// — En el frontend, scoring.js calcula SGT (6–31) y Estado: métrica de INTENSIDAD/ESTADO para la UI (no es lo mismo que DifficultyScore).
/// Opciones Q1–Q4 (y Q5 = mismas que Q4): única fuente de verdad en backend; mantener en sync con scoring.js.
/// </summary>
public static class FeedbackScoring
{
    public static readonly string[] Q1Options = { "Pocas mesas", "Media sala", "Sala completa", "Sala y terraza completas", "Sala y terraza completas y doblamos mesas" };
    public static readonly string[] Q2Options = { "Muy espaciadas, sin acumulación", "Entradas tranquilas", "Flujo constante", "Muchas entradas juntas", "Entradas continuas sin margen" };
    public static readonly string[] Q3Options = { "Siempre adelantado", "Generalmente con margen", "Justo", "Poco margen", "Ningún margen" };
    public static readonly string[] Q4Options = { "Muy fácil", "Fácil", "Normal", "Difícil", "Muy difícil" };

    /// <summary>Devuelve índice 1–5 si el texto coincide con una opción; 0 si no.</summary>
    public static int OptionToIndex(string? text, string[] options)
    {
        if (string.IsNullOrWhiteSpace(text)) return 0;
        var t = text.Trim();
        for (var i = 0; i < options.Length; i++)
            if (string.Equals(options[i].Trim(), t, StringComparison.OrdinalIgnoreCase))
                return i + 1;
        return 0;
    }

    /// <summary>
    /// Dificultad del turno 1–5. Usa Q4 (dificultad) como principal; si falta, media ponderada de Q1–Q4 (más peso a Q4 y Q2).
    /// Devuelve null si no hay ningún feedback válido.
    /// </summary>
    public static decimal? ComputeDifficultyScore(string? q1, string? q2, string? q3, string? q4)
    {
        var v = OptionToIndex(q1, Q1Options);
        var r = OptionToIndex(q2, Q2Options);
        var m = OptionToIndex(q3, Q3Options);
        var d = OptionToIndex(q4, Q4Options);
        if (d >= 1 && d <= 5) return d;
        if (v < 1 && r < 1 && m < 1) return null;
        var count = 0;
        var sum = 0m;
        if (v >= 1) { sum += v; count++; }
        if (r >= 1) { sum += r; count++; }
        if (m >= 1) { sum += (6 - m); count++; } // margen bajo = más difícil
        if (d >= 1) { sum += d * 2; count += 2; }
        if (count == 0) return null;
        var raw = sum / count;
        if (d >= 1) return Math.Clamp(raw, 1, 5);
        return Math.Clamp(raw, 1, 5);
    }

    /// <summary>Cómodo &lt;= 2.5, Límite 2.5–3.5, Complicado &gt; 3.5.</summary>
    public static string? GetComfortLevel(decimal? difficultyScore)
    {
        if (difficultyScore == null) return null;
        if (difficultyScore <= 2.5m) return "Cómodo";
        if (difficultyScore <= 3.5m) return "Límite";
        return "Complicado";
    }

    /// <summary>Dificultad cocina 1–5 a partir de Q5 (mismas opciones que Q4). Devuelve null si no hay Q5.</summary>
    public static decimal? ComputeDifficultyScoreKitchen(string? q5)
    {
        var d = OptionToIndex(q5, Q4Options);
        if (d >= 1 && d <= 5) return d;
        return null;
    }

    /// <summary>SGT (Score Global Turno) 6–31 a partir de Q1–Q4. Misma fórmula que scoring.js: (V×2)+R+(6−M)+D. Devuelve 0 si falta algún eje.</summary>
    public static int CalcSgt(string? q1, string? q2, string? q3, string? q4)
    {
        var v = OptionToIndex(q1, Q1Options);
        var r = OptionToIndex(q2, Q2Options);
        var m = OptionToIndex(q3, Q3Options);
        var d = OptionToIndex(q4, Q4Options);
        if (v < 1 || v > 5 || r < 1 || r > 5 || m < 1 || m > 5 || d < 1 || d > 5) return 0;
        return (v * 2) + r + (6 - m) + d;
    }

    /// <summary>Estado del turno según SGT (igual que scoring.js getEstado). Null si SGT &lt; 6.</summary>
    public static string? GetEstadoFromSgt(int sgt)
    {
        if (sgt < 6) return null;
        if (sgt <= 10) return "Infrautilizado";
        if (sgt <= 14) return "Tranquilo";
        if (sgt <= 18) return "Equilibrado";
        if (sgt <= 22) return "Productivo";
        if (sgt <= 26) return "Exigente";
        if (sgt <= 31) return "Crítico";
        return null;
    }
}
