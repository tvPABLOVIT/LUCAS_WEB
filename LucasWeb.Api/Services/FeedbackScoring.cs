namespace LucasWeb.Api.Services;

/// <summary>
/// Índices 1–5 a partir de las opciones de feedback Q1–Q4 (igual que scoring.js).
/// DifficultyScore y ComfortLevel para análisis de "límite cómodo" por esquema de personal.
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
}
