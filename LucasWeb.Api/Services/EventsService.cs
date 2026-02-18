using LucasWeb.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace LucasWeb.Api.Services;

/// <summary>Eventos desde BD; obras desde Open Data BCN (stub por ahora).</summary>
public class EventsService : IEventsService
{
    private readonly AppDbContext _db;

    public EventsService(AppDbContext db) => _db = db;

    public async Task<IReadOnlyList<EventItem>> GetEventsInRangeAsync(DateTime start, DateTime end)
    {
        var list = await _db.Events
            .AsNoTracking()
            .Where(e => e.Date >= start.Date && e.Date <= end.Date)
            .OrderBy(e => e.Date)
            .Select(e => new EventItem
            {
                Date = e.Date,
                Name = e.Name,
                Impact = e.Impact,
                Source = e.Source
            })
            .ToListAsync();
        return list;
    }

    public Task<IReadOnlyList<WorkItem>> GetWorksNearbyAsync(decimal? lat, decimal? lon, double radiusMeters = 300)
    {
        if (!lat.HasValue || !lon.HasValue) return Task.FromResult<IReadOnlyList<WorkItem>>(Array.Empty<WorkItem>());
        // Open Data BCN: opcional. Por ahora devolvemos vacío; se puede implementar con su API de obres.
        return Task.FromResult<IReadOnlyList<WorkItem>>(Array.Empty<WorkItem>());
    }
}
