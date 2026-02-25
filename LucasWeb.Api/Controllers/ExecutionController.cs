using LucasWeb.Api.Data;
using LucasWeb.Api.DTOs;
using LucasWeb.Api.Models;
using LucasWeb.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LucasWeb.Api.Controllers;

[ApiController]
[Route("api/execution")]
[Authorize]
public class ExecutionController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IServiceScopeFactory _scopeFactory;

    private static readonly string[] AllowedShiftNames = { "Mediodia", "Tarde", "Noche" };
    private static readonly string[] Q1Allowed = { "Pocas mesas", "Media sala", "Sala completa", "Sala y terraza completas", "Sala y terraza completas y doblamos mesas" };
    private static readonly string[] Q2Allowed = { "Muy espaciadas, sin acumulación", "Entradas tranquilas", "Flujo constante", "Muchas entradas juntas", "Entradas continuas sin margen" };
    private static readonly string[] Q3Allowed = { "Siempre adelantado", "Generalmente con margen", "Justo", "Poco margen", "Ningún margen" };
    private static readonly string[] Q4Allowed = { "Muy fácil", "Fácil", "Normal", "Difícil", "Muy difícil" };

    /// <summary>Porcentaje por defecto de descuento en facturación manual (configurable en Settings → DescuentoFacturacionManualPorcentaje).</summary>
    private const decimal DefaultManualRevenueDiscountPercent = 9.1m;

    private async Task<decimal> GetManualRevenueDiscountPercentAsync()
    {
        var value = await _db.Settings.AsNoTracking()
            .Where(s => s.Key == "DescuentoFacturacionManualPorcentaje")
            .Select(s => s.Value)
            .FirstOrDefaultAsync();
        if (string.IsNullOrWhiteSpace(value) || !decimal.TryParse(value.Replace(",", "."), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var pct))
            return DefaultManualRevenueDiscountPercent;
        return Math.Clamp(pct, 0m, 100m);
    }

    private static decimal ApplyManualRevenueDiscount(decimal revenue, decimal percent)
    {
        if (revenue <= 0 || percent <= 0) return revenue;
        return Math.Round(revenue * (100m - percent) / 100m, 2);
    }

    private static bool IsAllowed(string? value, string[] allowed) =>
        string.IsNullOrWhiteSpace(value) || allowed.Contains(value.Trim());

    private static string? ValidateShiftFeedback(ShiftDto s)
    {
        if (string.IsNullOrWhiteSpace(s.ShiftName)) return "Turno: shift_name requerido";
        if (!AllowedShiftNames.Contains(s.ShiftName.Trim(), StringComparer.OrdinalIgnoreCase))
            return "Turno: shift_name no permitido (use Mediodia, Tarde o Noche)";
        if (!IsAllowed(s.FeedbackQ1, Q1Allowed)) return "Q1 (Volumen): valor no permitido";
        if (!IsAllowed(s.FeedbackQ2, Q2Allowed)) return "Q2 (Ritmo): valor no permitido";
        if (!IsAllowed(s.FeedbackQ3, Q3Allowed)) return "Q3 (Margen): valor no permitido";
        if (!IsAllowed(s.FeedbackQ4, Q4Allowed)) return "Q4 (Dificultad): valor no permitido";
        if (!IsAllowed(s.FeedbackQ5, Q4Allowed)) return "Q5 (Dificultad cocina): valor no permitido";
        return null;
    }

    private static bool HasAnyFeedbackOrStaff(ShiftDto s) =>
        (s.StaffFloor + s.StaffKitchen) > 0 ||
        !string.IsNullOrWhiteSpace(s.FeedbackQ1) ||
        !string.IsNullOrWhiteSpace(s.FeedbackQ2) ||
        !string.IsNullOrWhiteSpace(s.FeedbackQ3) ||
        !string.IsNullOrWhiteSpace(s.FeedbackQ4) ||
        !string.IsNullOrWhiteSpace(s.FeedbackQ5);

    /// <summary>Rellena el clima del día desde los turnos del request para que el Sheet reciba clima al sincronizar (Feedback diario no muestra clima en UI pero sí se exporta).</summary>
    private static void ApplyDayWeatherFromShifts(ExecutionDay day, List<ShiftDto> shiftsReq)
    {
        var withCode = shiftsReq.FirstOrDefault(s => s.WeatherCode.HasValue);
        if (withCode != null)
            day.WeatherCode = withCode.WeatherCode;

        var temps = shiftsReq.Where(s => s.WeatherTempAvg.HasValue).Select(s => s.WeatherTempAvg!.Value).ToList();
        if (temps.Count > 0)
        {
            day.WeatherTemp = temps.First();
            day.WeatherTempMax = temps.Max();
            day.WeatherTempMin = temps.Min();
        }

        var precip = shiftsReq.FirstOrDefault(s => s.WeatherPrecipMm.HasValue);
        if (precip != null)
            day.WeatherPrecipMm = precip.WeatherPrecipMm;

        var winds = shiftsReq.Where(s => s.WeatherWindMaxKmh.HasValue).Select(s => s.WeatherWindMaxKmh!.Value).ToList();
        if (winds.Count > 0)
            day.WeatherWindMaxKmh = winds.Max();
    }

    private static (decimal revenue, decimal hours, int staffTotal, bool hasAnyRevenueOrHours, bool hasAnyFeedbackOrStaff) ComputeFromShifts(IEnumerable<ShiftDto> shifts)
    {
        decimal rev = 0;
        decimal hrs = 0;
        int staff = 0;
        bool anyNum = false;
        bool anyFeedbackOrStaff = false;
        foreach (var s in shifts)
        {
            rev += s.Revenue;
            hrs += s.HoursWorked;
            staff += s.StaffFloor + s.StaffKitchen;
            if (s.Revenue > 0 || s.HoursWorked > 0) anyNum = true;
            if (HasAnyFeedbackOrStaff(s)) anyFeedbackOrStaff = true;
        }
        return (rev, hrs, staff, anyNum, anyFeedbackOrStaff);
    }

    public ExecutionController(AppDbContext db, IServiceScopeFactory scopeFactory)
    {
        _db = db;
        _scopeFactory = scopeFactory;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<object>>> List([FromQuery] int days = 30)
    {
        if (days > 365) days = 365;
        var from = DateTime.UtcNow.Date.AddDays(-days);
        var list = await _db.ExecutionDays
            .Where(e => e.Date >= from)
            .OrderByDescending(e => e.Date)
            .Select(e => new { id = e.Id, date = e.Date.ToString("yyyy-MM-dd"), total_revenue = e.TotalRevenue, total_hours_worked = e.TotalHoursWorked, staff_total = e.StaffTotal })
            .ToListAsync();
        return Ok(list);
    }

    [HttpGet("{date}")]
    public async Task<ActionResult<ExecutionDayResponse>> GetByDate(string date)
    {
        if (!DateTime.TryParse(date, out var d))
            return BadRequest(new { message = "Fecha inválida (yyyy-MM-dd)" });
        var day = await _db.ExecutionDays
            .Include(e => e.ShiftFeedbacks.OrderBy(s => s.ShiftName))
            .FirstOrDefaultAsync(e => e.Date == d.Date);
        if (day == null)
            return NotFound();
        return Ok(ToResponse(day));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateExecutionRequest request)
    {
        if (string.IsNullOrEmpty(request.Date) || !DateTime.TryParse(request.Date, out var d))
            return BadRequest(new { message = "Fecha inválida (yyyy-MM-dd)" });
        if (request.TotalRevenue < 0 || request.TotalHoursWorked < 0)
            return BadRequest(new { message = "Facturación y horas deben ser >= 0" });

        var date = d.Date;
        if (await _db.ExecutionDays.AnyAsync(e => e.Date == date))
            return Conflict(new { message = "El día ya existe" });

        var shiftsReq = request.Shifts ?? new List<ShiftDto>();
        // Validación de duplicados por shift_name
        var dup = shiftsReq
            .Where(s => !string.IsNullOrWhiteSpace(s.ShiftName))
            .GroupBy(s => s.ShiftName.Trim().ToLowerInvariant())
            .FirstOrDefault(g => g.Count() > 1);
        if (dup != null) return BadRequest(new { message = "Turnos duplicados: " + dup.Key });

        foreach (var s in shiftsReq)
        {
            var err = ValidateShiftFeedback(s);
            if (err != null) return BadRequest(new { message = err });
        }

        var discountPercent = await GetManualRevenueDiscountPercentAsync();
        var computed = ComputeFromShifts(shiftsReq);
        var staffTotal = shiftsReq.Count > 0 ? computed.staffTotal : request.StaffTotal;
        var totalRevenue = (shiftsReq.Count > 0 && computed.hasAnyRevenueOrHours) ? computed.revenue : request.TotalRevenue;
        totalRevenue = ApplyManualRevenueDiscount(totalRevenue, discountPercent);
        var totalHours = (shiftsReq.Count > 0 && computed.hasAnyRevenueOrHours) ? computed.hours : request.TotalHoursWorked;
        var isFeedbackOnly = totalRevenue == 0 && totalHours == 0 && shiftsReq.Count > 0 && computed.hasAnyFeedbackOrStaff;
        var day = new ExecutionDay
        {
            Id = Guid.NewGuid(),
            Date = date,
            TotalRevenue = totalRevenue,
            TotalHoursWorked = totalHours,
            StaffTotal = staffTotal,
            Notes = request.Notes,
            IsFeedbackOnly = isFeedbackOnly,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _db.ExecutionDays.Add(day);
        if (shiftsReq.Count > 0)
        {
            foreach (var s in shiftsReq)
            {
                _db.ShiftFeedbacks.Add(ToShift(day.Id, s, ApplyManualRevenueDiscount(s.Revenue, discountPercent)));
            }
        }
        await _db.SaveChangesAsync();
        var savedDate = date;
        _ = Task.Run(async () =>
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var sync = scope.ServiceProvider.GetRequiredService<IGoogleSheetSyncService>();
                var scopeDb = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var dayWithShifts = await scopeDb.ExecutionDays.Include(e => e.ShiftFeedbacks).FirstOrDefaultAsync(e => e.Date == savedDate);
                if (dayWithShifts != null) await sync.SyncDayAsync(dayWithShifts);
            }
            catch { /* sync opcional, no propagar */ }
        });
        return CreatedAtAction(nameof(GetByDate), new { date = date.ToString("yyyy-MM-dd") }, ToResponse(await LoadDay(day.Id)));
    }

    [HttpPatch("{date}")]
    public async Task<ActionResult<ExecutionDayResponse>> Update(string date, [FromBody] UpdateExecutionRequest request)
    {
        if (!DateTime.TryParse(date, out var d))
            return BadRequest(new { message = "Fecha inválida (yyyy-MM-dd)" });
        if (request.TotalRevenue < 0 || request.TotalHoursWorked < 0)
            return BadRequest(new { message = "Facturación y horas deben ser >= 0" });
        var day = await _db.ExecutionDays.Include(e => e.ShiftFeedbacks).FirstOrDefaultAsync(e => e.Date == d.Date);
        if (day == null)
            return NotFound();
        var shiftsReq = request.Shifts ?? new List<ShiftDto>();
        var dup = shiftsReq
            .Where(s => !string.IsNullOrWhiteSpace(s.ShiftName))
            .GroupBy(s => s.ShiftName.Trim().ToLowerInvariant())
            .FirstOrDefault(g => g.Count() > 1);
        if (dup != null) return BadRequest(new { message = "Turnos duplicados: " + dup.Key });

        foreach (var s in shiftsReq)
        {
            var err = ValidateShiftFeedback(s);
            if (err != null) return BadRequest(new { message = err });
        }
        var discountPercent = await GetManualRevenueDiscountPercentAsync();
        if (shiftsReq.Count > 0)
        {
            var computed = ComputeFromShifts(shiftsReq);
            var rev = computed.hasAnyRevenueOrHours ? computed.revenue : request.TotalRevenue;
            day.TotalRevenue = ApplyManualRevenueDiscount(rev, discountPercent);
            day.TotalHoursWorked = computed.hasAnyRevenueOrHours ? computed.hours : request.TotalHoursWorked;
            day.StaffTotal = computed.staffTotal;
            day.IsFeedbackOnly = day.TotalRevenue == 0 && day.TotalHoursWorked == 0 && computed.hasAnyFeedbackOrStaff;
        }
        else
        {
            day.TotalRevenue = ApplyManualRevenueDiscount(request.TotalRevenue, discountPercent);
            day.TotalHoursWorked = request.TotalHoursWorked;
            day.StaffTotal = request.StaffTotal;
            // Si actualizas sin turnos, no tocamos IsFeedbackOnly (lo define el contenido real de turnos).
        }
        day.Notes = request.Notes;
        day.UpdatedAt = DateTime.UtcNow;

        if (shiftsReq.Count > 0)
        {
            _db.ShiftFeedbacks.RemoveRange(day.ShiftFeedbacks);
            foreach (var s in shiftsReq)
                _db.ShiftFeedbacks.Add(ToShift(day.Id, s, ApplyManualRevenueDiscount(s.Revenue, discountPercent)));
            // Propagar clima de los turnos al día para que el Sheet tenga clima al sincronizar (la columna Clima usa nivel día).
            ApplyDayWeatherFromShifts(day, shiftsReq);
        }
        await _db.SaveChangesAsync();
        var updatedDate = day.Date;
        _ = Task.Run(async () =>
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var sync = scope.ServiceProvider.GetRequiredService<IGoogleSheetSyncService>();
                var scopeDb = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var dayWithShifts = await scopeDb.ExecutionDays.Include(e => e.ShiftFeedbacks).FirstOrDefaultAsync(e => e.Date == updatedDate);
                if (dayWithShifts != null) await sync.SyncDayAsync(dayWithShifts);
            }
            catch { /* sync opcional, no propagar */ }
        });
        return Ok(ToResponse(await LoadDay(day.Id)));
    }

    /// <summary>
    /// Corrige TotalRevenue guardados con formato incorrecto (ej. 1,60635 en lugar de 1606,35).
    /// Solo corrige valores en (0, 100) con decimales cuyo valor * 1000 esté en [100, 100000].
    /// </summary>
    [HttpPost("fix-revenue-format")]
    public async Task<ActionResult<object>> FixRevenueFormat()
    {
        try
        {
            var allInRange = await _db.ExecutionDays
                .Where(e => e.TotalRevenue > 0 && e.TotalRevenue < 100)
                .ToListAsync();
            var days = allInRange.Where(e => (e.TotalRevenue % 1) != 0).ToList();
            var corrected = new List<object>();
            foreach (var day in days)
            {
                var before = day.TotalRevenue;
                var correctedValue = before * 1000;
                if (correctedValue >= 100 && correctedValue <= 100000)
                {
                    day.TotalRevenue = Math.Round(correctedValue, 2);
                    day.UpdatedAt = DateTime.UtcNow;
                    corrected.Add(new { date = day.Date.ToString("yyyy-MM-dd"), before, after = day.TotalRevenue });
                }
            }
            if (corrected.Count > 0)
                await _db.SaveChangesAsync();
            return Ok(new { updated = corrected.Count, items = corrected });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "Error al corregir: " + (ex.Message ?? "error interno") });
        }
    }

    private async Task<ExecutionDay> LoadDay(Guid id) =>
        (await _db.ExecutionDays.Include(e => e.ShiftFeedbacks.OrderBy(s => s.ShiftName)).FirstAsync(e => e.Id == id))!;

    private static ExecutionDayResponse ToResponse(ExecutionDay day) => new()
    {
        Id = day.Id.ToString(),
        Date = day.Date.ToString("yyyy-MM-dd"),
        TotalRevenue = day.TotalRevenue,
        TotalHoursWorked = day.TotalHoursWorked,
        StaffTotal = day.StaffTotal,
        Notes = day.Notes,
        IsFeedbackOnly = day.IsFeedbackOnly,
        WeatherCode = day.WeatherCode,
        WeatherTempMax = day.WeatherTempMax,
        WeatherTempMin = day.WeatherTempMin,
        WeatherPrecipMm = day.WeatherPrecipMm,
        WeatherWindMaxKmh = day.WeatherWindMaxKmh,
        Shifts = day.ShiftFeedbacks.Select(s =>
        {
            decimal? hoursSala = null;
            decimal? hoursCocina = null;
            var total = s.StaffFloor + s.StaffKitchen;
            if (s.HoursWorked > 0 && total > 0)
            {
                hoursSala = Math.Round(s.HoursWorked * s.StaffFloor / total, 2);
                hoursCocina = Math.Round(s.HoursWorked * s.StaffKitchen / total, 2);
            }
            return new ShiftDto
            {
                ShiftName = s.ShiftName,
                Revenue = s.Revenue,
                HoursWorked = s.HoursWorked,
                PlannedHours = s.PlannedHours,
                StaffFloor = s.StaffFloor,
                StaffKitchen = s.StaffKitchen,
                HoursSalaEstimated = hoursSala,
                HoursCocinaEstimated = hoursCocina,
                FeedbackQ1 = s.FeedbackQ1,
                FeedbackQ2 = s.FeedbackQ2,
                FeedbackQ3 = s.FeedbackQ3,
                FeedbackQ4 = s.FeedbackQ4,
                FeedbackQ5 = s.FeedbackQ5,
                RevenuePerWaiterSala = s.RevenuePerWaiterSala,
                DifficultyScore = s.DifficultyScore,
                ComfortLevel = s.ComfortLevel,
                RevenuePerWaiterCocina = s.RevenuePerWaiterCocina,
                DifficultyScoreKitchen = s.DifficultyScoreKitchen,
                ComfortLevelKitchen = s.ComfortLevelKitchen,
                RecordedBy = s.RecordedBy,
                EditedBy = s.EditedBy,
                WeatherCode = s.WeatherCode,
                WeatherTempAvg = s.WeatherTempAvg,
                WeatherPrecipMm = s.WeatherPrecipMm,
                WeatherWindMaxKmh = s.WeatherWindMaxKmh
            };
        }).ToList()
    };

    private static ShiftFeedback ToShift(Guid executionDayId, ShiftDto dto, decimal? revenueOverride = null)
    {
        var revenue = revenueOverride ?? dto.Revenue;
        var staffFloor = Math.Clamp(dto.StaffFloor, 0, 99);
        var staffKitchen = Math.Clamp(dto.StaffKitchen, 0, 99);
        var revenuePerWaiterSala = staffFloor > 0 && revenue > 0
            ? Math.Round(revenue / staffFloor, 2)
            : (decimal?)null;
        var revenuePerWaiterCocina = staffKitchen > 0 && revenue > 0
            ? Math.Round(revenue / staffKitchen, 2)
            : (decimal?)null;
        var difficultyScore = FeedbackScoring.ComputeDifficultyScore(dto.FeedbackQ1, dto.FeedbackQ2, dto.FeedbackQ3, dto.FeedbackQ4);
        var comfortLevel = FeedbackScoring.GetComfortLevel(difficultyScore);
        var difficultyScoreKitchen = FeedbackScoring.ComputeDifficultyScoreKitchen(dto.FeedbackQ5);
        var comfortLevelKitchen = FeedbackScoring.GetComfortLevel(difficultyScoreKitchen);
        return new ShiftFeedback
        {
            Id = Guid.NewGuid(),
            ExecutionDayId = executionDayId,
            ShiftName = dto.ShiftName,
            Revenue = revenue,
            HoursWorked = dto.HoursWorked,
            PlannedHours = dto.PlannedHours,
            StaffFloor = staffFloor,
            StaffKitchen = staffKitchen,
            FeedbackQ1 = dto.FeedbackQ1,
            FeedbackQ2 = dto.FeedbackQ2,
            FeedbackQ3 = dto.FeedbackQ3,
            FeedbackQ4 = dto.FeedbackQ4,
            FeedbackQ5 = dto.FeedbackQ5,
            RevenuePerWaiterSala = revenuePerWaiterSala,
            DifficultyScore = difficultyScore,
            ComfortLevel = comfortLevel,
            RevenuePerWaiterCocina = revenuePerWaiterCocina,
            DifficultyScoreKitchen = difficultyScoreKitchen,
            ComfortLevelKitchen = comfortLevelKitchen,
            RecordedBy = dto.RecordedBy,
            EditedBy = dto.EditedBy,
            CreatedAt = DateTime.UtcNow,
            WeatherCode = dto.WeatherCode,
            WeatherTempAvg = dto.WeatherTempAvg,
            WeatherPrecipMm = dto.WeatherPrecipMm,
            WeatherWindMaxKmh = dto.WeatherWindMaxKmh
        };
    }
}
