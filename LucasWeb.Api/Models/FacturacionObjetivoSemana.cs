namespace LucasWeb.Api.Models;

/// <summary>
/// Facturación objetivo guardada para una semana (para histórico cuando el objetivo cambia).
/// </summary>
public class FacturacionObjetivoSemana
{
    public DateTime WeekStart { get; set; }
    public decimal TargetRevenue { get; set; }
}
