using System.Data;
using LucasWeb.Api.Models;
using LucasWeb.Api.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace LucasWeb.Api.Data;

public static class DataSeeder
{
    private static readonly string[] ShiftNames = { "Mediodia", "Tarde", "Noche" };
    private static readonly string[] Q1Options = { "Pocas mesas", "Media sala", "Sala completa", "Sala y terraza completas", "Sala y terraza completas y doblamos mesas" };
    private static readonly string[] Q2Options = { "Muy espaciadas, sin acumulación", "Entradas tranquilas", "Flujo constante", "Muchas entradas juntas", "Entradas continuas sin margen" };
    private static readonly string[] Q3Options = { "Siempre adelantado", "Generalmente con margen", "Justo", "Poco margen", "Ningún margen" };
    private static readonly string[] Q4Options = { "Muy fácil", "Fácil", "Normal", "Difícil", "Muy difícil" };

    public static async Task SeedAsync(AppDbContext db, LucasOptions options, ILogger? logger = null)
    {
        await db.Database.EnsureCreatedAsync();
        await EnsurePlannedHoursColumnAsync(db);
        await EnsureStaffComfortColumnsAsync(db);
        await EnsureFeedbackQ5ColumnAsync(db);
        await EnsureKitchenComfortColumnsAsync(db);
        await EnsureShiftWeatherColumnsAsync(db);
        await EnsureExecutionDayFeedbackOnlyColumnAsync(db);
        await EnsureExecutionDayWeatherColumnsAsync(db);
        await EnsurePlannedHoursTotalColumnAsync(db);
        await BackfillStaffComfortAsync(db);
        await EnsureSettingsTableExistsAsync(db);
        await EnsureFacturacionObjetivoTableAsync(db);
        await EnsureDetectedPatternsTableAsync(db);
        await EnsureEventsTableAsync(db);
        await EnsureWeeklyPredictionColumnsAsync(db);
        if (!await db.Users.AnyAsync())
        {
            var pin = options.DefaultPin?.Trim() ?? "";
            if (string.IsNullOrEmpty(pin))
            {
                logger?.LogWarning("No se crea usuario inicial: Lucas:DefaultPin está vacío. Defina la variable de entorno Lucas__DefaultPin y reinicie la aplicación.");
                return;
            }
            var pinHash = BCrypt.Net.BCrypt.HashPassword(pin, BCrypt.Net.BCrypt.GenerateSalt(10));
            var master = new User
            {
                Id = Guid.NewGuid(),
                FullName = "Master",
                Role = "master",
                PinHash = pinHash,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            db.Users.Add(master);
            await db.SaveChangesAsync();
        }
        if (options.SeedDemoData)
            await SeedExecutionDemoDataAsync(db);
    }

    /// <summary>
    /// Carga datos de prueba solo para lo que el usuario introduce: días de ejecución (facturación, horas, notas) y turnos con respuestas Q1–Q4.
    /// El programa calculará dashboard, predicciones, recomendaciones, etc.
    /// </summary>
    private static async Task SeedExecutionDemoDataAsync(AppDbContext db)
    {
        if (await db.ExecutionDays.AnyAsync()) return;
        await InsertExecutionDemoDataAsync(db);
    }

    /// <summary>
    /// Borra todos los días de ejecución y turnos y vuelve a cargar 2 meses (60 días) de datos de prueba (solo datos que introduce el usuario).
    /// </summary>
    public static async Task SeedExecutionDemoDataForceAsync(AppDbContext db)
    {
        var shifts = await db.ShiftFeedbacks.ToListAsync();
        db.ShiftFeedbacks.RemoveRange(shifts);
        var days = await db.ExecutionDays.ToListAsync();
        db.ExecutionDays.RemoveRange(days);
        await db.SaveChangesAsync();
        await InsertExecutionDemoDataAsync(db);
    }

    private static async Task InsertExecutionDemoDataAsync(AppDbContext db)
    {
        var today = DateTime.UtcNow.Date;
        var start = today.AddDays(-59); // 2 meses (60 días)
        for (var d = start; d <= today; d = d.AddDays(1))
        {
            var dayOfWeek = (int)d.DayOfWeek;
            var dayIndex = (int)(d - start).TotalDays;
            var baseRevenue = 800m + (dayOfWeek * 80) + ((dayIndex % 5) * 100);
            if (d.DayOfWeek == DayOfWeek.Saturday || d.DayOfWeek == DayOfWeek.Sunday) baseRevenue *= 1.2m;
            var totalRevenue = 0m;
            var totalHours = 0m;
            var staffTotal = 0;
            var day = new ExecutionDay
            {
                Id = Guid.NewGuid(),
                Date = d,
                TotalRevenue = 0,
                TotalHoursWorked = 0,
                StaffTotal = 0,
                Notes = dayIndex % 7 == 0 ? "Nota de prueba " + d.ToString("yyyy-MM-dd") : null,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            db.ExecutionDays.Add(day);
            for (var s = 0; s < 3; s++)
            {
                var rev = Math.Round(baseRevenue * (0.25m + (s * 0.1m)) + (dayIndex % 3 * 20), 2);
                var hrs = 4m + (s * 1.5m) + (dayIndex % 2);
                var floor = (dayIndex + s) % 4;
                var kitchen = (dayIndex + s + 1) % 4;
                if (floor > 3) floor = 3;
                if (kitchen > 3) kitchen = 3;
                var q1 = Q1Options[(dayIndex + s) % 5];
                var q2 = Q2Options[(dayIndex + s + 1) % 5];
                var q3 = Q3Options[(dayIndex + s + 2) % 5];
                var q4 = Q4Options[(dayIndex + s + 3) % 5];
                var q5 = Q4Options[(dayIndex + s + 4) % 5]; // Dificultad cocina (mismas opciones que Q4)
                totalRevenue += rev;
                totalHours += hrs;
                staffTotal += floor + kitchen;
                var revenuePerWaiter = floor > 0 && rev > 0 ? Math.Round(rev / (decimal)floor, 2) : (decimal?)null;
                var difficultyScore = FeedbackScoring.ComputeDifficultyScore(q1, q2, q3, q4);
                var comfortLevel = FeedbackScoring.GetComfortLevel(difficultyScore);
                db.ShiftFeedbacks.Add(new ShiftFeedback
                {
                    Id = Guid.NewGuid(),
                    ExecutionDayId = day.Id,
                    ShiftName = ShiftNames[s],
                    Revenue = rev,
                    HoursWorked = hrs,
                    StaffFloor = floor,
                    StaffKitchen = kitchen,
                    FeedbackQ1 = q1,
                    FeedbackQ2 = q2,
                    FeedbackQ3 = q3,
                    FeedbackQ4 = q4,
                    FeedbackQ5 = q5,
                    RevenuePerWaiterSala = revenuePerWaiter,
                    DifficultyScore = difficultyScore,
                    ComfortLevel = comfortLevel,
                    RevenuePerWaiterCocina = kitchen > 0 && rev > 0 ? Math.Round(rev / (decimal)kitchen, 2) : (decimal?)null,
                    DifficultyScoreKitchen = FeedbackScoring.ComputeDifficultyScoreKitchen(q5),
                    ComfortLevelKitchen = FeedbackScoring.GetComfortLevel(FeedbackScoring.ComputeDifficultyScoreKitchen(q5)),
                    CreatedAt = DateTime.UtcNow
                });
            }
            day.TotalRevenue = totalRevenue;
            day.TotalHoursWorked = totalHours;
            day.StaffTotal = staffTotal;
        }
        await db.SaveChangesAsync();
    }

    /// <summary>Comprueba si una columna existe en una tabla (SQLite pragma_table_info). Evita ALTER que falle y que EF registre "Failed executing DbCommand".</summary>
    private static async Task<bool> ColumnExistsAsync(AppDbContext db, string tableName, string columnName)
    {
        var conn = db.Database.GetDbConnection();
        if (conn.State != ConnectionState.Open) await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = $"SELECT 1 FROM pragma_table_info('{tableName.Replace("'", "''")}') WHERE name='{columnName.Replace("'", "''")}' LIMIT 1";
        var o = await cmd.ExecuteScalarAsync();
        return o != null;
    }

    /// <summary>tableName, columnName y columnDef son siempre constantes del código (nunca entrada de usuario).</summary>
#pragma warning disable EF1002
    private static async Task AddColumnIfNotExistsAsync(AppDbContext db, string tableName, string columnName, string columnDef)
    {
        if (await ColumnExistsAsync(db, tableName, columnName)) return;
        await db.Database.ExecuteSqlRawAsync($"ALTER TABLE \"{tableName}\" ADD COLUMN \"{columnName}\" {columnDef};");
    }
#pragma warning restore EF1002

    private static async Task EnsurePlannedHoursColumnAsync(AppDbContext db)
    {
        await AddColumnIfNotExistsAsync(db, "ShiftFeedbacks", "PlannedHours", "REAL NULL");
    }

    private static async Task EnsureStaffComfortColumnsAsync(AppDbContext db)
    {
        await AddColumnIfNotExistsAsync(db, "ShiftFeedbacks", "RevenuePerWaiterSala", "REAL NULL");
        await AddColumnIfNotExistsAsync(db, "ShiftFeedbacks", "DifficultyScore", "REAL NULL");
        await AddColumnIfNotExistsAsync(db, "ShiftFeedbacks", "ComfortLevel", "TEXT NULL");
    }

    private static async Task EnsureFeedbackQ5ColumnAsync(AppDbContext db)
    {
        await AddColumnIfNotExistsAsync(db, "ShiftFeedbacks", "FeedbackQ5", "TEXT NULL");
    }

    private static async Task EnsureKitchenComfortColumnsAsync(AppDbContext db)
    {
        await AddColumnIfNotExistsAsync(db, "ShiftFeedbacks", "RevenuePerWaiterCocina", "REAL NULL");
        await AddColumnIfNotExistsAsync(db, "ShiftFeedbacks", "DifficultyScoreKitchen", "REAL NULL");
        await AddColumnIfNotExistsAsync(db, "ShiftFeedbacks", "ComfortLevelKitchen", "TEXT NULL");
    }

    private static async Task EnsureShiftWeatherColumnsAsync(AppDbContext db)
    {
        await AddColumnIfNotExistsAsync(db, "ShiftFeedbacks", "WeatherCode", "INTEGER NULL");
        await AddColumnIfNotExistsAsync(db, "ShiftFeedbacks", "WeatherTempAvg", "REAL NULL");
        await AddColumnIfNotExistsAsync(db, "ShiftFeedbacks", "WeatherPrecipMm", "REAL NULL");
        await AddColumnIfNotExistsAsync(db, "ShiftFeedbacks", "WeatherWindMaxKmh", "REAL NULL");
    }

    /// <summary>
    /// Añade columna IsFeedbackOnly a ExecutionDays (SQLite) para poder excluir días sin facturación/horas.
    /// </summary>
    private static async Task EnsureExecutionDayFeedbackOnlyColumnAsync(AppDbContext db)
    {
        await AddColumnIfNotExistsAsync(db, "ExecutionDays", "IsFeedbackOnly", "INTEGER NOT NULL DEFAULT 0");
    }

    private static async Task EnsureExecutionDayWeatherColumnsAsync(AppDbContext db)
    {
        await AddColumnIfNotExistsAsync(db, "ExecutionDays", "WeatherCode", "INTEGER NULL");
        await AddColumnIfNotExistsAsync(db, "ExecutionDays", "WeatherTemp", "REAL NULL");
        await AddColumnIfNotExistsAsync(db, "ExecutionDays", "WeatherTempMax", "REAL NULL");
        await AddColumnIfNotExistsAsync(db, "ExecutionDays", "WeatherTempMin", "REAL NULL");
        await AddColumnIfNotExistsAsync(db, "ExecutionDays", "WeatherPrecipMm", "REAL NULL");
        await AddColumnIfNotExistsAsync(db, "ExecutionDays", "WeatherWindMaxKmh", "REAL NULL");
        await AddColumnIfNotExistsAsync(db, "ExecutionDays", "IsHoliday", "INTEGER NOT NULL DEFAULT 0");
    }

    /// <summary>Horas totales del día según cuadrante PDF (suma columna Total por empleado).</summary>
    private static async Task EnsurePlannedHoursTotalColumnAsync(AppDbContext db)
    {
        await AddColumnIfNotExistsAsync(db, "ExecutionDays", "PlannedHoursTotal", "REAL NULL");
    }

    /// <summary>Rellena RevenuePerWaiterSala, DifficultyScore y ComfortLevel en turnos que aún los tienen NULL.</summary>
    private static async Task BackfillStaffComfortAsync(AppDbContext db)
    {
        var toUpdate = await db.ShiftFeedbacks
            .Where(s => s.RevenuePerWaiterSala == null || s.DifficultyScore == null || s.RevenuePerWaiterCocina == null || s.DifficultyScoreKitchen == null)
            .ToListAsync();
        var updated = 0;
        foreach (var s in toUpdate)
        {
            var needSave = false;
            if (s.RevenuePerWaiterSala == null || s.DifficultyScore == null)
            {
                var revenuePerWaiter = s.StaffFloor > 0 && s.Revenue > 0 ? Math.Round(s.Revenue / s.StaffFloor, 2) : (decimal?)null;
                var difficultyScore = FeedbackScoring.ComputeDifficultyScore(s.FeedbackQ1, s.FeedbackQ2, s.FeedbackQ3, s.FeedbackQ4);
                var comfortLevel = FeedbackScoring.GetComfortLevel(difficultyScore);
                if (revenuePerWaiter != null || difficultyScore != null)
                {
                    s.RevenuePerWaiterSala = revenuePerWaiter;
                    s.DifficultyScore = difficultyScore;
                    s.ComfortLevel = comfortLevel;
                    needSave = true;
                }
            }
            if (s.RevenuePerWaiterCocina == null || s.DifficultyScoreKitchen == null)
            {
                var revenuePerCocina = s.StaffKitchen > 0 && s.Revenue > 0 ? Math.Round(s.Revenue / s.StaffKitchen, 2) : (decimal?)null;
                var difficultyKitchen = FeedbackScoring.ComputeDifficultyScoreKitchen(s.FeedbackQ5);
                var comfortKitchen = FeedbackScoring.GetComfortLevel(difficultyKitchen);
                if (revenuePerCocina != null || difficultyKitchen != null)
                {
                    s.RevenuePerWaiterCocina = revenuePerCocina;
                    s.DifficultyScoreKitchen = difficultyKitchen;
                    s.ComfortLevelKitchen = comfortKitchen;
                    needSave = true;
                }
            }
            if (needSave) updated++;
        }
        if (updated > 0) await db.SaveChangesAsync();
    }

    private static async Task EnsureSettingsTableExistsAsync(AppDbContext db)
    {
        try
        {
            await db.Database.ExecuteSqlRawAsync(
                "CREATE TABLE IF NOT EXISTS \"Settings\" (\"Key\" TEXT NOT NULL PRIMARY KEY, \"Value\" TEXT NOT NULL, \"UpdatedAt\" TEXT NOT NULL);");
        }
        catch
        {
            // Tabla ya existe o esquema gestionado por migraciones
        }
    }

    private static async Task EnsureFacturacionObjetivoTableAsync(AppDbContext db)
    {
        try
        {
            await db.Database.ExecuteSqlRawAsync(
                "CREATE TABLE IF NOT EXISTS \"FacturacionObjetivoSemanas\" (\"WeekStart\" TEXT NOT NULL PRIMARY KEY, \"TargetRevenue\" REAL NOT NULL);");
        }
        catch
        {
            // Tabla ya existe
        }
    }

    private static async Task EnsureDetectedPatternsTableAsync(AppDbContext db)
    {
        try
        {
            await db.Database.ExecuteSqlRawAsync(
                "CREATE TABLE IF NOT EXISTS \"DetectedPatterns\" (\"Id\" TEXT NOT NULL PRIMARY KEY, \"Type\" TEXT NOT NULL, \"Key\" TEXT NULL, \"JsonData\" TEXT NULL, \"Confidence\" REAL NOT NULL, \"CreatedAt\" TEXT NOT NULL, \"UpdatedAt\" TEXT NOT NULL);");
        }
        catch { }
    }

    private static async Task EnsureEventsTableAsync(AppDbContext db)
    {
        try
        {
            await db.Database.ExecuteSqlRawAsync(
                "CREATE TABLE IF NOT EXISTS \"Events\" (\"Id\" TEXT NOT NULL PRIMARY KEY, \"Date\" TEXT NOT NULL, \"Name\" TEXT NOT NULL, \"Impact\" TEXT NULL, \"Description\" TEXT NULL, \"Source\" TEXT NULL, \"ExternalId\" TEXT NULL, \"CreatedAt\" TEXT NOT NULL, \"UpdatedAt\" TEXT NOT NULL);");
        }
        catch { }
    }

    private static async Task EnsureWeeklyPredictionColumnsAsync(AppDbContext db)
    {
        await AddColumnIfNotExistsAsync(db, "WeeklyPredictions", "ActualRevenue", "REAL NULL");
        await AddColumnIfNotExistsAsync(db, "WeeklyPredictions", "CompletedAt", "TEXT NULL");
        await AddColumnIfNotExistsAsync(db, "WeeklyPredictions", "StaffAccuracyJson", "TEXT NULL");
    }
}
