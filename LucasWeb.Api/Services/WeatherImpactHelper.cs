namespace LucasWeb.Api.Services;

/// <summary>Umbrales y criterios compartidos para impacto del clima (API weather-impact y DetectedPatternsService).</summary>
public static class WeatherImpactHelper
{
    public const decimal DefaultRainyPrecipMm = 0.5m;
    public const decimal DefaultHeavyRainMm = 5m;
    public const decimal DefaultWindyKmh = 35m;
    public const decimal DefaultColdC = 5m;
    public const decimal DefaultHotC = 30m;

    /// <summary>Códigos WMO de lluvia (51-67, 71-77, 80-82, 95, 96).</summary>
    public static bool IsRainCode(int code) =>
        code is >= 51 and <= 67 or >= 71 and <= 77 or >= 80 and <= 82 or 95 or 96;
}
