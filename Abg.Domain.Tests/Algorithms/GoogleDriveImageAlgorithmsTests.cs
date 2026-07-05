using Abg.Domain.Algorithms;

namespace Abg.Domain.Tests.Algorithms;

public class GoogleDriveImageAlgorithmsTests
{
    [Theory]
    [InlineData("https://drive.google.com/file/d/abc_123-XYZ/view")]
    [InlineData("https://drive.google.com/open?id=abc_123-XYZ")]
    [InlineData("https://drive.google.com/uc?export=view&id=abc_123-XYZ")]
    [InlineData("https://drive.google.com/thumbnail?id=abc_123-XYZ")]
    public void GetImageUrl_converts_drive_links_to_thumbnail_form(string url)
        => Assert.Equal("https://drive.google.com/thumbnail?id=abc_123-XYZ&sz=w1200", url.GetImageUrl());

    [Theory]
    [InlineData("https://example.com/image.jpg")]
    [InlineData("/imgs/designs/simple/Simple1.jpg")]
    public void GetImageUrl_leaves_non_drive_urls_untouched(string url)
        => Assert.Equal(url, url.GetImageUrl());
}
