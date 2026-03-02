using LucasWeb.Api.Models;

namespace LucasWeb.Api.Services;

/// <summary>Genera texto de observaciones a partir del feedback por turno (Q1–Q5), reutilizable por el sync del Google Sheet y el dashboard.
/// Usa los índices de scoring (V,R,M,D) para elegir frases naturales y variadas, con longitud similar.</summary>
public static class FeedbackObservationsHelper
{
    // Niveles: 0 = bajo (1-2), 1 = medio (3), 2 = alto (4-5)
    private static int Level(int index) => index <= 0 ? -1 : index <= 2 ? 0 : index == 3 ? 1 : 2;

    #region Frases por eje (variedad natural)

    private static readonly string[] VolumenBajo = {
        "poca carga", "la carga fue baja", "volumen bajo", "poco movimiento", "trabajo tranquilo en cuanto a volumen"
    };
    private static readonly string[] VolumenMedio = {
        "volumen normal", "carga media", "sala completa, volumen acorde", "nivel de trabajo habitual", "volumen acorde al día"
    };
    private static readonly string[] VolumenAlto = {
        "bastante volumen", "carga alta", "sala y terraza a tope", "mucho movimiento", "volumen fuerte", "trabajo intenso en volumen"
    };

    private static readonly string[] RitmoBajo = {
        "entradas muy repartidas", "ritmo tranquilo", "sin agobios de entradas", "las entradas vinieron espaciadas", "ritmo llevadero"
    };
    private static readonly string[] RitmoMedio = {
        "flujo constante", "ritmo regular", "entradas repartidas", "el ritmo fue constante", "ni picos ni vacíos"
    };
    private static readonly string[] RitmoAlto = {
        "muchas entradas juntas", "ritmo fuerte", "picos de entrada", "entradas continuas", "sin respiro entre entradas", "el ritmo apretó"
    };

    private static readonly string[] MargenBajo = {
        "con margen", "siempre adelantados", "holgura suficiente", "se pudo ir adelantado", "con buen margen", "sin ir apurados"
    };
    private static readonly string[] MargenMedio = {
        "margen justo", "ajustado pero controlado", "ni holgura ni ahogo", "justo de tiempo", "margen suficiente pero justo"
    };
    private static readonly string[] MargenAlto = {
        "poco margen", "sin respiro", "muy ajustado", "sin margen operativo", "ir siempre al límite", "no hubo margen"
    };

    private static readonly string[] DificultadBaja = {
        "En conjunto resultó muy llevadero.", "A nivel global, fácil.", "El turno fue tranquilo.", "Sensación global: fácil.", "Muy llevadero en conjunto."
    };
    private static readonly string[] DificultadMedia = {
        "En conjunto resultó normal.", "A nivel global, normal.", "Dificultad normal.", "Ni fácil ni duro.", "Sensación global: normal.", "Un turno normal."
    };
    private static readonly string[] DificultadAlta = {
        "En conjunto resultó duro.", "A nivel global, exigente.", "El turno fue difícil.", "Sensación global: dura.", "Costó llevarlo.", "Turno exigente."
    };

    private static readonly string[] CocinaBaja = {
        "En cocina, llevadero.", "Cocina: tranquila.", "Por cocina, bien.", "En cocina resultó fácil.", "Cocina sin complicaciones."
    };
    private static readonly string[] CocinaMedia = {
        "En cocina, normal.", "Cocina: normal.", "Por cocina, acorde.", "En cocina resultó normal.", "Cocina al uso."
    };
    private static readonly string[] CocinaAlta = {
        "En cocina, exigente.", "Cocina: dura.", "Por cocina, apretada.", "En cocina resultó difícil.", "Cocina a presión."
    };

    private static readonly string[] Aperturas = {
        "Fue un turno de ", "El turno tuvo ", "Volumen ", "En este turno, ", "Se trabajó con "
    };

    // Estructuras para combinar V+R+M: índice 0 = "[V] y [R], con [M].", 1 = "[V], [R] y [M].", etc.
    private static readonly (string beforeV, string betweenVR, string betweenRM, string afterM)[] Estructuras = {
        ("", " y ", ", con ", "."),
        ("", ", ", " y ", "."),
        ("", " y ", "; ", "."),
        ("", ", ", ", ", "."),
    };

    #endregion

