using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Abg.Data.Paymongo;

internal static class PaymongoErrors
{
    public static Exception CreatePaymongoException(this string respBody, HttpStatusCode statusCode)
    {
        try
        {
            var error = JsonSerializer.Deserialize<PaymongoErrorResp>(respBody);
            var first = error?.Errors.FirstOrDefault();

            if (first is not null)
            {
                return new InvalidOperationException(
                    $"PayMongo error ({(int)statusCode}): {first.Code} - {first.Detail}");
            }
        }
        catch
        {
        }

        return new InvalidOperationException($"PayMongo error ({(int)statusCode}): {respBody}");
    }
}

internal sealed class PaymongoErrorResp
{
    [JsonPropertyName("errors")]
    public List<PaymongoErrorItem> Errors { get; set; } = [];
}

internal sealed class PaymongoErrorItem
{
    [JsonPropertyName("code")]
    public string Code   { get; set; } = string.Empty;

    [JsonPropertyName("detail")]
    public string Detail { get; set; } = string.Empty;
}
