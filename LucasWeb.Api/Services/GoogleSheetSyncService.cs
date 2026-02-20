using System.Globalization;
using System.Text.RegularExpressions;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Services;
using Google.Apis.Sheets.v4;
using Google.Apis.Sheets.v4.Data;
using LucasWeb.Api.Data;
using LucasWeb.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace LucasWeb.Api.Services;

/// <summary>
/// Sincroniza días con Google Sheet: hoja del mes (Mes Año), una fila por día (fila = día + 1), 9 columnas A–I. Ver GUARDADO_GOOGLE_SHEET.md.
/// Facturación escrita en el sheet: la misma norma que en BD. Cuando el dato viene de ingreso manual ya está guardado con descuento 9,1%;
/// cuando viene de importación Excel se guarda y se escribe en el sheet sin descuento. Escribimos siempre el valor almacenado en BD.
/// </summary>
public class GoogleSheetSyncService : IGoogleSheetSyncService
{
    private static readonly Regex SpreadsheetIdRegex = new(@"/d/([a-zA-Z0-9_-]+)(?:/|$)", RegexOptions.Compiled);
    private static readonly CultureInfo EsEs = CultureInfo.GetCultureInfo("es-ES");
    private readonly AppDbContext _db;
    private readonly ILogger<GoogleSheetSyncService> _logger;

