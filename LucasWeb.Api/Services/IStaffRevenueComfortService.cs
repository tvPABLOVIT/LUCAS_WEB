using LucasWeb.Api.DTOs;

namespace LucasWeb.Api.Services;

/// <summary>Agregados por esquema de personal (sala-cocina) y banda de facturación por camarero para "límite cómodo".</summary>
public interface IStaffRevenueComfortService
{
    /// <summary>Agrupa turnos por esquema (ej. "1-1") y banda de RevenuePerWaiterSala; devuelve count y dificultad media por grupo.</summary>
    Task<StaffRevenueComfortResult> GetAggregatesAsync(int? minShifts = null, CancellationToken cancellationToken = default);
}
