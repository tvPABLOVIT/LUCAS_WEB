using System.Globalization;
using System.Text.RegularExpressions;
using ClosedXML.Excel;
using LucasWeb.Api.DTOs;

namespace LucasWeb.Api.Services;

/// <summary>
/// Parsea un Excel con facturación y horas reales por turno.
/// Formatos soportados:
/// - Estimaciones (sN_AAAA): fila 21 fechas C–I, filas 22/26/30 facturación Med/Tar/Noc, J39 horas semanales. targetDate = refDate − 14 días.
/// - Genérico: fila 1 cabecera, luego A=fecha, B=facturación, C=horas (sin turnos).
/// - A) Una fila por turno: Fecha, Turno, Facturacion, Horas.
/// - B) Una fila por día: Fecha + Mediodia_Fact/Horas, etc.
/// - C) Plantilla antigua con LUNES y LUNCH/MERIENDA/DINNER.
/// </summary>
public static class ExcelImportService
{
    private static readonly string[] ShiftNames = { "Mediodia", "Tarde", "Noche" };
    private static readonly HashSet<string> ShiftAliases = new(StringComparer.OrdinalIgnoreCase)
    {
        "Mediodia", "Mediodía", "Mediodia", "Tarde", "Noche",
        "M", "T", "N"
    };

    /// <summary>Patrón sN_AAAA (ej. s6_2026, s 6-2026).</summary>
    private static readonly Regex SnAaaaRegex = new(@"s\s*(\d{1,2})\s*[_-]?\s*(\d{4})", RegexOptions.IgnoreCase);

    /// <summary>Indica si el nombre del archivo es formato estimaciones sN_AAAA y devuelve (weekNum, year).</summary>
    public static (bool IsMatch, int WeekNum, int Year) TryParseEstimacionFileName(string? fileName)
    {
        var name = Path.GetFileNameWithoutExtension(fileName ?? "").Trim();
        var m = SnAaaaRegex.Match(name);
        if (!m.Success) return (false, 0, 0);
        if (!int.TryParse(m.Groups[1].Value, out var weekNum) || weekNum < 1 || weekNum > 53) return (false, 0, 0);
        if (!int.TryParse(m.Groups[2].Value, out var year) || year < 2000 || year > 2100) return (false, 0, 0);
        return (true, weekNum, year);
    }

    /// <summary>
    /// Intenta obtener el lunes de la semana desde el nombre de archivo sN_YYYY (ej. s1_2026.xlsx -> lunes semana 1 de 2026).
    /// </summary>
    public static DateTime? GetWeekStartFromFileName(string fileName)
    {
        var (ok, weekNum, year) = TryParseEstimacionFileName(fileName);
        if (!ok) return null;
        var jan4 = new DateTime(year, 1, 4);
        var monday = jan4.AddDays(-(int)jan4.DayOfWeek + (int)DayOfWeek.Monday);
        return monday.AddDays((weekNum - 1) * 7);
    }

    /// <summary>Obtiene la primera fecha válida de la fila 21 para derivar fileYear y weekNum cuando no hay nombre sN_AAAA.</summary>
    public static (int FileYear, int WeekNum)? GetFileYearWeekFromRow21(IXLWorksheet ws)
    {
        for (var col = 3; col <= 9; col++)
        {
            var cell = ws.Cell(21, col);
            var dt = cell.GetValue<DateTime>();
            if (dt != default && dt.Year >= 2000 && dt.Year <= 2100)
                return (dt.Year, ISOWeek.GetWeekOfYear(dt));
            if (cell.TryGetValue(out double serial) && serial >= 36526 && serial <= 50000)
            {
                try
                {
                    var d = DateTime.FromOADate(serial);
                    return (d.Year, ISOWeek.GetWeekOfYear(d));
                }
                catch { }
            }
        }
        return null;
    }

