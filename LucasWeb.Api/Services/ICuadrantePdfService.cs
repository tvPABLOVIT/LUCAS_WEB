using LucasWeb.Api.DTOs;

namespace LucasWeb.Api.Services;

/// <summary>Parsea un PDF de cuadrante BETLEM (invocando LucasCuadranteParser) y devuelve los días con turnos (horas programadas + personal).</summary>
public interface ICuadrantePdfService
{
    /// <summary>Ejecuta el parser sobre el PDF y devuelve la lista de días. Lanza si el PDF no se puede procesar.</summary>
    Task<List<CuadranteDayDto>> ParsePdfAsync(Stream pdfStream, CancellationToken cancellationToken = default);
}
