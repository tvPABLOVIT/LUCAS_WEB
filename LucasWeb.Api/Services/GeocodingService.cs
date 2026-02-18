using System.Globalization;
using System.Text.Json;

namespace LucasWeb.Api.Services;

/// <summary>Geocoding con Open-Meteo (mismo proveedor que el clima, sin API key).</summary>
public class GeocodingService : IGeocodingService
{
    private readonly HttpClient _http;
    private const string OpenMeteoUrl = "https://geocoding-api.open-meteo.com/v1/search";
    private const string NominatimUrl = "https://nominatim.openstreetmap.org/search";

    public GeocodingService(IHttpClientFactory factory)
    {
        _http = factory.CreateClient();
        _http.Timeout = TimeSpan.FromSeconds(12);
        _http.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "LucasWeb/1.0 (config@localhost)");
    }

    public async Task<(decimal? Lat, decimal? Lon)> GetCoordinatesAsync(string address, string? countryCode = null)
    {
        var trimmed = (address ?? "").Trim();
        if (trimmed.Length < 2)
            return (null, null);

        // Dirección con coma o números → intentar Nominatim primero (mejor para calles)
        var looksLikeStreet = trimmed.IndexOf(',') >= 0 || System.Text.RegularExpressions.Regex.IsMatch(trimmed, @"\d");
        if (looksLikeStreet)
        {
            var result = await TryNominatimAsync(trimmed).ConfigureAwait(false);
            if (result.Lat.HasValue && result.Lon.HasValue)
                return result;
        }

        // Open-Meteo (ciudad/región)
        var resultOm = await TryOpenMeteoAsync(trimmed, countryCode).ConfigureAwait(false);
        if (resultOm.Lat.HasValue && resultOm.Lon.HasValue)
            return resultOm;

        if (!looksLikeStreet)
        {
            var resultNom = await TryNominatimAsync(trimmed).ConfigureAwait(false);
            if (resultNom.Lat.HasValue && resultNom.Lon.HasValue)
                return resultNom;
        }

        // Fallback: extraer ciudad (último segmento tras coma, ej. "CARRER X, 70, BARCELONA" → "Barcelona")
        var parts = trimmed.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length >= 2)
        {
            var city = parts[parts.Length - 1];
            if (city.Length >= 2)
            {
                resultOm = await TryOpenMeteoAsync(city, countryCode ?? "ES").ConfigureAwait(false);
                if (resultOm.Lat.HasValue && resultOm.Lon.HasValue)
                    return resultOm;
            }
        }

        // Último recurso: Barcelona si la dirección lo menciona (clima aproximado)
        if (trimmed.Contains("barcelona", StringComparison.OrdinalIgnoreCase))
            return (41.3851m, 2.1734m);

        return (null, null);
    }

    private async Task<(decimal? Lat, decimal? Lon)> TryOpenMeteoAsync(string address, string? countryCode)
    {
        var name = Uri.EscapeDataString(address);
        var url = $"{OpenMeteoUrl}?name={name}&count=1&language=es";
        if (!string.IsNullOrWhiteSpace(countryCode))
            url += "&countryCode=" + Uri.EscapeDataString(countryCode.Trim());

        try
        {
            var response = await _http.GetAsync(url).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            var json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("results", out var results) || results.GetArrayLength() == 0)
                return (null, null);

            var first = results[0];
            decimal? lat = null, lon = null;
            if (first.TryGetProperty("latitude", out var latEl))
            {
                if (latEl.TryGetDecimal(out var la)) lat = la;
                else if (latEl.TryGetDouble(out var lad)) lat = (decimal)lad;
            }
            if (first.TryGetProperty("longitude", out var lonEl))
            {
                if (lonEl.TryGetDecimal(out var lo)) lon = lo;
                else if (lonEl.TryGetDouble(out var lod)) lon = (decimal)lod;
            }
            return (lat, lon);
        }
        catch
        {
            return (null, null);
        }
    }

    private async Task<(decimal? Lat, decimal? Lon)> TryNominatimAsync(string address)
    {
        var q = Uri.EscapeDataString(address);
        var url = $"{NominatimUrl}?q={q}&format=json&limit=1";

        try
        {
            var response = await _http.GetAsync(url).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            var json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Array || doc.RootElement.GetArrayLength() == 0)
                return (null, null);

            var first = doc.RootElement[0];
            decimal? lat = null, lon = null;
            if (first.TryGetProperty("lat", out var latEl) && latEl.ValueKind == JsonValueKind.String)
            {
                var s = latEl.GetString();
                if (!string.IsNullOrEmpty(s) && decimal.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var la))
                    lat = la;
            }
            if (first.TryGetProperty("lon", out var lonEl) && lonEl.ValueKind == JsonValueKind.String)
            {
                var s = lonEl.GetString();
                if (!string.IsNullOrEmpty(s) && decimal.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var lo))
                    lon = lo;
            }
            return (lat, lon);
        }
        catch
        {
            return (null, null);
        }
    }
}
