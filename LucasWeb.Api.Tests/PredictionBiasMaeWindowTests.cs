using LucasWeb.Api.Services;
using Xunit;

namespace LucasWeb.Api.Tests;

public class PredictionBiasMaeWindowTests
{
    [Fact]
    public void UpdateWindow_adds_value_and_computes_average()
    {
        var list = new List<double> { 5, 10 };
        PredictionBiasMaeWindow.UpdateWindow(list, 15, 12, out var avg);
        Assert.Equal(3, list.Count);
        Assert.Equal(10, avg); // (5+10+15)/3
    }

    [Fact]
    public void UpdateWindow_removes_oldest_when_exceeds_window()
    {
        var list = new List<double> { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12 };
        PredictionBiasMaeWindow.UpdateWindow(list, 100, 12, out var avg);
        Assert.Equal(12, list.Count);
        Assert.Equal(2, list[0]); // 1 was removed
        Assert.Equal(100, list[11]);
        // avg = (2+3+4+5+6+7+8+9+10+11+12+100)/12
        Assert.Equal(14.5, avg);
    }

    [Fact]
    public void ParseBiasWithWindow_empty_json_returns_zeros_and_empty_lists()
    {
        PredictionBiasMaeWindow.ParseBiasWithWindow(null, out var avg, out var recent);
        Assert.Equal(7, avg.Length);
        Assert.Equal(7, recent.Length);
        Assert.All(avg, a => Assert.Equal(0, a));
        Assert.All(recent, r => Assert.Empty(r));
    }

    [Fact]
    public void ParseBiasWithWindow_with_recent_overwrites_avg()
    {
        var json = "{\"avg\":[1,2,3,4,5,6,7],\"recent_0\":[10,20],\"recent_1\":[5]}";
        PredictionBiasMaeWindow.ParseBiasWithWindow(json, out var avg, out var recent);
        Assert.Equal(15, avg[0]); // (10+20)/2
        Assert.Equal(5, avg[1]);
        Assert.Equal(2, recent[0].Count);
        Assert.Single(recent[1]);
    }

    [Fact]
    public void SerializeBiasWithWindow_roundtrip()
    {
        var avg = new double[] { 1.5, 2, 3, 4, 5, 6, 7 };
        var recent = new List<double>[7];
        for (var i = 0; i < 7; i++)
            recent[i] = new List<double> { 1 + i, 2 + i };
        var json = PredictionBiasMaeWindow.SerializeBiasWithWindow(avg, recent);
        PredictionBiasMaeWindow.ParseBiasWithWindow(json, out var avg2, out var recent2);
        Assert.Equal(avg.Length, avg2.Length);
        for (var i = 0; i < 7; i++)
        {
            Assert.Equal(avg[i], avg2[i]);
            Assert.Equal(recent[i].Count, recent2[i].Count);
        }
    }

    [Fact]
    public void SerializeMaeWithWindow_roundtrip()
    {
        var avg = new double[] { 100, 200, 150, 0, 0, 0, 0 };
        var recent = new List<double>[7];
        for (var i = 0; i < 7; i++)
            recent[i] = i < 3 ? new List<double> { 90 + i * 10, 110 + i * 10 } : new List<double>();
        var json = PredictionBiasMaeWindow.SerializeMaeWithWindow(avg, recent);
        Assert.Contains("\"avg_mae\"", json);
        Assert.Contains("\"recent_0\"", json);
        PredictionBiasMaeWindow.ParseMaeWithWindow(json, out var avg2, out var recent2);
        Assert.Equal(100, avg2[0]);
        Assert.Equal(2, recent2[0].Count);
    }
}
