namespace LucasWeb.Api.Services;

/// <summary>Festivos por país (Nager.Date).</summary>
public interface IHolidaysService
{
    /// <summary>Obtiene festivos para un rango de fechas. CountryCode null o vacío → sin datos.</summary>
    Task<IReadOnlyList<HolidayInfo>> GetHolidaysInRangeAsync(DateTime startDate, DateTime endDate, string? countryCode);
}

public class HolidayInfo
{
    public DateTime Date { get; set; }
    public string Name { get; set; } = "";
    public bool IsHoliday => !string.IsNullOrWhiteSpace(Name);
}