    public GoogleSheetSyncService(AppDbContext db, ILogger<GoogleSheetSyncService> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <summary>Un día guardado (Preguntas o Registro): escribe en la hoja del mes en segundo plano. La facturación ya viene con descuento 9,1% aplicado en BD.</summary>
    public async Task SyncDayAsync(ExecutionDay day, CancellationToken cancellationToken = default)
    {
        if (day?.ShiftFeedbacks == null) return;
        // Evitar volcar a Google Sheets días "solo feedback" (sin facturación/horas), para no contaminar métricas.
        if (day.IsFeedbackOnly && day.TotalRevenue == 0 && day.TotalHoursWorked == 0) return;
        var (service, spreadsheetId) = await GetServiceAndSheetIdAsync(cancellationToken);
        if (service == null || string.IsNullOrEmpty(spreadsheetId)) return;
        try
        {
            await WriteDayToMonthSheetAsync(service, spreadsheetId, day, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Google Sheet sync falló para el día {Date}.", day.Date.ToString("yyyy-MM-dd"));
        }
    }

    /// <summary>Varios días (Exportar todo / Import Excel): escribe cada uno en su hoja del mes. Si los datos vienen de Excel no tienen descuento 9,1%.</summary>
    public async Task SyncAsync(IEnumerable<DateTime> dates, CancellationToken cancellationToken = default)
    {
        var dateList = dates.ToList();
        if (dateList.Count == 0) return;

        var (service, spreadsheetId) = await GetServiceAndSheetIdAsync(cancellationToken);
        if (service == null || string.IsNullOrEmpty(spreadsheetId)) return;

        var days = await _db.ExecutionDays
            .AsNoTracking()
            .Include(e => e.ShiftFeedbacks)
            .Where(e => dateList.Contains(e.Date))
            .OrderBy(e => e.Date)
            .ToListAsync(cancellationToken);

        foreach (var day in days)
        {
            try
            {
                if (day.IsFeedbackOnly && day.TotalRevenue == 0 && day.TotalHoursWorked == 0) continue;
                await WriteDayToMonthSheetAsync(service, spreadsheetId, day, cancellationToken);
            }
            catch
            {
                // Continuar con el siguiente día
            }
        }
    }

    private async Task<(SheetsService? service, string? spreadsheetId)> GetServiceAndSheetIdAsync(CancellationToken cancellationToken)
    {
        var url = await _db.Settings.AsNoTracking().Where(s => s.Key == "GoogleSheetsUrl").Select(s => s.Value).FirstOrDefaultAsync(cancellationToken);
        var credentialsPath = await _db.Settings.AsNoTracking().Where(s => s.Key == "GoogleCredentialsPath").Select(s => s.Value).FirstOrDefaultAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(url))
        {
            _logger.LogDebug("Google Sheet sync omitida: no hay GoogleSheetsUrl en Configuración → Integraciones.");
            return (null, null);
        }

        var match = SpreadsheetIdRegex.Match(url.Trim());
        if (!match.Success)
        {
            _logger.LogWarning("Google Sheet sync omitida: URL de hoja no válida (se espera https://docs.google.com/spreadsheets/d/ID/...).");
            return (null, null);
        }
        var spreadsheetId = match.Groups[1].Value;

        // 1) Intentar cargar credenciales desde variable de entorno (modo recomendado en hosting como Railway)
        //    - Si en Configuración → Integraciones se pone "env:NOMBRE_VARIABLE" en GoogleCredentialsPath,
        //      se leerá esa variable de entorno.
        //    - Si no hay nada configurado pero existe GOOGLE_CREDENTIALS_JSON, se usará esa.
        GoogleCredential? credential = null;
        try
        {
            string? credentialsJson = null;

            // Opción A: GoogleCredentialsPath = "env:NOMBRE"
            if (!string.IsNullOrWhiteSpace(credentialsPath) && credentialsPath.Trim().StartsWith("env:", StringComparison.OrdinalIgnoreCase))
            {
                var envName = credentialsPath.Trim()[4..].Trim();
                if (!string.IsNullOrWhiteSpace(envName))
                    credentialsJson = Environment.GetEnvironmentVariable(envName);
            }

            // Opción B: variable por defecto GOOGLE_CREDENTIALS_JSON
            if (string.IsNullOrWhiteSpace(credentialsJson))
                credentialsJson = Environment.GetEnvironmentVariable("GOOGLE_CREDENTIALS_JSON");

            if (!string.IsNullOrWhiteSpace(credentialsJson))
            {
                credential = GoogleCredential.FromJson(credentialsJson).CreateScoped(SheetsService.Scope.Spreadsheets);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Google Sheet sync omitida: error al cargar credenciales desde variable de entorno (env:...).");
            credential = null;
        }

        // 2) Fallback: archivo en disco (modo antiguo para entorno local/PC)
        if (credential == null)
        {
            // Si la ruta de credenciales está vacía, buscar: google-credentials.json o manager-os-484801-069d6254dfb1.json
            // en BaseDirectory o en directorios padre (raíz del proyecto)
            string pathToUse;
            if (string.IsNullOrWhiteSpace(credentialsPath))
            {
                var baseDir = AppContext.BaseDirectory;
                var fileName = "manager-os-484801-069d6254dfb1.json";
                var altFileName = "google-credentials.json";
                var candidates = new List<string>
                {
                    Path.Combine(baseDir, altFileName),
                    Path.Combine(baseDir, fileName)
                };
                var dir = new DirectoryInfo(baseDir);
                for (var i = 0; i < 4 && dir?.Parent != null; i++)
                {
                    dir = dir.Parent;
                    if (dir != null)
                    {
                        candidates.Add(Path.Combine(dir.FullName, fileName));
                        candidates.Add(Path.Combine(dir.FullName, altFileName));
                    }
                }
                pathToUse = candidates.FirstOrDefault(File.Exists) ?? candidates[0];
            }
            else
            {
                pathToUse = Path.IsPathRooted(credentialsPath)
                    ? credentialsPath.Trim()
                    : Path.Combine(AppContext.BaseDirectory, credentialsPath.TrimStart('/', '\\'));
            }
            if (!File.Exists(pathToUse))
            {
                _logger.LogWarning("Google Sheet sync omitida: credenciales no encontradas. Configure GoogleCredentialsPath como 'env:GOOGLE_CREDENTIALS_JSON' (y defina esa variable en el hosting) o coloque manager-os-484801-069d6254dfb1.json/google-credentials.json junto a la API. BaseDir: {BaseDir}", AppContext.BaseDirectory);
                return (null, null);
            }

            try
            {
                using var stream = new FileStream(pathToUse, FileMode.Open, FileAccess.Read, FileShare.Read);
                credential = GoogleCredential.FromStream(stream).CreateScoped(SheetsService.Scope.Spreadsheets);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Google Sheet sync omitida: error al cargar credenciales desde {Path}.", pathToUse);
                return (null, null);
            }
        }

        var service = new SheetsService(new BaseClientService.Initializer
        {
            HttpClientInitializer = credential,
            ApplicationName = "Lucas"
        });
        return (service, spreadsheetId);
    }

    private static string GetMonthSheetName(DateTime date)
    {
        var name = EsEs.DateTimeFormat.GetMonthName(date.Month);
        return char.ToUpperInvariant(name[0]) + name[1..] + " " + date.Year;
    }

    private static string GetDayOfWeekName(DateTime date)
    {
        var name = EsEs.DateTimeFormat.GetDayName(date.DayOfWeek);
        return char.ToUpperInvariant(name[0]) + name[1..];
    }

    /// <summary>Observaciones del turno: solo el resumen del turno (clima opcional + Q1–Q5). La facturación va en columnas F–I.</summary>
    /// <param name="includeDayWeather">True solo para el primer turno del día, para no repetir el clima.</param>
    private static string BuildResumenFromFeedback(ShiftFeedback? shift, ExecutionDay day, bool includeDayWeather)
    {
        static string LowerFirst(string s)
        {
            if (string.IsNullOrEmpty(s) || s.Length == 0) return s;
            return char.ToLowerInvariant(s[0]) + s[1..];
        }

        var q1 = (shift?.FeedbackQ1 ?? "").Trim();
        var q2 = (shift?.FeedbackQ2 ?? "").Trim();
        var q3 = (shift?.FeedbackQ3 ?? "").Trim();
        var q4 = (shift?.FeedbackQ4 ?? "").Trim();
        var q5 = (shift?.FeedbackQ5 ?? "").Trim();
        var hasFeedback = !string.IsNullOrEmpty(q1) || !string.IsNullOrEmpty(q2) || !string.IsNullOrEmpty(q3) || !string.IsNullOrEmpty(q4) || !string.IsNullOrEmpty(q5);
        if (!hasFeedback && !includeDayWeather) return "";

        var frases = new List<string>();

        if (includeDayWeather)
        {
            var clima = GetWeatherDescription(day);
            if (!string.IsNullOrEmpty(clima)) frases.Add(clima);
        }

        if (hasFeedback)
        {
            var partes = new List<string>();
            if (!string.IsNullOrEmpty(q1)) partes.Add("con " + LowerFirst(q1));
            if (!string.IsNullOrEmpty(q2)) partes.Add(LowerFirst(q2));
            if (!string.IsNullOrEmpty(q3)) partes.Add(LowerFirst(q3));
            var turno = "Fue un turno " + string.Join(", ", partes) + ".";
            if (!string.IsNullOrEmpty(q4))
                turno += " En conjunto resultó " + LowerFirst(q4) + ".";
            if (!string.IsNullOrEmpty(q5))
                turno += " En cocina: " + LowerFirst(q5) + ".";
            frases.Add(turno);
        }

        return string.Join(" ", frases).Trim();
    }

    /// <summary>Descripción breve del clima del día si fue lluvioso o extremo (frío/calor).</summary>
    private static string? GetWeatherDescription(ExecutionDay day)
    {
        var code = day.WeatherCode;
        var temp = day.WeatherTemp;
        var lluvioso = code.HasValue && IsRainCode(code.Value);
        var extremo = temp.HasValue && (temp.Value < 5 || temp.Value > 30);
        if (lluvioso && extremo) return "Día lluvioso y con clima extremo.";
        if (lluvioso) return "Día lluvioso.";
        if (extremo) return "Día con clima extremo.";
        return null;
    }

    private static bool IsRainCode(int code) => code is >= 51 and <= 67 or >= 71 and <= 77 or >= 80 and <= 82 or 95 or 96;

    private async Task EnsureMonthSheetAsync(SheetsService service, string spreadsheetId, string monthSheetName, CancellationToken cancellationToken)
    {
        var spreadsheet = await service.Spreadsheets.Get(spreadsheetId).ExecuteAsync(cancellationToken);
        var sheets = spreadsheet.Sheets ?? new List<Sheet>();
        var existing = sheets.FirstOrDefault(s => s.Properties?.Title != null && string.Equals(s.Properties.Title, monthSheetName, StringComparison.OrdinalIgnoreCase));
        if (existing != null) return;

        var template = sheets.FirstOrDefault(s => s.Properties?.Title != null && string.Equals(s.Properties.Title, "Plantilla", StringComparison.OrdinalIgnoreCase));
        var sourceSheet = template ?? sheets.FirstOrDefault();
        if (sourceSheet?.Properties?.SheetId == null) return;

        var dupRequest = new BatchUpdateSpreadsheetRequest
        {
            Requests = new List<Request>
            {
                new Request { DuplicateSheet = new DuplicateSheetRequest { SourceSheetId = sourceSheet.Properties.SheetId.Value, InsertSheetIndex = 0 } }
            }
        };
        var batchResp = await service.Spreadsheets.BatchUpdate(dupRequest, spreadsheetId).ExecuteAsync(cancellationToken);
        var newSheetId = batchResp.Replies?.FirstOrDefault()?.DuplicateSheet?.Properties?.SheetId;
        if (newSheetId == null) return;

        var updateReq = new BatchUpdateSpreadsheetRequest
        {
            Requests = new List<Request>
            {
                new Request
                {
                    UpdateSheetProperties = new UpdateSheetPropertiesRequest
                    {
                        Properties = new SheetProperties { SheetId = newSheetId.Value, Title = monthSheetName },
                        Fields = "title"
                    }
                }
            }
        };
        await service.Spreadsheets.BatchUpdate(updateReq, spreadsheetId).ExecuteAsync(cancellationToken);

        var clearRange1 = $"'{monthSheetName}'!A2:I32";
        var clearRange2 = $"'{monthSheetName}'!A34:I1000";
        await service.Spreadsheets.Values.Clear(new ClearValuesRequest(), spreadsheetId, clearRange1).ExecuteAsync(cancellationToken);
        await service.Spreadsheets.Values.Clear(new ClearValuesRequest(), spreadsheetId, clearRange2).ExecuteAsync(cancellationToken);
    }

    private async Task WriteDayToMonthSheetAsync(SheetsService service, string spreadsheetId, ExecutionDay day, CancellationToken cancellationToken)
    {
        var monthSheetName = GetMonthSheetName(day.Date);
        await EnsureMonthSheetAsync(service, spreadsheetId, monthSheetName, cancellationToken);

        var med = day.ShiftFeedbacks?.FirstOrDefault(s => string.Equals(s.ShiftName, "Mediodia", StringComparison.OrdinalIgnoreCase));
        var tar = day.ShiftFeedbacks?.FirstOrDefault(s => string.Equals(s.ShiftName, "Tarde", StringComparison.OrdinalIgnoreCase));
        var noc = day.ShiftFeedbacks?.FirstOrDefault(s => string.Equals(s.ShiftName, "Noche", StringComparison.OrdinalIgnoreCase));

        // Facturación: valor almacenado en BD (manual → ya con 9,1% descuento; Excel → sin descuento). Misma norma que en API.
        var row = new List<object>
        {
            day.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            GetDayOfWeekName(day.Date),
            BuildResumenFromFeedback(med, day, true),
            BuildResumenFromFeedback(tar, day, false),
            BuildResumenFromFeedback(noc, day, false),
            (double)(med?.Revenue ?? 0),
            (double)(tar?.Revenue ?? 0),
            (double)(noc?.Revenue ?? 0),
            (double)day.TotalRevenue
        };

        var targetRow = day.Date.Day + 1;
        var range = $"'{monthSheetName}'!A{targetRow}:I{targetRow}";
        var body = new ValueRange { Values = new List<IList<object>> { row } };
        var request = service.Spreadsheets.Values.Update(body, spreadsheetId, range);
        request.ValueInputOption = SpreadsheetsResource.ValuesResource.UpdateRequest.ValueInputOptionEnum.USERENTERED;
        await request.ExecuteAsync(cancellationToken);
    }
}
