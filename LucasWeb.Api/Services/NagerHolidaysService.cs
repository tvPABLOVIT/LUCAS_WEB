using System.Globalization;
using System.Text.Json;

namespace LucasWeb.Api.Services;

/// <summary>Festivos vía Nager.Date API (sin API key).</summary>
public class NagerHolidaysService : IHolidaysService
{
    private readonly HttpClient _http;
    private const string BaseUrl = "https://date.nager.at/api/v3/PublicHolidays";

    public NagerHolidaysService(IHttpClientFactory factory)
    {
        _http = factory.CreateClient();
        _http.Timeout = TimeSpan.FromSeconds(8);
    }

    public async Task<IReadOnlyList<HolidayInfo>> GetHolidaysInRangeAsync(DateTime startDate, DateTime endDate, string? countryCode)
    {
        if (string.IsNullOrWhiteSpace(countryCode) || countryCode.Length < 2) return Array.Empty<HolidayInfo>();
        var cc = countryCode.Length == 2 ? countryCode.ToUpperInvariant() : countryCode[..2].ToUpperInvariant();
        var year = startDate.Year;
        if (endDate.Year > year) year = endDate.Year;
        var url = $"{BaseUrl}/{year}/{cc}";
        try
        {
            var response = await _http.GetAsync(url);
            response.EnsureSuccessStatusCode();
            var json = await response.Content.ReadAsStringAsync();
            var list = new List<HolidayInfo>();
            using var doc = JsonDocument.Parse(json);
            foreach (var el in doc.RootElement.EnumerateArray())
            {
                if (!el.TryGetProperty("date", out var dateEl)) continue;
                var dateStr = dateEl.GetString();
                if (string.IsNullOrEmpty(dateStr) || !DateTime.TryParse(dateStr, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)) continue;
                var d = date.Date;
                if (d < startDate.Date || d > endDate.Date) continue;
                var name = el.TryGetProperty("localName", out var ln) ? ln.GetString() : el.TryGetProperty("name", out var n) ? n.GetString() : "";
                list.Add(new HolidayInfo { Date = d, Name = name ?? "" });
            }
            return list;
        }
        catch
        {
            return Array.Empty<HolidayInfo>();
        }
    }
}
