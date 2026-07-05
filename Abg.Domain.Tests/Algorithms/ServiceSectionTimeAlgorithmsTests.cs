using Abg.Domain.Algorithms;

namespace Abg.Domain.Tests.Algorithms;

public class ServiceSectionTimeAlgorithmsTests
{
    static readonly DateTime Day = new(2026, 7, 10);

    [Theory]
    [InlineData("10:00 AM - 11:00 AM", 10, 0)]
    [InlineData("1:30 PM - 2:30 PM", 13, 30)]
    [InlineData("10 AM - 11 AM", 10, 0)]
    [InlineData("10", 10, 0)]
    [InlineData("2 PM", 14, 0)]
    public void CombineDateAndTime_parses_start_time_variants(string timeRange, int hour, int minute)
    {
        var combined = ServiceSectionTimeAlgorithms.CombineDateAndTime(Day, timeRange);

        Assert.Equal(new DateTime(2026, 7, 10, hour, minute, 0), combined);
    }

    [Fact]
    public void CombineDateAndTime_falls_back_to_date_when_unparseable()
        => Assert.Equal(Day, ServiceSectionTimeAlgorithms.CombineDateAndTime(Day, "not a time"));

    [Theory]
    [InlineData("10:00 AM - 11:00 AM", "10:00 AM")]
    [InlineData("10 - 11", "10:00 AM")]
    [InlineData("2 PM - 3 PM", "2:00 PM")]
    public void ExtractStartTimeText_normalizes_start(string timeRange, string expected)
        => Assert.Equal(expected, ServiceSectionTimeAlgorithms.ExtractStartTimeText(timeRange));

    [Theory]
    [InlineData("10AM-11AM", "10:00 AM-11:00 AM")]
    [InlineData("10:00 am - 11:00 am", "10:00 AM-11:00 AM")]
    [InlineData("1 - 2 PM", "1:00 PM-2:00 PM")]
    [InlineData("10:00 AM", "10:00 AM")]
    public void NormalizeTimeRangeLabel_produces_canonical_labels(string input, string expected)
        => Assert.Equal(expected, ServiceSectionTimeAlgorithms.NormalizeTimeRangeLabel(input));

    [Theory]
    [InlineData("10", "10:00 AM")]
    [InlineData("10 pm", "10:00 PM")]
    [InlineData("9:15 AM", "9:15 AM")]
    public void NormalizeSingleTimeLabel_handles_hour_only_values(string input, string expected)
        => Assert.Equal(expected, ServiceSectionTimeAlgorithms.NormalizeSingleTimeLabel(input));
}