    /// <summary>Observaciones del turno: párrafo natural y variado a partir de los índices de scoring (Q1–Q5). Retorna "" si no hay feedback.</summary>
    public static string BuildObservacionesFromFeedback(ShiftFeedback? shift)
    {
        if (shift == null) return "";

        var v = FeedbackScoring.OptionToIndex(shift.FeedbackQ1, FeedbackScoring.Q1Options);
        var r = FeedbackScoring.OptionToIndex(shift.FeedbackQ2, FeedbackScoring.Q2Options);
        var m = FeedbackScoring.OptionToIndex(shift.FeedbackQ3, FeedbackScoring.Q3Options);
        var d = FeedbackScoring.OptionToIndex(shift.FeedbackQ4, FeedbackScoring.Q4Options);
        var dCocina = FeedbackScoring.OptionToIndex(shift.FeedbackQ5, FeedbackScoring.Q4Options);

        var hasSala = v >= 1 || r >= 1 || m >= 1 || d >= 1;
        if (!hasSala && dCocina < 1) return "";

        var seed = GetSeed(shift);
        var parts = new List<string>();

        if (hasSala)
        {
            var fraseSala = BuildFraseSala(v, r, m, d, seed);
            if (!string.IsNullOrEmpty(fraseSala))
                parts.Add(fraseSala);

            if (d >= 1)
            {
                var fraseD = Pick(DificultadBaja, DificultadMedia, DificultadAlta, Level(d), seed + 1);
                if (!string.IsNullOrEmpty(fraseD))
                    parts.Add(fraseD);
            }
        }

        if (dCocina >= 1)
        {
            var fraseCocina = Pick(CocinaBaja, CocinaMedia, CocinaAlta, Level(dCocina), seed + 2);
            if (!string.IsNullOrEmpty(fraseCocina))
                parts.Add(fraseCocina);
        }

        return parts.Count > 0 ? string.Join(" ", parts) : "";
    }

    private static int GetSeed(ShiftFeedback shift)
    {
        var h = (shift.ShiftName ?? "").GetHashCode(StringComparison.OrdinalIgnoreCase);
        var g = shift.ExecutionDayId.GetHashCode();
        return Math.Abs(unchecked(h + g));
    }

    private static string BuildFraseSala(int v, int r, int m, int d, int seed)
    {
        var lv = Level(v);
        var lr = Level(r);
        var lm = Level(m);

        if (lv < 0 && lr < 0 && lm < 0) return "";

        var vPhrase = lv >= 0 ? Pick(VolumenBajo, VolumenMedio, VolumenAlto, lv, seed) : "";
        var rPhrase = lr >= 0 ? Pick(RitmoBajo, RitmoMedio, RitmoAlto, lr, seed + 10) : "";
        var mPhrase = lm >= 0 ? Pick(MargenBajo, MargenMedio, MargenAlto, lm, seed + 20) : "";

        if (string.IsNullOrEmpty(vPhrase) && string.IsNullOrEmpty(rPhrase) && string.IsNullOrEmpty(mPhrase)) return "";

        if (string.IsNullOrEmpty(vPhrase)) vPhrase = VolumenMedio[0];
        if (string.IsNullOrEmpty(rPhrase)) rPhrase = RitmoMedio[0];
        if (string.IsNullOrEmpty(mPhrase)) mPhrase = MargenMedio[0];

        var openingIdx = seed % Aperturas.Length;
        var structIdx = (seed / 7) % Estructuras.Length;
        var open = Aperturas[openingIdx];
        var (_, betweenVR, betweenRM, afterM) = Estructuras[structIdx];

        var sala = open + vPhrase + betweenVR + rPhrase + betweenRM + mPhrase + afterM;
        return Capitalize(sala);
    }

    private static string Pick(string[] low, string[] mid, string[] high, int level, int seed)
    {
        var arr = level == 0 ? low : level == 1 ? mid : high;
        if (arr == null || arr.Length == 0) return "";
        return arr[seed % arr.Length];
    }

    private static string Capitalize(string s)
    {
        if (string.IsNullOrEmpty(s)) return s;
        return char.ToUpperInvariant(s[0]) + s[1..];
    }

    /// <summary>Resumen del día: concatena observaciones de cada turno (Mediodía, Tarde, Noche) con etiquetas. Retorna "" si ninguno tiene contenido.</summary>
    public static string BuildDayFeedbackSummary(IEnumerable<ShiftFeedback> shifts)
    {
        if (shifts == null) return "";
        var order = new[] { "Mediodia", "Tarde", "Noche" };
        var labels = new[] { "Mediodía", "Tarde", "Noche" };
        var list = shifts.ToList();
        var parts = new List<string>();
        for (var i = 0; i < order.Length; i++)
        {
            var shift = list.FirstOrDefault(s => string.Equals(s.ShiftName, order[i], StringComparison.OrdinalIgnoreCase));
            var text = BuildObservacionesFromFeedback(shift);
            if (!string.IsNullOrWhiteSpace(text))
                parts.Add(labels[i] + ": " + text);
        }
        return parts.Count > 0 ? string.Join(" ", parts) : "";
    }
}
