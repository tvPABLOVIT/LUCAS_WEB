using System.Text.Json.Serialization;

namespace LucasWeb.Api.DTOs;

/// <summary>Agregado por esquema de personal (sala-cocina) y banda de facturación por camarero.</summary>
public class StaffRevenueComfortResult
{
    [JsonPropertyName("schemas")]
    public List<StaffRevenueComfortSchemaDto> Schemas { get; set; } = new();

    /// <summary>Agregados por número de cocineros (1, 2, 3…) y banda de facturación por cocinero.</summary>
    [JsonPropertyName("cocina_schemas")]
    public List<StaffRevenueComfortSchemaDto> CocinaSchemas { get; set; } = new();

    [JsonPropertyName("bands")]
    public List<StaffRevenueComfortBandDto> BandsDefinition { get; set; } = new();
}

public class StaffRevenueComfortSchemaDto
{
    [JsonPropertyName("schema")]
    public string Schema { get; set; } = ""; // "1-1", "2-1", etc.

    [JsonPropertyName("bands")]
    public List<StaffRevenueComfortBandItemDto> Bands { get; set; } = new();

    /// <summary>Revenue por camarero hasta el cual la dificultad media suele estar por debajo del umbral (ej. 3.5).</summary>
    [JsonPropertyName("comfort_limit_approx")]
    public decimal? ComfortLimitApprox { get; set; }
}

public class StaffRevenueComfortBandItemDto
{
    [JsonPropertyName("min")]
    public decimal Min { get; set; }

    [JsonPropertyName("max")]
    public decimal Max { get; set; }

    [JsonPropertyName("count")]
    public int Count { get; set; }

    [JsonPropertyName("avg_difficulty")]
    public decimal? AvgDifficulty { get; set; }

    [JsonPropertyName("pct_difficult")]
    public decimal? PctDifficult { get; set; } // % turnos con DifficultyScore >= 4
}

/// <summary>Definición de bandas (solo informativo).</summary>
public class StaffRevenueComfortBandDto
{
    [JsonPropertyName("min")]
    public decimal Min { get; set; }

    [JsonPropertyName("max")]
    public decimal Max { get; set; }
}
