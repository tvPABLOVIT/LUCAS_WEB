using LucasWeb.Api.Data;
using LucasWeb.Api.DTOs;
using Microsoft.EntityFrameworkCore;

namespace LucasWeb.Api.Services;

/// <summary>Agregados por esquema de personal (sala-cocina) y banda de facturación por camarero para "límite cómodo". Bandas dinámicas por percentiles cuando hay suficientes datos.</summary>
public class StaffRevenueComfortService : IStaffRevenueComfortService
{
    private readonly AppDbContext _db;

    private const decimal DifficultyThreshold = 3.5m;
    private const int DifficultScoreMin = 4;

    private static readonly decimal[] FixedBandLimits = { 0, 400, 500, 600, 700, 800, 1000, 9999 };
    private static readonly string[] AllowedSchemas = { "1-1", "1-2", "2-1", "2-2", "2-3", "3-2", "3-3" };

    private sealed record ShiftSalaRow(int StaffFloor, int StaffKitchen, decimal RevenuePerWaiterSala, decimal DifficultyScore);

    public StaffRevenueComfortService(AppDbContext db) => _db = db;

    private static decimal[]? ComputeBandLimitsFromPercentiles(IReadOnlyList<decimal> sortedValues, int roundTo = 50)
    {
        if (sortedValues.Count < 5) return null;
        var p = new[] { 0.0, 0.2, 0.4, 0.6, 0.8, 1.0 };
        var limits = new List<decimal> { 0 };
        for (var i = 1; i < p.Length; i++)
        {
            var idx = (int)Math.Round(p[i] * (sortedValues.Count - 1));
            idx = Math.Clamp(idx, 0, sortedValues.Count - 1);
            var v = sortedValues[idx];
            var rounded = roundTo > 0 ? Math.Round(v / roundTo) * roundTo : v;
            if (rounded < 0) rounded = 0;
            if (limits.Count == 0 || rounded > limits[^1])
                limits.Add(rounded);
        }
        limits.Sort();
        limits.Add(9999);
        return limits.Distinct().ToArray();
    }

