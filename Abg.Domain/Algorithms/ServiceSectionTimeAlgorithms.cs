using System.Globalization;
using System.Text.RegularExpressions;

namespace Abg.Domain.Algorithms;

public static class ServiceSectionTimeAlgorithms
{
    public static DateTime CombineDateAndTime(DateTime date, string timeRange)
    {
        var startText = ExtractStartTimeText(timeRange);

        if (DateTime.TryParse(startText, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
            return new DateTime(date.Year, date.Month, date.Day, parsed.Hour, parsed.Minute, 0);

        if (DateTime.TryParseExact(startText, ["h tt", "hh tt", "h:mm tt", "hh:mm tt"], CultureInfo.InvariantCulture, DateTimeStyles.None, out parsed))
            return new DateTime(date.Year, date.Month, date.Day, parsed.Hour, parsed.Minute, 0);

        return date;
    }

    public static string ExtractStartTimeText(string timeRange)
    {
        var normalized = timeRange.Trim().ToUpperInvariant();
        var firstPart  = normalized.Split('-')[0].Trim();

        if (Regex.IsMatch(firstPart, @"^\d{1,2}$"))
            return $"{firstPart}:00 AM";

        if (Regex.IsMatch(firstPart, @"^\d{1,2}\s*(AM|PM)$", RegexOptions.IgnoreCase))
            return Regex.Replace(firstPart, @"^\s*(\d{1,2})\s*(AM|PM)\s*$", "$1:00 $2", RegexOptions.IgnoreCase);

        return firstPart;
    }

    public static string NormalizeTimeRangeLabel(string timeRange)
    {
        var parts = timeRange.Split('-', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
            return timeRange.Trim();

        var startRaw = parts[0];
        var endRaw   = parts.Length > 1 ? parts[1] : "";

        var endMeridiemMatch   = Regex.Match(endRaw, @"\b(AM|PM)\b", RegexOptions.IgnoreCase);
        var startMeridiemMatch = Regex.Match(startRaw, @"\b(AM|PM)\b", RegexOptions.IgnoreCase);

        if (!startMeridiemMatch.Success && endMeridiemMatch.Success)
            startRaw = $"{startRaw} {endMeridiemMatch.Value}";

        var startFormatted = NormalizeSingleTimeLabel(startRaw);
        var endFormatted   = NormalizeSingleTimeLabel(endRaw);

        if (string.IsNullOrWhiteSpace(endFormatted))
            return startFormatted;

        return $"{startFormatted}-{endFormatted}";
    }

    public static string NormalizeSingleTimeLabel(string value)
    {
        var cleaned = Regex.Replace(value.Trim(), @"\s+", " ").ToUpperInvariant();

        if (DateTime.TryParse(cleaned, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
            return parsed.ToString("h:mm tt", CultureInfo.InvariantCulture);

        if (DateTime.TryParseExact(cleaned, ["h tt", "hh tt", "h:mm tt", "hh:mm tt"], CultureInfo.InvariantCulture, DateTimeStyles.None, out parsed))
            return parsed.ToString("h:mm tt", CultureInfo.InvariantCulture);

        if (Regex.IsMatch(cleaned, @"^\d{1,2}$"))
            return $"{int.Parse(cleaned)}:00 AM";

        if (Regex.IsMatch(cleaned, @"^\d{1,2}\s*(AM|PM)$", RegexOptions.IgnoreCase))
        {
            var match = Regex.Match(cleaned, @"^(\d{1,2})\s*(AM|PM)$", RegexOptions.IgnoreCase);
            return $"{int.Parse(match.Groups[1].Value)}:00 {match.Groups[2].Value.ToUpperInvariant()}";
        }

        return cleaned;
    }
}