    /// <summary>Detección de plantilla de estimaciones: al menos 5 de las celdas C21:I21 son fechas.</summary>
    public static bool LooksLikeEstimacionTemplate(IXLWorksheet ws)
    {
        int dateCount = 0;
        for (var col = 3; col <= 9; col++)
        {
            if (IsDateCell(ws.Cell(21, col))) dateCount++;
        }
        return dateCount >= 5;
    }

    private static bool IsDateCell(IXLCell cell)
    {
        var dt = cell.GetValue<DateTime>();
        if (dt != default && dt.Year >= 2000 && dt.Year <= 2100) return true;
        if (cell.TryGetValue(out double serial) && serial >= 36526 && serial <= 50000) return true;
        var s = cell.GetString()?.Trim();
        if (string.IsNullOrEmpty(s)) return false;
        if (DateTime.TryParse(s, CultureInfo.GetCultureInfo("es-ES"), DateTimeStyles.None, out _)) return true;
        if (DateTime.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.None, out _)) return true;
        return false;
    }

    /// <summary>Obtiene la fecha de referencia de la celda (fila 21). Devuelve default si no se puede leer.</summary>
    public static DateTime TryGetRefDateFromCell(IXLCell cell, int fileYear, int weekNum, List<string> errList)
    {
        var dt = cell.GetValue<DateTime>();
        if (dt != default && dt.Year >= 2000 && dt.Year <= 2100)
            return ResolveRefDate(dt.Month, dt.Day, fileYear, weekNum);

        if (cell.TryGetValue(out double serial) && serial >= 36526 && serial <= 50000)
        {
            try
            {
                var d = DateTime.FromOADate(serial);
                return ResolveRefDate(d.Month, d.Day, fileYear, weekNum);
            }
            catch { }
        }

        var s = cell.GetString()?.Trim();
        if (!string.IsNullOrEmpty(s))
        {
            if (DateTime.TryParse(s, CultureInfo.GetCultureInfo("es-ES"), DateTimeStyles.None, out var parsed) && parsed.Year >= 2000 && parsed.Year <= 2100)
                return ResolveRefDate(parsed.Month, parsed.Day, fileYear, weekNum);
            if (DateTime.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.None, out parsed) && parsed.Year >= 2000 && parsed.Year <= 2100)
                return ResolveRefDate(parsed.Month, parsed.Day, fileYear, weekNum);
            var cleaned = Regex.Replace(s, @"\b(lunes|martes|miércoles|miercoles|jueves|viernes|sábado|sabado|domingo)\b", "", RegexOptions.IgnoreCase).Trim();
            if (DateTime.TryParse(cleaned, CultureInfo.GetCultureInfo("es-ES"), DateTimeStyles.None, out parsed))
                return ResolveRefDate(parsed.Month, parsed.Day, fileYear, weekNum);
            var match = Regex.Match(s, @"(\d{1,2})\s*[/\.\-]\s*(\d{1,2})");
            if (match.Success && int.TryParse(match.Groups[1].Value, out var day) && int.TryParse(match.Groups[2].Value, out var month)
                && day >= 1 && day <= 31 && month >= 1 && month <= 12)
                return ResolveRefDate(month, day, fileYear, weekNum);
        }

        errList.Add("Fecha inválida en fila 21.");
        return default;
    }

    public static DateTime ResolveRefDate(int month, int day, int fileYear, int weekNum)
    {
        var effectiveYear = (weekNum <= 2 && month == 12) ? fileYear - 1 : fileYear;
        var d = Math.Min(day, DateTime.DaysInMonth(effectiveYear, month));
        return new DateTime(effectiveYear, month, d);
    }

    /// <summary>GetDecimal según doc: double, luego string con InvariantCulture, es-ES, CurrentCulture.</summary>
    public static decimal GetDecimalFromCell(IXLCell cell)
    {
        if (cell.TryGetValue(out double d) && !double.IsNaN(d)) return (decimal)d;
        var s = cell.GetString()?.Trim();
        if (string.IsNullOrEmpty(s)) return 0;
        if (decimal.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed)) return parsed;
        if (decimal.TryParse(s, NumberStyles.Any, CultureInfo.GetCultureInfo("es-ES"), out parsed)) return parsed;
        if (decimal.TryParse(s, NumberStyles.Any, CultureInfo.CurrentCulture, out parsed)) return parsed;
        return 0;
    }

    /// <summary>Parsea la hoja en formato estimaciones (filas 21, 22, 26, 30, J39). targetDate = refDate − 14 días.</summary>
    public static List<EstimacionDayData> ParseEstimacionSheet(IXLWorksheet ws, int fileYear, int weekNum, List<string> errors)
    {
        const int rowDates = 21, rowMed = 22, rowTar = 26, rowNoc = 30, rowHours = 39, colHours = 10;
        var dayData = new List<(DateTime TargetDate, decimal RevMed, decimal RevTar, decimal RevNoc)>();

        for (var col = 3; col <= 9; col++)
        {
            var refDate = TryGetRefDateFromCell(ws.Cell(rowDates, col), fileYear, weekNum, errors);
            if (refDate == default) continue;

            var targetDate = refDate.AddDays(-14);
            if (weekNum <= 2 && targetDate.Year == fileYear && targetDate.Month == 1)
                targetDate = new DateTime(fileYear - 1, 12, Math.Min(targetDate.Day, 31));

            var revMed = GetDecimalFromCell(ws.Cell(rowMed, col));
            var revTar = GetDecimalFromCell(ws.Cell(rowTar, col));
            var revNoc = GetDecimalFromCell(ws.Cell(rowNoc, col));
            dayData.Add((targetDate, revMed, revTar, revNoc));
        }

        if (dayData.Count == 0) return new List<EstimacionDayData>();

        var realWeeklyHours = GetDecimalFromCell(ws.Cell(rowHours, colHours));
        var revenuePerDay = dayData.Select(x => x.RevMed + x.RevTar + x.RevNoc).ToList();
        var totalRevenueWeek = revenuePerDay.Sum();
        decimal[] realHoursPerDay;
        if (totalRevenueWeek > 0 && realWeeklyHours > 0)
            realHoursPerDay = revenuePerDay.Select(r => r / totalRevenueWeek * realWeeklyHours).ToArray();
        else
            realHoursPerDay = Enumerable.Range(0, dayData.Count).Select(_ => realWeeklyHours / dayData.Count).ToArray();

        var result = new List<EstimacionDayData>();
        for (var i = 0; i < dayData.Count; i++)
        {
            var (targetDate, revMed, revTar, revNoc) = dayData[i];
            var total = revMed + revTar + revNoc;
            var realHoursDay = realHoursPerDay[i];
            decimal hoursMed = 0, hoursTar = 0, hoursNoc = 0;
            if (total > 0 && realHoursDay > 0)
            {
                hoursMed = revMed / total * realHoursDay;
                hoursTar = revTar / total * realHoursDay;
                hoursNoc = revNoc / total * realHoursDay;
            }
            result.Add(new EstimacionDayData
            {
                TargetDate = targetDate,
                RevMed = revMed,
                RevTar = revTar,
                RevNoc = revNoc,
                HoursMed = hoursMed,
                HoursTar = hoursTar,
                HoursNoc = hoursNoc
            });
        }
        return result;
    }

    /// <summary>Formato genérico: fila 1 cabecera, luego A=fecha, B=facturación, C=horas.</summary>
    public static List<GenericDayRow> ParseGenericSheet(IXLWorksheet ws, List<string> errors)
    {
        var used = ws.RangeUsed();
        if (used == null) return new List<GenericDayRow>();
        var rows = used.Rows().ToList();
        if (rows.Count < 2) return new List<GenericDayRow>();
        var result = new List<GenericDayRow>();
        var rowNum = 2;
        foreach (var row in rows.Skip(1))
        {
            var dateStr = row.Cell(1).GetString()?.Trim() ?? "";
            if (string.IsNullOrEmpty(dateStr)) { rowNum++; continue; }

            if (!DateTime.TryParse(dateStr, CultureInfo.GetCultureInfo("es-ES"), DateTimeStyles.None, out var date)
                && !DateTime.TryParse(dateStr, CultureInfo.InvariantCulture, DateTimeStyles.None, out date))
            {
                errors.Add($"Fila {rowNum}: fecha inválida '{dateStr}'.");
                rowNum++;
                continue;
            }

            var revenue = GetDecimalFromCell(row.Cell(2));
            var hours = GetDecimalFromCell(row.Cell(3));
            result.Add(new GenericDayRow { Date = date.Date, TotalRevenue = revenue, TotalHoursWorked = hours });
            rowNum++;
        }
        return result;
    }

    public static List<ExcelShiftRow> Parse(Stream stream, List<string> errors)
    {
        var rows = new List<ExcelShiftRow>();
        try
        {
            using var book = new XLWorkbook(stream);
            var ws = book.Worksheet(1);
            if (ws == null) { errors.Add("No se encontró ninguna hoja."); return rows; }

            var used = ws.RangeUsed();
            if (used == null) { errors.Add("La hoja está vacía."); return rows; }

            var firstRow = used.FirstRow().RowNumber();
            var lastRow = used.LastRow().RowNumber();
            var lastCol = used.LastColumn().ColumnNumber();

            var dayNamesRow = FindRowWithDayNames(ws, firstRow, Math.Min(lastRow, firstRow + 25));
            if (dayNamesRow >= 0)
            {
                ParseLayoutC(ws, dayNamesRow, lastRow, rows, errors);
                if (rows.Count > 0) return rows;
            }

            var headers = new List<string>();
            for (var c = 1; c <= lastCol; c++)
                headers.Add(GetCellString(ws, firstRow, c));

            if (firstRow >= lastRow) { errors.Add("No hay filas de datos."); return rows; }

            if (headers.Any(h => h.Contains("Turno", StringComparison.OrdinalIgnoreCase) || h.Equals("Shift", StringComparison.OrdinalIgnoreCase)))
                ParseLayoutA(ws, firstRow, lastRow, headers, rows, errors);
            else
                ParseLayoutB(ws, firstRow, lastRow, headers, rows, errors);
        }
        catch (Exception ex)
        {
            errors.Add("Error al leer el Excel: " + ex.Message);
        }

        return rows;
    }

    /// <summary>
    /// Plantilla tipo s1_2026: fila con LUNES..DOMINGO, siguiente con fechas (serial), luego bloques LUNCH/MERIENDA/DINNER (fila fact + fila horas), columnas C–I = Lun–Dom.
    /// </summary>
    private static void ParseLayoutC(IXLWorksheet ws, int dayNamesRow, int lastRow, List<ExcelShiftRow> rows, List<string> errors)
    {
        const int colFirstDay = 3;
        const int colLastDay = 9;
        var dateRow = dayNamesRow + 1;
        var dates = new List<DateTime>();
        for (var c = colFirstDay; c <= colLastDay; c++)
        {
            var cell = ws.Cell(dateRow, c);
            if (cell.TryGetValue(out double serial))
            {
                try
                {
                    var dt = DateTime.FromOADate(serial);
                    dates.Add(dt.Date);
                }
                catch
                {
                    dates.Clear();
                    break;
                }
            }
            else
            {
                var s = GetCellString(ws, dateRow, c);
                if (DateTime.TryParse(s, out var d)) dates.Add(d.Date);
                else { dates.Clear(); break; }
            }
        }
        if (dates.Count != 7)
        {
            errors.Add("No se pudieron leer las 7 fechas (Lun–Dom) en la fila siguiente a los días.");
            return;
        }

        var shiftKeywords = new[] { ("LUNCH", "Mediodia"), ("MEDIO DIA", "Mediodia"), ("MEDIODIA", "Mediodia"), ("MERIENDA", "Tarde"), ("TARDE", "Tarde"), ("DINNER", "Noche"), ("NOCHE", "Noche") };
        var seenShifts = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var r = dateRow + 1;
        while (r + 1 <= lastRow && seenShifts.Count < 3)
        {
            var cellA = GetCellString(ws, r, 1).Trim();
            if (string.IsNullOrEmpty(cellA)) { r++; continue; }
            string? shiftName = null;
            foreach (var (kw, name) in shiftKeywords)
                if (cellA.Contains(kw, StringComparison.OrdinalIgnoreCase))
                {
                    shiftName = name;
                    break;
                }
            if (shiftName == null || seenShifts.Contains(shiftName)) { r++; continue; }
            seenShifts.Add(shiftName);

            for (var i = 0; i < 7 && i < dates.Count; i++)
            {
                var revenue = GetCellDecimal(ws, r, colFirstDay + i);
                var hours = GetCellDecimal(ws, r + 1, colFirstDay + i);
                rows.Add(new ExcelShiftRow { Date = dates[i], ShiftName = shiftName, Revenue = revenue, HoursWorked = hours });
            }
            r += 2;
        }
    }

    private static int FindRowWithDayNames(IXLWorksheet ws, int fromRow, int toRow)
    {
        for (var r = fromRow; r <= toRow; r++)
        {
            var c3 = GetCellString(ws, r, 3).Trim();
            if (c3.Contains("LUNES", StringComparison.OrdinalIgnoreCase))
                return r;
        }
        return -1;
    }

    private static void ParseLayoutA(IXLWorksheet ws, int firstRow, int lastRow, List<string> headers, List<ExcelShiftRow> rows, List<string> errors)
    {
        var colDate = FindColumn(headers, "Fecha", "Date");
        var colShift = FindColumn(headers, "Turno", "Shift");
        var colRevenue = FindColumn(headers, "Facturacion", "Facturación", "Revenue", "Facturacion total");
        var colHours = FindColumn(headers, "Horas", "HoursWorked", "Horas reales", "Hours");

        if (colDate < 0) { errors.Add("No se encontró columna Fecha/Date."); return; }
        if (colShift < 0) { errors.Add("No se encontró columna Turno/Shift."); return; }
        if (colRevenue < 0) { errors.Add("No se encontró columna Facturacion/Revenue."); return; }
        if (colHours < 0) { errors.Add("No se encontró columna Horas/HoursWorked."); return; }

        for (var r = firstRow + 1; r <= lastRow; r++)
        {
            var dateStr = GetCellString(ws, r, colDate + 1);
            var shiftStr = GetCellString(ws, r, colShift + 1);
            if (string.IsNullOrWhiteSpace(dateStr)) continue;

            if (!DateTime.TryParse(dateStr.Trim(), out var date))
            {
                errors.Add($"Fila {r}: fecha no válida '{dateStr}'.");
                continue;
            }

            var shift = NormalizeShiftName(shiftStr);
            if (string.IsNullOrEmpty(shift))
            {
                errors.Add($"Fila {r}: turno no reconocido '{shiftStr}'. Use Mediodia, Tarde o Noche.");
                continue;
            }

            var revenue = GetCellDecimal(ws, r, colRevenue + 1);
            var hours = GetCellDecimal(ws, r, colHours + 1);

            rows.Add(new ExcelShiftRow { Date = date.Date, ShiftName = shift, Revenue = revenue, HoursWorked = hours });
        }
    }

    private static void ParseLayoutB(IXLWorksheet ws, int firstRow, int lastRow, List<string> headers, List<ExcelShiftRow> rows, List<string> errors)
    {
        var colDate = FindColumn(headers, "Fecha", "Date");
        if (colDate < 0) { errors.Add("No se encontró columna Fecha/Date."); return; }

        foreach (var shift in ShiftNames)
        {
            var colRev = FindColumn(headers, shift + "_Fact", shift + "_Revenue", "Fact_" + shift, "Facturacion_" + shift);
            var colHrs = FindColumn(headers, shift + "_Horas", shift + "_Hours", "Horas_" + shift, "Hours_" + shift);
            if (colRev < 0 && colHrs < 0) continue;
            var revenueCol = colRev >= 0 ? colRev + 1 : colHrs + 1;
            var hoursCol = colHrs >= 0 ? colHrs + 1 : colRev + 1;
            if (colRev < 0) revenueCol = colHrs + 1;
            if (colHrs < 0) hoursCol = colRev + 1;

            for (var r = firstRow + 1; r <= lastRow; r++)
            {
                var dateStr = GetCellString(ws, r, colDate + 1);
                if (string.IsNullOrWhiteSpace(dateStr)) continue;
                if (!DateTime.TryParse(dateStr.Trim(), out var date))
                {
                    errors.Add($"Fila {r}: fecha no válida '{dateStr}'.");
                    continue;
                }

                var revenue = colRev >= 0 ? GetCellDecimal(ws, r, colRev + 1) : 0;
                var hours = colHrs >= 0 ? GetCellDecimal(ws, r, colHrs + 1) : 0;
                if (revenue == 0 && hours == 0) continue;

                rows.Add(new ExcelShiftRow { Date = date.Date, ShiftName = shift, Revenue = revenue, HoursWorked = hours });
            }
        }
    }

    private static int FindColumn(List<string> headers, params string[] names)
    {
        for (var i = 0; i < headers.Count; i++)
        {
            var h = (headers[i] ?? "").Trim();
            foreach (var n in names)
                if (h.Equals(n, StringComparison.OrdinalIgnoreCase) || h.Contains(n, StringComparison.OrdinalIgnoreCase))
                    return i;
        }
        return -1;
    }

    private static string GetCellString(IXLWorksheet ws, int row, int col)
    {
        var cell = ws.Cell(row, col);
        if (cell == null) return "";
        var v = cell.GetString();
        return v ?? "";
    }

    private static decimal GetCellDecimal(IXLWorksheet ws, int row, int col)
    {
        var cell = ws.Cell(row, col);
        if (cell == null) return 0;
        if (cell.TryGetValue(out decimal d)) return d;
        var s = cell.GetString();
        if (string.IsNullOrWhiteSpace(s)) return 0;
        return decimal.TryParse(s.Replace(",", ".").Trim(), out var parsed) ? parsed : 0;
    }

    private static string? NormalizeShiftName(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var v = value.Trim();
        if (v.Equals("Mediodia", StringComparison.OrdinalIgnoreCase) || v.Equals("Mediodía", StringComparison.OrdinalIgnoreCase) || v.Equals("M", StringComparison.OrdinalIgnoreCase) || v.Equals("LUNCH", StringComparison.OrdinalIgnoreCase) || v.Contains("MEDIO DIA", StringComparison.OrdinalIgnoreCase))
            return "Mediodia";
        if (v.Equals("Tarde", StringComparison.OrdinalIgnoreCase) || v.Equals("T", StringComparison.OrdinalIgnoreCase) || v.Equals("MERIENDA", StringComparison.OrdinalIgnoreCase))
            return "Tarde";
        if (v.Equals("Noche", StringComparison.OrdinalIgnoreCase) || v.Equals("N", StringComparison.OrdinalIgnoreCase) || v.Equals("DINNER", StringComparison.OrdinalIgnoreCase))
            return "Noche";
        return null;
    }
}

public class ExcelShiftRow
{
    public DateTime Date { get; set; }
    public string ShiftName { get; set; } = "";
    public decimal Revenue { get; set; }
    public decimal HoursWorked { get; set; }
}

public class EstimacionDayData
{
    public DateTime TargetDate { get; set; }
    public decimal RevMed { get; set; }
    public decimal RevTar { get; set; }
    public decimal RevNoc { get; set; }
    public decimal HoursMed { get; set; }
    public decimal HoursTar { get; set; }
    public decimal HoursNoc { get; set; }
}

public class GenericDayRow
{
    public DateTime Date { get; set; }
    public decimal TotalRevenue { get; set; }
    public decimal TotalHoursWorked { get; set; }
}
