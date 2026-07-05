using System.Text;
using System.Text.Json;

namespace FunctionApp1.Auth;

/// <summary>
/// Parses the x-ms-client-principal header that Azure Static Web Apps
/// forwards to its linked Functions backend.
/// </summary>
public static class SwaPrincipalParser
{
    public const string HeaderName = "x-ms-client-principal";

    static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static ClientPrincipal? Parse(string? headerValue)
    {
        if (string.IsNullOrWhiteSpace(headerValue))
            return null;

        try
        {
            var json      = Encoding.UTF8.GetString(Convert.FromBase64String(headerValue));
            var principal = JsonSerializer.Deserialize<ClientPrincipal>(json, JsonOptions);

            return principal is { IsAuthenticated: true } ? principal : null;
        }
        catch (FormatException)
        {
            return null;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
