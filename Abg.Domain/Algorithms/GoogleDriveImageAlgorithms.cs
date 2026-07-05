using System.Text.RegularExpressions;

namespace Abg.Domain.Algorithms;

public static class GoogleDriveImageAlgorithms
{
    public static string GetImageUrl(this string url)
    {
        if (!url.Contains("drive.google.com", StringComparison.OrdinalIgnoreCase))
            return url;

        var patterns =
            new[]
            {
                @"\/file\/d\/([a-zA-Z0-9_-]+)",
                @"[?&]id=([a-zA-Z0-9_-]+)",
                @"\/open\?id=([a-zA-Z0-9_-]+)",
                @"\/uc\?export=view&id=([a-zA-Z0-9_-]+)",
                @"\/thumbnail\?id=([a-zA-Z0-9_-]+)"
            };

        foreach (var pattern in patterns)
        {
            var match = Regex.Match(url, pattern);
            if (match.Success && match.Groups.Count > 1)
            {
                var fileId = match.Groups[1].Value;
                return $"https://drive.google.com/thumbnail?id={fileId}&sz=w1200";
            }
        }

        return url;
    }
}
