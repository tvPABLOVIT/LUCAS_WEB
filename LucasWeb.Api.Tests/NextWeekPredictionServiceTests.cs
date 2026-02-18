using LucasWeb.Api.Services;
using Xunit;

namespace LucasWeb.Api.Tests;

public class NextWeekPredictionServiceTests
{
    [Theory]
    [InlineData("2026-02-09", "2026-02-16")] // lunes -> siguiente lunes
    [InlineData("2026-02-10", "2026-02-16")] // martes
    [InlineData("2026-02-15", "2026-02-16")] // domingo
    [InlineData("2026-02-16", "2026-02-23")] // ya lunes -> siguiente
    public void GetNextMonday_returns_next_monday(string fromDate, string expectedMonday)
    {
        var from = DateTime.Parse(fromDate);
        var expected = DateTime.Parse(expectedMonday);
        var result = NextWeekPredictionService.GetNextMonday(from);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void GetNextMonday_from_sunday_returns_next_week_monday()
    {
        var from = new DateTime(2026, 2, 15); // domingo
        var result = NextWeekPredictionService.GetNextMonday(from);
        Assert.Equal(DayOfWeek.Monday, result.DayOfWeek);
        Assert.Equal(16, result.Day);
        Assert.Equal(2026, result.Year);
    }
}
