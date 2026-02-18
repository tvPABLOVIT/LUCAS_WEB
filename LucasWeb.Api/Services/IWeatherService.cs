namespace LucasWeb.Api.Services;

/// <summary>Información de clima por día (Open-Meteo).</summary>
public interface IWeatherService
{
    /// <summary>Obtiene clima para un rango de fechas. Lat/lon null → sin datos.</summary>
    Task<IReadOnlyList<WeatherDayInfo>> GetWeatherForRangeAsync(DateTime startDate, DateTime endDate, decimal? latitude, decimal? longitude);

    /// <summary>Obtiene clima horario para un rango de fechas (local time en timezone solicitado).</summary>
    Task<IReadOnlyList<WeatherHourInfo>> GetHourlyWeatherForRangeAsync(DateTime startDate, DateTime endDate, decimal? latitude, decimal? longitude);

    /// <summary>
    /// Agrega el clima horario a ventanas de turno (Mediodia/Tarde/Noche) por cada fecha del rango.
    /// </summary>
    Task<IReadOnlyList<WeatherShiftInfo>> GetShiftWeatherForRangeAsync(DateTime startDate, DateTime endDate, decimal? latitude, decimal? longitude);
}

public class WeatherDayInfo
{
    public DateTime Date { get; set; }
    /// <summary>Código WMO (0=despejado, 51-67/80-82=lluvia).</summary>
    public int WeatherCode { get; set; }
    public string Description { get; set; } = "";
    public decimal? TempMax { get; set; }
    public decimal? TempMin { get; set; }
    public decimal? PrecipitationSumMm { get; set; }
    public decimal? WindSpeedMaxKmh { get; set; }
    public bool IsRainy => WeatherCode is >= 51 and <= 67 or >= 80 and <= 82;
}

public class WeatherHourInfo
{
    /// <summary>Hora local (timezone devuelto por la API).</summary>
    public DateTime Time { get; set; }
    public int WeatherCode { get; set; }
    public decimal? Temperature2m { get; set; }
    public decimal? PrecipitationMm { get; set; }
    public decimal? WindSpeed10mKmh { get; set; }
}

public class WeatherShiftInfo
{
    public DateTime Date { get; set; }
    public string ShiftName { get; set; } = ""; // Mediodia, Tarde, Noche
    public int WeatherCode { get; set; }
    public string Description { get; set; } = "";
    public decimal? TempAvg { get; set; }
    public decimal? PrecipMm { get; set; }
    public decimal? WindMaxKmh { get; set; }

    public bool IsRainy => WeatherCode is >= 51 and <= 67 or >= 80 and <= 82;
}
