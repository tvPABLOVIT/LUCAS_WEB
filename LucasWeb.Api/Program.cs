using LucasWeb.Api;
using LucasWeb.Api.Data;
using LucasWeb.Api.Middleware;
using LucasWeb.Api.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting.WindowsServices;

// Log inmediato para ver en Railway que el proceso arrancó
Console.WriteLine("[Lucas] Iniciando...");

// Como servicio de Windows el directorio de trabajo es System32; usar la carpeta del ejecutable.
var contentRoot = WindowsServiceHelpers.IsWindowsService() && AppContext.BaseDirectory is { } baseDir
    ? baseDir
    : null;

var builder = contentRoot != null
    ? WebApplication.CreateBuilder(new WebApplicationOptions { ContentRootPath = contentRoot })
    : WebApplication.CreateBuilder(args);

// Railway inyecta PORT (ej. 8080). Si no existe, usar 8080 para compatibilidad con Railway.
var port = Environment.GetEnvironmentVariable("PORT") ?? "8080";
builder.WebHost.UseUrls($"http://0.0.0.0:{port}");
Console.WriteLine("[Lucas] Puerto: {Port}", port);

if (OperatingSystem.IsWindows())
    builder.Host.UseWindowsService();

builder.Services.Configure<LucasOptions>(builder.Configuration.GetSection(LucasOptions.SectionName));
builder.Services.Configure<CuadranteParserOptions>(builder.Configuration.GetSection(CuadranteParserOptions.SectionName));
builder.Services.AddScoped<ICuadrantePdfService, CuadrantePdfService>();
builder.Services.AddDbContext<AppDbContext>(o =>
{
    var cs = builder.Configuration.GetConnectionString("DefaultConnection") ?? "Data Source=manageros.db;Cache=Shared";
    o.UseSqlite(cs);
});
builder.Services.AddHttpClient();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<NextWeekPredictionService>();
builder.Services.AddScoped<IWeatherService, WeatherService>();
builder.Services.AddScoped<IGeocodingService, GeocodingService>();
builder.Services.AddHostedService<WeatherAutoBackfillHostedService>();
builder.Services.AddHostedService<PredictionEvaluationHostedService>();
builder.Services.AddHostedService<AuthTokenCleanupHostedService>();
builder.Services.AddScoped<IHolidaysService, NagerHolidaysService>();
builder.Services.AddScoped<IEventsService, EventsService>();
builder.Services.AddScoped<IDetectedPatternsService, DetectedPatternsService>();
builder.Services.AddScoped<IStaffRevenueComfortService, StaffRevenueComfortService>();
builder.Services.AddScoped<IEvaluatePredictionsService, EvaluatePredictionsService>();
builder.Services.AddScoped<IGoogleSheetSyncService, GoogleSheetSyncService>();
builder.Services.AddScoped<PredictionEnrichmentService>();
builder.Services.AddScoped<StaffByTurnoPredictionService>();
builder.Services.AddControllers();
var lucasOptions = builder.Configuration.GetSection(LucasOptions.SectionName).Get<LucasOptions>() ?? new LucasOptions();
var isProduction = builder.Environment.IsProduction();
var allowedOrigins = (lucasOptions.AllowedOrigins ?? "").Trim();
builder.Services.AddCors(o =>
{
    o.AddDefaultPolicy(p =>
    {
        p.AllowAnyMethod().AllowAnyHeader();
        if (isProduction && !string.IsNullOrEmpty(allowedOrigins))
        {
            var origins = allowedOrigins.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (origins.Length > 0)
                p.WithOrigins(origins).AllowCredentials();
            else
                p.AllowAnyOrigin();
        }
        else
            p.AllowAnyOrigin();
    });
});

var app = builder.Build();

app.UseMiddleware<ApiExceptionMiddleware>();

// DataSeeder en segundo plano: la app enlaza y responde /health de inmediato.
// Si el seeder se bloquea (BD en volumen lento/bloqueada), la app sigue operativa.
_ = Task.Run(async () =>
{
    await Task.Delay(500);
    try
    {
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var options = scope.ServiceProvider.GetRequiredService<Microsoft.Extensions.Options.IOptions<LucasOptions>>().Value;
        var loggerFactory = scope.ServiceProvider.GetService<Microsoft.Extensions.Logging.ILoggerFactory>();
        var logger = loggerFactory?.CreateLogger("LucasWeb.Api.Data.DataSeeder");
        await DataSeeder.SeedAsync(db, options, logger);
    }
    catch (Exception ex)
    {
        var loggerFactory = app.Services.GetService<Microsoft.Extensions.Logging.ILoggerFactory>();
        loggerFactory?.CreateLogger("DataSeeder").LogError(ex, "DataSeeder en segundo plano falló.");
    }
});

app.UseCors();
app.UseMiddleware<BearerAuthMiddleware>();
app.UseAuthorization();

// Health check sin autenticación para Railway y comprobar que la app responde
app.MapGet("/health", () => Results.Ok(new { status = "ok" })).AllowAnonymous();

app.MapControllers();

var wwwroot = Path.Combine(app.Environment.ContentRootPath, "wwwroot");
var wwwrootExists = Directory.Exists(wwwroot);
app.Logger.LogInformation("wwwroot existe: {Exists}, ruta: {Path}", wwwrootExists, wwwroot);

if (wwwrootExists)
{
    app.Use(async (context, next) =>
    {
        if (context.Request.Path.Equals("/favicon.ico", StringComparison.OrdinalIgnoreCase))
        {
            context.Request.Path = "/logo.png";
        }
        await next();
    });
    app.UseDefaultFiles();
    app.UseStaticFiles();
    app.MapFallbackToFile("index.html");
}
else
{
    app.MapFallback(async context =>
    {
        context.Response.StatusCode = 500;
        await context.Response.WriteAsJsonAsync(new { error = "wwwroot no encontrado", path = wwwroot });
    });
}

app.Logger.LogInformation("Escuchando en http://0.0.0.0:{Port} (PORT={PortEnv})", port, Environment.GetEnvironmentVariable("PORT") ?? "(no definido)");
Console.WriteLine("[Lucas] A punto de escuchar en 0.0.0.0:" + port);

try
{
    await app.RunAsync();
}
catch (Exception ex)
{
    Console.WriteLine("[Lucas] Error al ejecutar: " + ex);
    throw;
}
