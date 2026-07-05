using FunctionApp1.Auth;
using System.Text;
using System.Text.Json;

namespace FunctionApp1.Tests.Auth;

public class SwaPrincipalParserTests
{
    static string Encode(object principal)
        => Convert.ToBase64String(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(principal)));

    [Fact]
    public void Parses_authenticated_google_principal()
    {
        var header = Encode(new
        {
            identityProvider = "google",
            userId           = "abc123",
            userDetails      = "ana@example.com",
            userRoles        = new[] { "anonymous", "authenticated" }
        });

        var principal = SwaPrincipalParser.Parse(header);

        Assert.NotNull(principal);
        Assert.Equal("google", principal.IdentityProvider);
        Assert.Equal("google:abc123", principal.UserKey);
        Assert.Equal("ana@example.com", principal.Email);
        Assert.True(principal.IsAuthenticated);
    }

    [Fact]
    public void Non_email_user_details_yield_empty_email()
    {
        var header = Encode(new
        {
            identityProvider = "facebook",
            userId           = "fb1",
            userDetails      = "Ana Cruz",
            userRoles        = new[] { "authenticated" }
        });

        var principal = SwaPrincipalParser.Parse(header);

        Assert.NotNull(principal);
        Assert.Equal("", principal.Email);
        Assert.Equal("Ana Cruz", principal.UserDetails);
    }

    [Fact]
    public void Anonymous_only_principal_is_rejected()
    {
        var header = Encode(new
        {
            identityProvider = "google",
            userId           = "abc123",
            userDetails      = "ana@example.com",
            userRoles        = new[] { "anonymous" }
        });

        Assert.Null(SwaPrincipalParser.Parse(header));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-base64!!!")]
    [InlineData("aGVsbG8=")]
    public void Missing_or_malformed_headers_return_null(string? header)
        => Assert.Null(SwaPrincipalParser.Parse(header));
}
