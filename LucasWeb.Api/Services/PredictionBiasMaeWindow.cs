using System.Text.Json;

namespace LucasWeb.Api.Services;

/// <summary>Lógica de ventana móvil para bias y MAE por DOW; expuesta para tests unitarios.</summary>
public static class PredictionBiasMaeWindow
{
    public const int DefaultWindowSize = 12;

    public static void UpdateWindow(List<double> list, double newValue, int windowSize, out double avg)
    {
        list.Add(newValue);
        while (list.Count > windowSize) list.RemoveAt(0);
        avg = list.Count > 0 ? list.Average() : 0;
    }

    public static void ParseBiasWithWindow(string? json, out double[] avg, out List<double>[] recent)
    {
        avg = new double[7];
        recent = new List<double>[7];
        for (var i = 0; i < 7; i++) recent[i] = new List<double>();
        if (string.IsNullOrWhiteSpace(json)) return;
        try
        {
            var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("avg", out var a))
                for (var i = 0; i < 7 && i < a.GetArrayLength(); i++) avg[i] = a[i].GetDouble();
            for (var i = 0; i < 7; i++)
            {
                var key = "recent_" + i;
                if (doc.RootElement.TryGetProperty(key, out var arr) && arr.ValueKind == JsonValueKind.Array)
                {
                    recent[i] = new List<double>();
                    foreach (var el in arr.EnumerateArray())
                        if (el.TryGetDouble(out var v)) recent[i].Add(v);
                    if (recent[i].Count > 0) avg[i] = recent[i].Average();
                }
            }
        }
        catch { }
    }

    public static void ParseMaeWithWindow(string? json, out double[] avg, out List<double>[] recent)
    {
        avg = new double[7];
        recent = new List<double>[7];
        for (var i = 0; i < 7; i++) recent[i] = new List<double>();
        if (string.IsNullOrWhiteSpace(json)) return;
        try
        {
            var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("avg_mae", out var a))
                for (var i = 0; i < 7 && i < a.GetArrayLength(); i++) avg[i] = a[i].GetDouble();
            for (var i = 0; i < 7; i++)
            {
                var key = "recent_" + i;
                if (doc.RootElement.TryGetProperty(key, out var arr) && arr.ValueKind == JsonValueKind.Array)
                {
                    recent[i] = new List<double>();
                    foreach (var el in arr.EnumerateArray())
                        if (el.TryGetDouble(out var v)) recent[i].Add(v);
                    if (recent[i].Count > 0) avg[i] = recent[i].Average();
                }
            }
        }
        catch { }
    }

    public static string SerializeBiasWithWindow(double[] avg, List<double>[] recent)
    {
        var obj = new Dictionary<string, object> { ["avg"] = avg };
        for (var i = 0; i < 7; i++)
            obj["recent_" + i] = recent[i].ToArray();
        return JsonSerializer.Serialize(obj);
    }

    public static string SerializeMaeWithWindow(double[] avg, List<double>[] recent)
    {
        var obj = new Dictionary<string, object> { ["avg_mae"] = avg };
        for (var i = 0; i < 7; i++)
            obj["recent_" + i] = recent[i].ToArray();
        return JsonSerializer.Serialize(obj);
    }
}
