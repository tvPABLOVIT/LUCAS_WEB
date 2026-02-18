using LucasWeb.Api.Data;
using LucasWeb.Api.DTOs;
using Microsoft.EntityFrameworkCore;

namespace LucasWeb.Api.Services;

/// <summary>Agregados por esquema de personal (sala-cocina) y banda de facturación por camarero para "límite cómodo".</summary>
public class StaffRevenueComfortService : IStaffRevenueComfortService
{
    private readonly AppDbContext _db;

    private static readonly decimal[] BandLimits = { 0, 400, 500, 600, 700, 800, 1000, 9999 };

    /// <summary>Esquemas sala-cocina a analizar (en este orden).</summary>
    private static readonly string[] AllowedSchemas = { "1-1", "1-2", "2-1", "2-2", "2-3", "3-2", "3-3" };

    private sealed record ShiftSalaRow(int StaffFloor, int StaffKitchen, decimal RevenuePerWaiterSala, decimal DifficultyScore);

    public StaffRevenueComfortService(AppDbContext db) => _db = db;

    public async Task<StaffRevenueComfortResult> GetAggregatesAsync(int? minShifts = null, CancellationToken cancellationToken = default)
    {
        var shifts = await _db.ShiftFeedbacks
            .AsNoTracking()
            .Where(s => s.RevenuePerWaiterSala != null && s.DifficultyScore != null && s.StaffFloor > 0)
            .Select(s => new ShiftSalaRow(s.StaffFloor, s.StaffKitchen, s.RevenuePerWaiterSala!.Value, s.DifficultyScore!.Value))
            .ToListAsync(cancellationToken);

        var bandsDefinition = new List<StaffRevenueComfortBandDto>();
        for (var i = 0; i < BandLimits.Length - 1; i++)
            bandsDefinition.Add(new StaffRevenueComfortBandDto { Min = BandLimits[i], Max = BandLimits[i + 1] == 9999 ? 9999 : BandLimits[i + 1] });

        var bySchemaDict = shifts
            .GroupBy(s => $"{s.StaffFloor}-{s.StaffKitchen}")
            .ToDictionary(g => g.Key, g => g.ToList());

        var schemaDtos = new List<StaffRevenueComfortSchemaDto>();
        foreach (var schemaKey in AllowedSchemas)
        {
            if (!bySchemaDict.TryGetValue(schemaKey, out var list))
                list = new List<ShiftSalaRow>();
            if (minShifts.HasValue && list.Count < minShifts.Value)
            {
                schemaDtos.Add(new StaffRevenueComfortSchemaDto { Schema = schemaKey, Bands = new List<StaffRevenueComfortBandItemDto>(), ComfortLimitApprox = null });
                continue;
            }

            var bandItems = new List<StaffRevenueComfortBandItemDto>();
            decimal? comfortLimitApprox = null;
            foreach (var (min, max) in BandLimits.Zip(BandLimits.Skip(1), (a, b) => (a, b)))
            {
                var inBand = list.Where(s => s.RevenuePerWaiterSala >= min && s.RevenuePerWaiterSala < max).ToList();
                if (inBand.Count == 0) continue;
                var avgDiff = (decimal)inBand.Average(s => s.DifficultyScore);
                var difficultCount = inBand.Count(s => s.DifficultyScore >= 4);
                bandItems.Add(new StaffRevenueComfortBandItemDto
                {
                    Min = min,
                    Max = max == 9999 ? 9999 : max,
                    Count = inBand.Count,
                    AvgDifficulty = Math.Round(avgDiff, 2),
                    PctDifficult = inBand.Count > 0 ? Math.Round(100m * difficultCount / inBand.Count, 1) : null
                });
                if (comfortLimitApprox == null && avgDiff >= 3.5m)
                    comfortLimitApprox = min;
            }

            schemaDtos.Add(new StaffRevenueComfortSchemaDto
            {
                Schema = schemaKey,
                Bands = bandItems,
                ComfortLimitApprox = comfortLimitApprox
            });
        }

        // Cocina: agrupar por StaffKitchen y bandas de RevenuePerWaiterCocina
        var shiftsCocina = await _db.ShiftFeedbacks
            .AsNoTracking()
            .Where(s => s.RevenuePerWaiterCocina != null && s.DifficultyScoreKitchen != null && s.StaffKitchen > 0)
            .Select(s => new { s.StaffKitchen, s.RevenuePerWaiterCocina, s.DifficultyScoreKitchen })
            .ToListAsync(cancellationToken);

        var byCocina = shiftsCocina
            .GroupBy(s => s.StaffKitchen.ToString())
            .OrderBy(g => g.Key)
            .ToList();

        var cocinaDtos = new List<StaffRevenueComfortSchemaDto>();
        foreach (var grp in byCocina)
        {
            var schemaKey = grp.Key;
            var list = grp.ToList();
            if (minShifts.HasValue && list.Count < minShifts.Value) continue;

            var bandItems = new List<StaffRevenueComfortBandItemDto>();
            decimal? comfortLimitApprox = null;
            foreach (var (min, max) in BandLimits.Zip(BandLimits.Skip(1), (a, b) => (a, b)))
            {
                var inBand = list.Where(s => s.RevenuePerWaiterCocina >= min && s.RevenuePerWaiterCocina < max).ToList();
                if (inBand.Count == 0) continue;
                var avgDiff = (decimal)inBand.Average(s => s.DifficultyScoreKitchen!.Value);
                var difficultCount = inBand.Count(s => s.DifficultyScoreKitchen >= 4);
                bandItems.Add(new StaffRevenueComfortBandItemDto
                {
                    Min = min,
                    Max = max == 9999 ? 9999 : max,
                    Count = inBand.Count,
                    AvgDifficulty = Math.Round(avgDiff, 2),
                    PctDifficult = inBand.Count > 0 ? Math.Round(100m * difficultCount / inBand.Count, 1) : null
                });
                if (comfortLimitApprox == null && avgDiff >= 3.5m)
                    comfortLimitApprox = min;
            }

            cocinaDtos.Add(new StaffRevenueComfortSchemaDto
            {
                Schema = schemaKey,
                Bands = bandItems,
                ComfortLimitApprox = comfortLimitApprox
            });
        }

        return new StaffRevenueComfortResult
        {
            Schemas = schemaDtos,
            CocinaSchemas = cocinaDtos,
            BandsDefinition = bandsDefinition
        };
    }
}
