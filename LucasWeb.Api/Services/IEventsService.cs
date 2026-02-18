namespace LucasWeb.Api.Services;

/// <summary>Eventos y obras que pueden afectar demanda (tabla Events + Open Data BCN opcional).</summary>
public interface IEventsService
{
    Task<IReadOnlyList<EventItem>> GetEventsInRangeAsync(DateTime start, DateTime end);
    Task<IReadOnlyList<WorkItem>> GetWorksNearbyAsync(decimal? lat, decimal? lon, double radiusMeters = 300);
}

public class EventItem
{
    public DateTime Date { get; set; }
    public string Name { get; set; } = "";
    public string? Impact { get; set; }
    public string? Source { get; set; }
}

public class WorkItem
{
    public string Description { get; set; } = "";
    public string? Address { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
}