    public async Task<StaffRevenueComfortResult> GetAggregatesAsync(int? minShifts = null, CancellationToken cancellationToken = default)
    {
        var shifts = await _db.ShiftFeedbacks
            .AsNoTracking()
            .Where(s => s.RevenuePerWaiterSala != null && s.DifficultyScore != null && s.StaffFloor > 0)
            .Select(s => new ShiftSalaRow(s.StaffFloor, s.StaffKitchen, s.RevenuePerWaiterSala!.Value, s.DifficultyScore!.Value))
            .ToListAsync(cancellationToken);

        var salaValues = shifts.Select(s => s.RevenuePerWaiterSala).OrderBy(x => x).ToList();
        var bandLimitsSala = salaValues.Count >= 5 ? ComputeBandLimitsFromPercentiles(salaValues) ?? FixedBandLimits : FixedBandLimits;
        var bandsSource = salaValues.Count >= 5 ? "dynamic" : "fixed";

        var bandsDefinition = new List<StaffRevenueComfortBandDto>();
        for (var i = 0; i < bandLimitsSala.Length - 1; i++)
            bandsDefinition.Add(new StaffRevenueComfortBandDto { Min = bandLimitsSala[i], Max = bandLimitsSala[i + 1] == 9999 ? 9999 : bandLimitsSala[i + 1] });

        var bySchemaDict = shifts.GroupBy(s => $"{s.StaffFloor}-{s.StaffKitchen}").ToDictionary(g => g.Key, g => g.ToList());

        var schemaDtos = new List<StaffRevenueComfortSchemaDto>();
        foreach (var schemaKey in AllowedSchemas)
        {
            if (!bySchemaDict.TryGetValue(schemaKey, out var list)) list = new List<ShiftSalaRow>();
            if (minShifts.HasValue && list.Count < minShifts.Value)
            {
                schemaDtos.Add(new StaffRevenueComfortSchemaDto { Schema = schemaKey, Bands = new List<StaffRevenueComfortBandItemDto>(), ComfortLimitApprox = null });
                continue;
            }

            var bandItems = new List<StaffRevenueComfortBandItemDto>();
            decimal? comfortLimitApprox = null;
            for (var i = 0; i < bandLimitsSala.Length - 1; i++)
            {
                var min = bandLimitsSala[i];
                var max = bandLimitsSala[i + 1];
                var inBand = list.Where(s => s.RevenuePerWaiterSala >= min && s.RevenuePerWaiterSala < max).ToList();
                if (inBand.Count == 0) continue;
                var avgDiff = (decimal)inBand.Average(s => s.DifficultyScore);
                var difficultCount = inBand.Count(s => s.DifficultyScore >= DifficultScoreMin);
                bandItems.Add(new StaffRevenueComfortBandItemDto
                {
                    Min = min,
                    Max = max == 9999 ? 9999 : max,
                    Count = inBand.Count,
                    AvgDifficulty = Math.Round(avgDiff, 2),
                    PctDifficult = inBand.Count > 0 ? Math.Round(100m * difficultCount / inBand.Count, 1) : null
                });
                if (comfortLimitApprox == null && avgDiff >= DifficultyThreshold)
                    comfortLimitApprox = min;
            }

            schemaDtos.Add(new StaffRevenueComfortSchemaDto { Schema = schemaKey, Bands = bandItems, ComfortLimitApprox = comfortLimitApprox });
        }

        var shiftsCocina = await _db.ShiftFeedbacks
            .AsNoTracking()
            .Where(s => s.RevenuePerWaiterCocina != null && s.DifficultyScoreKitchen != null && s.StaffKitchen > 0)
            .Select(s => new { s.StaffKitchen, s.RevenuePerWaiterCocina, s.DifficultyScoreKitchen })
            .ToListAsync(cancellationToken);

        var cocinaValues = shiftsCocina.Select(s => s.RevenuePerWaiterCocina!.Value).OrderBy(x => x).ToList();
        var bandLimitsCocina = cocinaValues.Count >= 5 ? ComputeBandLimitsFromPercentiles(cocinaValues) ?? FixedBandLimits : FixedBandLimits;
        if (cocinaValues.Count >= 5) bandsSource = "dynamic";

        var byCocina = shiftsCocina.GroupBy(s => s.StaffKitchen.ToString()).OrderBy(g => g.Key).ToList();

        var cocinaDtos = new List<StaffRevenueComfortSchemaDto>();
        foreach (var grp in byCocina)
        {
            var schemaKey = grp.Key;
            var list = grp.ToList();
            if (minShifts.HasValue && list.Count < minShifts.Value) continue;

            var bandItems = new List<StaffRevenueComfortBandItemDto>();
            decimal? comfortLimitApprox = null;
            for (var i = 0; i < bandLimitsCocina.Length - 1; i++)
            {
                var min = bandLimitsCocina[i];
                var max = bandLimitsCocina[i + 1];
                var inBand = list.Where(s => s.RevenuePerWaiterCocina >= min && s.RevenuePerWaiterCocina < max).ToList();
                if (inBand.Count == 0) continue;
                var avgDiff = (decimal)inBand.Average(s => s.DifficultyScoreKitchen!.Value);
                var difficultCount = inBand.Count(s => s.DifficultyScoreKitchen >= DifficultScoreMin);
                bandItems.Add(new StaffRevenueComfortBandItemDto
                {
                    Min = min,
                    Max = max == 9999 ? 9999 : max,
                    Count = inBand.Count,
                    AvgDifficulty = Math.Round(avgDiff, 2),
                    PctDifficult = inBand.Count > 0 ? Math.Round(100m * difficultCount / inBand.Count, 1) : null
                });
                if (comfortLimitApprox == null && avgDiff >= DifficultyThreshold)
                    comfortLimitApprox = min;
            }

            cocinaDtos.Add(new StaffRevenueComfortSchemaDto { Schema = schemaKey, Bands = bandItems, ComfortLimitApprox = comfortLimitApprox });
        }

        return new StaffRevenueComfortResult
        {
            Schemas = schemaDtos,
            CocinaSchemas = cocinaDtos,
            BandsDefinition = bandsDefinition,
            DifficultyThreshold = DifficultyThreshold,
            BandsSource = bandsSource,
            TotalShiftsSala = shifts.Count,
            TotalShiftsCocina = shiftsCocina.Count
        };
    }
}
