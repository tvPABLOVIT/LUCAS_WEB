using LucasWeb.Api.Data;
using LucasWeb.Api.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LucasWeb.Api.Controllers;

[ApiController]
[Route("api/recommendations")]
[Authorize]
public class RecommendationsController : ControllerBase
{
    private readonly AppDbContext _db;

    public RecommendationsController(AppDbContext db) => _db = db;

    [HttpGet]
    public async Task<ActionResult<IEnumerable<RecommendationDto>>> List([FromQuery] int limit = 20)
    {
        if (limit > 100) limit = 100;
        var list = await _db.Recommendations
            .Where(r => r.Status == "pending" || r.Status == "accepted" || r.Status == "applied")
            .OrderByDescending(r => r.CreatedAt)
            .Take(limit)
            .Select(r => new RecommendationDto
            {
                Id = r.Id.ToString(),
                Title = r.Title,
                Message = r.Message,
                Text = r.Message ?? r.Title,
                Status = r.Status,
                CreatedAt = r.CreatedAt
            })
            .ToListAsync();
        return Ok(list);
    }

    [HttpGet("version")]
    [AllowAnonymous]
    public async Task<IActionResult> GetVersion()
    {
        var execMax = await _db.ExecutionDays.MaxAsync(e => (DateTime?)e.UpdatedAt);
        var recMax = await _db.Recommendations.MaxAsync(r => (DateTime?)r.CreatedAt);
        var predMax = await _db.WeeklyPredictions.MaxAsync(p => (DateTime?)p.CreatedAt);
        var max = new[] { execMax, recMax, predMax }.Where(d => d.HasValue).Select(d => d!.Value.Ticks).DefaultIfEmpty(0).Max();
        return Ok(new RecommendationVersionResponse { Version = max });
    }

    [HttpPatch("{id}")]
    public async Task<IActionResult> UpdateStatus(Guid id, [FromBody] UpdateRecommendationRequest request)
    {
        var rec = await _db.Recommendations.FindAsync(id);
        if (rec == null) return NotFound();
        if (request.Status != null && new[] { "accepted", "applied", "discarded" }.Contains(request.Status))
        {
            rec.Status = request.Status;
            await _db.SaveChangesAsync();
        }
        return Ok();
    }
}

public class UpdateRecommendationRequest
{
    public string? Status { get; set; }
}
