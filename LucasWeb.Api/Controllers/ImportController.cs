using ClosedXML.Excel;
using LucasWeb.Api.Data;
using LucasWeb.Api.DTOs;
using LucasWeb.Api.Models;
using LucasWeb.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LucasWeb.Api.Controllers;

[ApiController]
[Route("api/import")]
[Authorize]
public class ImportController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IGoogleSheetSyncService _googleSheetSync;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ICuadrantePdfService _cuadrantePdf;

    public ImportController(AppDbContext db, IGoogleSheetSyncService googleSheetSync, IServiceScopeFactory scopeFactory, ICuadrantePdfService cuadrantePdf)
    {
        _db = db;
        _googleSheetSync = googleSheetSync;
        _scopeFactory = scopeFactory;
        _cuadrantePdf = cuadrantePdf;
    }

    /// <summary>
    /// Importa archivo de estimaciones (sN_AAAA). Solo Configuración. El nombre del archivo debe ser sN_AAAA (ej. s6_2026.xlsx).
    /// </summary>
    [HttpPost("estimacion-excel")]
    [RequestSizeLimit(10 * 1024 * 1024)]
    public async Task<ActionResult<ImportExcelResult>> ImportEstimacionExcel(IFormFile? file)
    {
        if (file == null || file.Length == 0)
            return BadRequest(new { message = "Suba un archivo Excel (.xlsx)." });

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (ext != ".xlsx" && ext != ".xls")
            return BadRequest(new { message = "Solo se admiten archivos Excel (.xlsx)." });

        var (isSnAaaa, weekNum, year) = ExcelImportService.TryParseEstimacionFileName(file.FileName);
        if (!isSnAaaa)
            return BadRequest(new { message = "Nombre de archivo no válido. Use formato sN_AAAA (ej. s6_2026)." });

        var errors = new List<string>();
        var result = new ImportExcelResult { Errors = errors };
        await using (var stream = file.OpenReadStream())
        {
            using var book = new XLWorkbook(stream);
            var ws = book.Worksheet(1);
            if (ws == null)
            {
                errors.Add("No se encontró ninguna hoja.");
                return Ok(result);
            }
            var days = ExcelImportService.ParseEstimacionSheet(ws, year, weekNum, errors);
            if (days.Count == 0)
            {
                result.Message = "No se pudieron leer fechas en C21:I21. La fila 21 debe contener fechas válidas y el archivo debe llamarse sN_AAAA (ej. s6_2026).";
                return Ok(result);
            }
            var (imported, updated, shiftsUpdated) = await PersistEstimacionDaysAsync(days);
            result.DaysCreated = imported;
            result.DaysUpdated = updated;
            result.ShiftsUpdated = shiftsUpdated;
            await _db.SaveChangesAsync();
            var estimacionDates = days.Select(d => d.TargetDate).ToList();
            await _googleSheetSync.SyncAsync(estimacionDates);
            result.Message = $"Estimaciones: {imported} importados, {updated} actualizados, {errors.Count} errores. Google Sheet actualizado.";
            if (errors.Count > 0)
                result.Message += " " + string.Join(" ", errors.Take(5));
            RunBackgroundAnalysisAsync(imported + updated);
        }
        return Ok(result);
    }

    /// <summary>
    /// Importa desde Excel (Dashboard). Los datos del Excel actualizan los cargados manualmente (sobrescriben); la facturación no se descuenta (el 9,1% solo se aplica al ingreso manual).
    /// Si el archivo se llama sN_AAAA → formato estimaciones. Si no, formato genérico: fila 1 cabecera, A=fecha, B=facturación, C=horas.
    /// </summary>
    [HttpPost("excel")]
    [RequestSizeLimit(10 * 1024 * 1024)]
    public async Task<ActionResult<ImportExcelResult>> ImportExcel(IFormFile? file, [FromQuery] string? weekStart)
    {
        if (file == null || file.Length == 0)
            return BadRequest(new { message = "Suba un archivo Excel (.xlsx)." });

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (ext != ".xlsx" && ext != ".xls")
            return BadRequest(new { message = "Solo se admiten archivos Excel (.xlsx)." });

        var errors = new List<string>();
        var result = new ImportExcelResult { Errors = errors };
        var imported = 0;
        var updated = 0;
        var shiftsUpdated = 0;

        await using (var stream = file.OpenReadStream())
        {
            using var book = new XLWorkbook(stream);
            var ws = book.Worksheet(1);
            if (ws == null)
            {
                errors.Add("No se encontró ninguna hoja.");
                return Ok(result);
            }

            if (ExcelImportService.TryParseEstimacionFileName(file.FileName).IsMatch)
            {
                var (_, weekNum, year) = ExcelImportService.TryParseEstimacionFileName(file.FileName);
                var days = ExcelImportService.ParseEstimacionSheet(ws, year, weekNum, errors);
                if (days.Count == 0)
                {
                    result.Message = "No se leyeron fechas en la fila 21. Use formato sN_AAAA (ej. s6_2026) y fechas válidas en C21:I21.";
                    return Ok(result);
                }
                (imported, updated, shiftsUpdated) = await PersistEstimacionDaysAsync(days);
                result.DaysCreated = imported;
                result.DaysUpdated = updated;
                result.ShiftsUpdated = shiftsUpdated;
                await _db.SaveChangesAsync();
                var estimacionDates = days.Select(d => d.TargetDate).ToList();
                await _googleSheetSync.SyncAsync(estimacionDates);
                result.Message = $"Estimaciones: {imported} importados, {updated} actualizados, {errors.Count} errores. Google Sheet actualizado.";
                RunBackgroundAnalysisAsync(imported + updated);
                return Ok(result);
            }

            if (ExcelImportService.LooksLikeEstimacionTemplate(ws))
            {
                var (isSnAaaa, weekNum, year) = ExcelImportService.TryParseEstimacionFileName(file.FileName);
                if (!isSnAaaa)
                {
                    return BadRequest(new { message = "Nombre de archivo no válido. Use formato sN_AAAA (ej. s6_2026)." });
                }
                var days = ExcelImportService.ParseEstimacionSheet(ws, year, weekNum, errors);
                if (days.Count == 0)
                {
                    result.Message = "No se leyeron fechas en la fila 21. La fila 21 debe contener fechas válidas y el archivo debe llamarse sN_AAAA (ej. s6_2026).";
                    return Ok(result);
                }
                (imported, updated, shiftsUpdated) = await PersistEstimacionDaysAsync(days);
                result.DaysCreated = imported;
                result.DaysUpdated = updated;
                result.ShiftsUpdated = shiftsUpdated;
                await _db.SaveChangesAsync();
                var estimacionDates = days.Select(d => d.TargetDate).ToList();
                await _googleSheetSync.SyncAsync(estimacionDates);
                result.Message = $"Estimaciones: {imported} importados, {updated} actualizados, {errors.Count} errores. Google Sheet actualizado.";
                if (errors.Count > 0)
                    result.Message += " " + string.Join(" ", errors.Take(5));
                RunBackgroundAnalysisAsync(imported + updated);
                return Ok(result);
            }

            // Los datos del Excel actualizan los cargados manualmente (sobrescriben). Facturación del Excel no se descuenta (ya viene aplicado el 9,1%).
            var genericRows = ExcelImportService.ParseGenericSheet(ws, errors);
            foreach (var row in genericRows)
            {
                var day = await _db.ExecutionDays.FirstOrDefaultAsync(e => e.Date == row.Date);
                if (day == null)
                {
                    _db.ExecutionDays.Add(new ExecutionDay
                    {
                        Id = Guid.NewGuid(),
                        Date = row.Date,
                        TotalRevenue = row.TotalRevenue,
                        TotalHoursWorked = row.TotalHoursWorked,
                        StaffTotal = 0,
                        IsFeedbackOnly = false,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    });
                    imported++;
                }
                else
                {
                    // Actualizar día existente (p. ej. datos manuales): Excel sobrescribe; no aplicar descuento 9,1%.
                    day.TotalRevenue = row.TotalRevenue;
                    day.TotalHoursWorked = row.TotalHoursWorked;
                    if (row.TotalRevenue > 0 || row.TotalHoursWorked > 0) day.IsFeedbackOnly = false;
                    day.UpdatedAt = DateTime.UtcNow;
                    updated++;
                }
            }

            await _db.SaveChangesAsync();
            result.DaysCreated = imported;
            result.DaysUpdated = updated;
            result.Message = $"Importados: {imported}, Actualizados: {updated}, Errores: {errors.Count}";
            if (errors.Count > 0)
                result.Message += ". Primeros 5 errores: " + string.Join("; ", errors.Take(5));
            RunBackgroundAnalysisAsync(imported + updated);
        }

        return Ok(result);
    }

    private class EmpleadoSetting
    {
        public string Name { get; set; } = "";
        public int Hours { get; set; }
        public string Position { get; set; } = "";
    }

    /// <summary>
    /// Importa PDF de cuadrante BETLEM (horarios programados). Actualiza personal (sala/cocina) y horas programadas por turno (PlannedHours).
    /// Las horas reales y la facturación siguen viniendo del Excel.
    /// </summary>
    [HttpPost("cuadrante-pdf")]
    [RequestSizeLimit(10 * 1024 * 1024)]
    public async Task<ActionResult<ImportExcelResult>> ImportCuadrantePdf(IFormFile? file, [FromQuery] string? weekStart)
    {
        if (file == null || file.Length == 0)
            return BadRequest(new { message = "Suba un archivo PDF." });

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (ext != ".pdf")
            return BadRequest(new { message = "Solo se admiten archivos PDF." });

        var errors = new List<string>();
        var result = new ImportExcelResult { Errors = errors };
        var daysCreated = 0;
        var daysUpdated = 0;
        var shiftsUpdated = 0;

        try
        {
            await using var stream = file.OpenReadStream();
            var days = await _cuadrantePdf.ParsePdfAsync(stream);

            if (days.Count == 0)
            {
                result.Message = "El PDF no contiene días reconocidos.";
                return Ok(result);
            }

            foreach (var d in days)
            {
                if (!DateTime.TryParse(d.Date, out var date))
                {
                    errors.Add($"Fecha no válida: {d.Date}");
                    continue;
                }
                date = date.Date;

                var day = await _db.ExecutionDays.Include(e => e.ShiftFeedbacks).FirstOrDefaultAsync(e => e.Date == date);
                if (day == null)
                {
                    day = new ExecutionDay
                    {
                        Id = Guid.NewGuid(),
                        Date = date,
                        TotalRevenue = 0,
                        TotalHoursWorked = 0,
                        StaffTotal = 0,
                        IsFeedbackOnly = true,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    };
                    _db.ExecutionDays.Add(day);
                    daysCreated++;
                }
                else
                {
                    daysUpdated++;
                }

                // Horas totales del día según el PDF (suma columna Total por empleado = recuadro rojo)
                day.PlannedHoursTotal = d.TotalHoursWorked;

                foreach (var s in d.Shifts)
                {
                    var shift = day.ShiftFeedbacks.FirstOrDefault(x => string.Equals(x.ShiftName, s.ShiftName, StringComparison.OrdinalIgnoreCase));
                    if (shift == null)
                    {
                        day.ShiftFeedbacks.Add(new ShiftFeedback
                        {
                            Id = Guid.NewGuid(),
                            ExecutionDayId = day.Id,
                            ShiftName = s.ShiftName,
                            Revenue = 0,
                            HoursWorked = 0,
                            PlannedHours = s.HoursWorked,
                            StaffFloor = s.StaffFloor,
                            StaffKitchen = s.StaffKitchen,
                            CreatedAt = DateTime.UtcNow
                        });
                        shiftsUpdated++;
                    }
                    else
                    {
                        shift.StaffFloor = s.StaffFloor;
                        shift.StaffKitchen = s.StaffKitchen;
                        shift.PlannedHours = s.HoursWorked;
                        shiftsUpdated++;
                    }
                }
            }

            await _db.SaveChangesAsync();
            result.DaysCreated = daysCreated;
            result.DaysUpdated = daysUpdated;
            result.ShiftsUpdated = shiftsUpdated;
            result.Message = $"Cuadrante PDF: {daysCreated} días creados, {daysUpdated} actualizados, {shiftsUpdated} turnos (horas programadas y personal). Las horas reales se cargan con el Excel.";
            if (errors.Count > 0)
                result.Message += " Errores: " + string.Join("; ", errors.Take(3));
            RunBackgroundAnalysisAsync(daysCreated + daysUpdated);
        }
        catch (InvalidOperationException ex)
        {
            errors.Add(ex.Message);
            result.Message = "Error al procesar el PDF: " + ex.Message;
            return Ok(result);
        }
        catch (Exception ex)
        {
            errors.Add(ex.Message);
            result.Message = "Error inesperado: " + ex.Message;
            return Ok(result);
        }

        return Ok(result);
    }

    /// <summary>Exportar todo: envía todos los días guardados (ExecutionDays) al Google Sheet configurado (como en doc Windows).</summary>
    [HttpPost("sheets-export-all")]
    public async Task<IActionResult> ExportAllToGoogleSheet()
    {
        var dates = await _db.ExecutionDays.AsNoTracking().OrderBy(e => e.Date).Select(e => e.Date).ToListAsync();
        if (dates.Count == 0)
            return Ok(new { message = "No hay días guardados para exportar." });
        await _googleSheetSync.SyncAsync(dates);
        return Ok(new { message = "Exportado al Google Sheet.", count = dates.Count });
    }

    /// <summary>Ejecuta en segundo plano evaluación de predicciones y análisis de patrones (como en doc Windows: RunFullBackgroundAnalysisAsync).</summary>
    private void RunBackgroundAnalysisAsync(int touchedCount)
    {
        if (touchedCount <= 0) return;
        _ = Task.Run(async () =>
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var evaluate = scope.ServiceProvider.GetRequiredService<IEvaluatePredictionsService>();
                var patterns = scope.ServiceProvider.GetRequiredService<IDetectedPatternsService>();
                await evaluate.EvaluateLastWeekIfPendingAsync();
                await patterns.ComputeAndSavePatternsAsync();
            }
            catch
            {
                // No propagar para no afectar la respuesta al usuario
            }
        });
    }

    private async Task<(int Imported, int Updated, int ShiftsUpdated)> PersistEstimacionDaysAsync(List<EstimacionDayData> days)
    {
        var imported = 0;
        var updated = 0;
        var shiftsUpdated = 0;
        foreach (var d in days)
        {
            var total = d.RevMed + d.RevTar + d.RevNoc;
            var totalHours = d.HoursMed + d.HoursTar + d.HoursNoc;
            var day = await _db.ExecutionDays.Include(e => e.ShiftFeedbacks).FirstOrDefaultAsync(e => e.Date == d.TargetDate);
            if (day == null)
            {
                day = new ExecutionDay
                {
                    Id = Guid.NewGuid(),
                    Date = d.TargetDate,
                    TotalRevenue = total,
                    TotalHoursWorked = totalHours,
                    StaffTotal = 0,
                    IsFeedbackOnly = false,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };
                _db.ExecutionDays.Add(day);
                imported++;
                AddOrUpdateShift(day, "Mediodia", d.RevMed, d.HoursMed);
                AddOrUpdateShift(day, "Tarde", d.RevTar, d.HoursTar);
                AddOrUpdateShift(day, "Noche", d.RevNoc, d.HoursNoc);
                shiftsUpdated += 3;
            }
            else
            {
                // Actualizar día existente (manual u otro): Excel sobrescribe; no aplicar descuento 9,1%.
                day.TotalRevenue = total;
                day.TotalHoursWorked = totalHours;
                if (total > 0 || totalHours > 0) day.IsFeedbackOnly = false;
                day.UpdatedAt = DateTime.UtcNow;
                updated++;
                AddOrUpdateShift(day, "Mediodia", d.RevMed, d.HoursMed);
                AddOrUpdateShift(day, "Tarde", d.RevTar, d.HoursTar);
                AddOrUpdateShift(day, "Noche", d.RevNoc, d.HoursNoc);
                shiftsUpdated += 3;
            }
        }
        return (imported, updated, shiftsUpdated);
    }

    private static void AddOrUpdateShift(ExecutionDay day, string shiftName, decimal revenue, decimal hoursWorked)
    {
        var shift = day.ShiftFeedbacks.FirstOrDefault(s => s.ShiftName == shiftName);
        if (shift == null)
        {
            day.ShiftFeedbacks.Add(new ShiftFeedback
            {
                Id = Guid.NewGuid(),
                ExecutionDayId = day.Id,
                ShiftName = shiftName,
                Revenue = revenue,
                HoursWorked = hoursWorked,
                StaffFloor = 0,
                StaffKitchen = 0,
                CreatedAt = DateTime.UtcNow
            });
        }
        else
        {
            shift.Revenue = revenue;
            shift.HoursWorked = hoursWorked;
        }
    }

}
