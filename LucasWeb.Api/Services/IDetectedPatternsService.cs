namespace LucasWeb.Api.Services;

/// <summary>Patrones aprendidos: impacto lluvia, festivos, temperatura.</summary>
public interface IDetectedPatternsService
{
    Task<PatternData?> GetPatternAsync(string type, string? key = null);
    Task SavePatternAsync(string type, string? key, string jsonData, decimal confidence);
    /// <summary>Calcula patrones desde histórico (ExecutionDays con WeatherCode, IsHoliday, WeatherTemp) y los guarda.</summary>
    Task ComputeAndSavePatternsAsync();
}

public class PatternData
{
    public string Type { get; set; } = "";
    public string? Key { get; set; }
    public string? JsonData { get; set; }
    public decimal Confidence { get; set; }
}
