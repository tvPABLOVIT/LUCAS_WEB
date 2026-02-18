namespace LucasWeb.Api.Services;

/// <summary>
/// Esquema sala/cocina según documento §7: personas por turno a partir de revenue,
/// reparto TotalToCocinaSala y umbrales.
/// </summary>
public static class SalaCocinaService
{
    /// <summary>Convierte total personas por turno en (sala, cocina). 2→(1,1), 3→(2,1), 4→(2,2), 5→(3,2), 6→(3,3). Máx 3 cada uno.</summary>
    public static (int Sala, int Cocina) TotalToCocinaSala(int total)
    {
        if (total <= 0) return (0, 0);
        if (total == 1) return (1, 0);
        if (total == 2) return (1, 1);
        if (total == 3) return (2, 1);
        if (total == 4) return (2, 2);
        if (total == 5) return (3, 2);
        return (3, 3);
    }

    /// <summary>
    /// Reparto por turno: en mediodía y noche cocina tiene la misma cantidad o más que sala; en tarde cocina tiene la misma cantidad o menos que sala.
    /// Ejemplo real: sala 1-1-2, cocina 2-1-2.
    /// </summary>
    public static (int Sala, int Cocina) TotalToCocinaSalaByShift(int total, string shiftName)
    {
        if (total <= 0) return (0, 0);
        if (total == 1) return (1, 0);
        if (total == 2) return (1, 1);
        var isTarde = shiftName != null && shiftName.Trim().Equals("tarde", StringComparison.OrdinalIgnoreCase);
        if (isTarde)
        {
            // Tarde: cocina misma cantidad o menos que sala (cocina ≤ sala)
            if (total == 3) return (2, 1);
            if (total == 4) return (2, 2);
            if (total == 5) return (3, 2);
            return (3, 3);
        }
        // Mediodía y Noche: cocina misma cantidad o más que sala (cocina ≥ sala)
        if (total == 3) return (1, 2);
        if (total == 4) return (2, 2);
        if (total == 5) return (2, 3);
        return (3, 3);
    }

    /// <summary>
    /// Calcula esquema sala y cocina para los 3 turnos (mediodía, tarde, noche).
    /// Usa TotalToCocinaSalaByShift: tarde cocina ≤ sala; mediodía/noche cocina ≥ sala.
    /// Umbrales: día ≥ 2400 € → mín 2 sala y 2 cocina por turno; día &gt; 3000 → máx cocina 3; día ≥ 3500 → máx sala 3; turno &gt; 600 € → mín sala 2.
    /// </summary>
    public static (string Sala, string Cocina) GetSalaCocinaScheme(
        decimal revenueMediodia, decimal revenueTarde, decimal revenueNoche,
        decimal productividadIdealEurHora, decimal horasPorTurno,
        decimal revenueDiaTotal)
    {
        if (productividadIdealEurHora <= 0 || horasPorTurno <= 0)
            return ("—", "—");
        decimal divisor = productividadIdealEurHora * horasPorTurno;
        int PersonasTurno(decimal rev)
        {
            var n = (int)Math.Round(rev / divisor, MidpointRounding.AwayFromZero);
            return n < 1 ? 1 : Math.Min(n, 6);
        }
        int m = PersonasTurno(revenueMediodia);
        int t = PersonasTurno(revenueTarde);
        int n = PersonasTurno(revenueNoche);
        bool requiere2PorTurno = revenueDiaTotal >= 2400;
        int maxCocina = revenueDiaTotal > 3000 ? 3 : 2;
        int maxSala = revenueDiaTotal >= 3500 ? 3 : 2;
        int minSalaTurno(decimal rev) => rev > 600 ? 2 : 1;
        int sm = 0, cm = 0, st = 0, ct = 0, sn = 0, cn = 0;
        void Aplicar(int total, decimal rev, string turno, out int sala, out int cocina)
        {
            var (s, c) = TotalToCocinaSalaByShift(total, turno);
            int minS = requiere2PorTurno ? 2 : minSalaTurno(rev);
            sala = Math.Max(s, minS);
            cocina = Math.Max(c, requiere2PorTurno ? 2 : 1);
            sala = Math.Min(sala, maxSala);
            cocina = Math.Min(cocina, maxCocina);
        }
        Aplicar(m, revenueMediodia, "mediodia", out sm, out cm);
        Aplicar(t, revenueTarde, "tarde", out st, out ct);
        Aplicar(n, revenueNoche, "noche", out sn, out cn);
        return (sm + "-" + st + "-" + sn, cm + "-" + ct + "-" + cn);
    }
}
