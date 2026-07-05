using Abg.Domain.Algorithms;

namespace Abg.Domain.Tests.Algorithms;

public class ServiceSectionCalendarAlgorithmsTests
{
    [Fact]
    public void BuildCalendarDays_returns_42_day_grid_starting_on_sunday()
    {
        var days = ServiceSectionCalendarAlgorithms.BuildCalendarDays(
            new DateTime(2026, 7, 1),
            ["Monday"],
            _ => true);

        Assert.Equal(42, days.Count);
        Assert.Equal(DayOfWeek.Sunday, days[0].Date.DayOfWeek);
        Assert.Equal(new DateTime(2026, 6, 28), days[0].Date);
    }

    [Fact]
    public void BuildCalendarDays_only_allows_configured_days_with_available_slots()
    {
        var days = ServiceSectionCalendarAlgorithms.BuildCalendarDays(
            new DateTime(2026, 7, 1),
            ["Monday", "Tuesday"],
            date => date.DayOfWeek == DayOfWeek.Monday);

        foreach (var day in days)
        {
            var expected = day.Date.DayOfWeek == DayOfWeek.Monday;
            Assert.Equal(expected, day.IsAllowed);
            Assert.Equal(expected ? "Available" : "No available time", day.Title);
        }
    }

    [Fact]
    public void BuildCalendarDays_flags_current_month_days()
    {
        var days = ServiceSectionCalendarAlgorithms.BuildCalendarDays(
            new DateTime(2026, 7, 15),
            [],
            _ => true);

        Assert.Equal(31, days.Count(d => d.IsCurrentMonth));
        Assert.All(days.Where(d => d.IsCurrentMonth), d => Assert.Equal(7, d.Date.Month));
    }

    [Fact]
    public void BuildCalendarDays_handles_null_allowed_days()
    {
        var days = ServiceSectionCalendarAlgorithms.BuildCalendarDays(
            new DateTime(2026, 7, 1),
            null!,
            _ => true);

        Assert.All(days, d => Assert.False(d.IsAllowed));
    }
}
