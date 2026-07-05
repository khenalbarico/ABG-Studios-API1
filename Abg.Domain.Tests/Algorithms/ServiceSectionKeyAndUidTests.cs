using Abg.Domain.__Base__;
using Abg.Domain.Algorithms;
using Abg.Domain.Utilities;

namespace Abg.Domain.Tests.Algorithms;

public class ServiceSectionKeyAlgorithmsTests
{
    [Fact]
    public void BuildCardKey_combines_title_uid_details_and_cost()
    {
        var svc = new BaseSvcStructure { Uid = "NAS-123", Details = "Gel polish", Cost = 500m };

        Assert.Equal("Nails|NAS-123|Gel polish|500", ServiceSectionKeyAlgorithms.BuildCardKey("Nails", svc));
    }
}

public class UidGeneratorTests
{
    [Theory]
    [InlineData("Nails", "NAS")]
    [InlineData("Lash", "LAS")]
    [InlineData("Eyebrows", "EYS")]
    [InlineData("Footspa", "FOS")]
    [InlineData("Pedicure", "PES")]
    [InlineData("Unknown", "SVS")]
    public void GenerateUid_uses_category_prefix_and_three_digits(string category, string prefix)
        => Assert.Matches($@"^{prefix}-\d{{3}}$", category.GenerateUid());

    [Theory]
    [InlineData("nails", "Nails")]
    [InlineData("LASH", "Lash")]
    [InlineData("eyebrows", "Eyebrows")]
    [InlineData("Footspa", "Footspa")]
    [InlineData("pedicure", "Pedicure")]
    [InlineData("garbage", "Nails")]
    [InlineData(null, "Nails")]
    public void NormalizeCategory_maps_to_canonical_names(string? category, string expected)
        => Assert.Equal(expected, category!.NormalizeCategory());
}
