using System.Security.Cryptography;
using System.Text;

namespace Abg.Data.Paymongo;

/// <summary>
/// Verifies the Paymongo-Signature webhook header: "t=&lt;timestamp&gt;,te=&lt;test sig&gt;,li=&lt;live sig&gt;".
/// The signature is HMAC-SHA256 over "&lt;timestamp&gt;.&lt;raw body&gt;" using the webhook secret key.
/// </summary>
public static class PaymongoWebhookVerifier
{
    public static bool IsValid(string? signatureHeader, string payload, string webhookSecretKey)
    {
        if (string.IsNullOrWhiteSpace(signatureHeader) || string.IsNullOrWhiteSpace(webhookSecretKey))
            return false;

        string? timestamp = null;
        var signatures = new List<string>();

        foreach (var part in signatureHeader.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var separatorIndex = part.IndexOf('=');

            if (separatorIndex <= 0)
                continue;

            var key   = part[..separatorIndex];
            var value = part[(separatorIndex + 1)..];

            if (key == "t")
                timestamp = value;
            else if (key is "te" or "li")
                signatures.Add(value);
        }

        if (timestamp is null || signatures.Count == 0)
            return false;

        var expected = ComputeSignature(timestamp, payload, webhookSecretKey);

        return signatures.Any(actual =>
            CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(expected),
                Encoding.UTF8.GetBytes(actual)));
    }

    public static string ComputeSignature(string timestamp, string payload, string webhookSecretKey)
    {
        var bytes = HMACSHA256.HashData(
            Encoding.UTF8.GetBytes(webhookSecretKey),
            Encoding.UTF8.GetBytes($"{timestamp}.{payload}"));

        return Convert.ToHexStringLower(bytes);
    }
}
