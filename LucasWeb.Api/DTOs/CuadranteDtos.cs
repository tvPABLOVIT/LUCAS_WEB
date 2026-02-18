using System.Text.Json.Serialization;

namespace LucasWeb.Api.DTOs;

/// <summary>Formato de salida del parser Python (LucasCuadranteParser). Un día con turnos.</summary>
public class CuadranteDayDto
{
    [JsonPropertyName("date")]
    public string Date { get; set; } = ""; // yyyy-MM-dd

    [JsonPropertyName("total_revenue")]
    public decimal TotalRevenue { get; set; }

    [JsonPropertyName("total_hours_worked")]
    public decimal TotalHoursWorked { get; set; }

    [JsonPropertyName("shifts")]
    public List<CuadranteShiftDto> Shifts { get; set; } = new();
}

/// <summary>Turno dentro de un día del cuadrante (horas programadas + personal).</summary>
public class CuadranteShiftDto
{
    [JsonPropertyName("shift_name")]
    public string ShiftName { get; set; } = "";

    [JsonPropertyName("staff_floor")]
    public int StaffFloor { get; set; }

    [JsonPropertyName("staff_kitchen")]
    public int StaffKitchen { get; set; }

    [JsonPropertyName("hours_worked")]
    public decimal HoursWorked { get; set; }
}
