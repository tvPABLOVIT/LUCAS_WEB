namespace LucasWeb.Api.Services;

/// <summary>Obtiene coordenadas (lat/lon) a partir de una dirección para usar con el clima.</summary>
public interface IGeocodingService
{
    /// <summary>Geocodifica una dirección y devuelve latitud y longitud. Devuelve null si no hay resultados o hay error.</summary>
    /// <param name="address">Dirección o nombre de lugar (ej. "Carrer de Girona, 70 Barcelona").</param>
    /// <param name="countryCode">Código ISO de país opcional (ej. "ES") para afinar resultados.</param>
    Task<(decimal? Lat, decimal? Lon)> GetCoordinatesAsync(string address, string? countryCode = null);
}
