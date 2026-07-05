using Abg.Data.Paymongo.Models;
using Abg.Domain.Client;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace Abg.Data.Paymongo;

public sealed class PaymongoQrphClient(HttpClient _httpClient) : IPaymongoClient
{
    // CLAUDE.md §8 mandates a 3-minute payment window (legacy used 300s).
    public const int QrExpirySeconds = 180;

    public static HttpClient ConfigureHttpClient(HttpClient httpClient, PaymongoOptions options)
    {
        var basicToken = Convert.ToBase64String(Encoding.UTF8.GetBytes(options.SecretKey));

        httpClient.BaseAddress = new Uri(options.BaseUrl);
        httpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Basic", basicToken);
        httpClient.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/json"));

        return httpClient;
    }

    public async Task<PaymongoQrphChargeResult> CreateQrphChargeAsync(
        ClientRequest req,
        CancellationToken ct = default)
    {
        var totalAmountPhp      = req.ClientServices.Sum(x => x.ServiceCost);
        var totalAmountCentavos = (int)Math.Round(totalAmountPhp * 100m, MidpointRounding.AwayFromZero);
        string desc             = BuildDescription(req);

        var metadata = new Dictionary<string, string>
        {
            ["client_booking_id"] = req.ClientInformation.ClientBookingId ?? "",
            ["customer_name"]     = $"{req.ClientInformation.FirstName} {req.ClientInformation.LastName}".Trim(),
            ["customer_email"]    = req.ClientInformation.Email ?? "",
            ["service_count"]     = req.ClientServices.Count.ToString(),
            ["services"]          = string.Join(", ", req.ClientServices.Select(s => $"{s.ServiceUid}:{s.ServiceName}"))
        };

        var paymentIntentReq = new
        {
            data = new
            {
                attributes = new
                {
                    amount                 = totalAmountCentavos,
                    currency               = "PHP",
                    capture_type           = "automatic",
                    payment_method_allowed = new[] { "qrph" },
                    description            = desc,
                    statement_descriptor   = "BOOKING",
                    metadata
                }
            }
        };

        var paymentIntentResp = await PostAsync<PaymongoEnvelope<PaymentIntentData>>(
            "payment_intents",
            paymentIntentReq,
            ct);

        var paymentIntentId = paymentIntentResp.Data.Id;

        var paymentMethodReq = new
        {
            data = new
            {
                attributes = new
                {
                    expiry_seconds = QrExpirySeconds,
                    type = "qrph",
                    billing = new
                    {
                        name = $"{req.ClientInformation.FirstName} {req.ClientInformation.LastName}".Trim(),
                        email = req.ClientInformation.Email,
                        phone = req.ClientInformation.ContactNumber,
                        address = new
                        {
                            line1 = "N/A",
                            line2 = "N/A",
                            city = "N/A",
                            state = "N/A",
                            postal_code = "0000",
                            country = "PH"
                        }
                    }
                }
            }
        };

        var paymentMethodResp = await PostAsync<PaymongoEnvelope<PaymentMethodData>>(
            "payment_methods",
            paymentMethodReq,
            ct);

        var paymentMethodId = paymentMethodResp.Data.Id;

        var attachReq = new
        {
            data = new
            {
                attributes = new
                {
                    payment_method = paymentMethodId,
                    client_key = paymentIntentResp.Data.Attributes.ClientKey
                }
            }
        };

        var attachResp = await PostAsync<PaymongoEnvelope<PaymentIntentData>>(
            $"payment_intents/{paymentIntentId}/attach",
            attachReq,
            ct);

        var nextAction = attachResp.Data.Attributes.NextAction;
        var qrCode     = nextAction?.Code;

        return new PaymongoQrphChargeResult
        {
            PaymentIntentId     = attachResp.Data.Id ?? "",
            PaymentIntentStatus = attachResp.Data.Attributes.Status ?? "",
            PaymentMethodId     = paymentMethodId,
            AmountCentavos      = attachResp.Data.Attributes.Amount,
            AmountPhp           = attachResp.Data.Attributes.Amount / 100m,
            QrImageUrl          = qrCode?.ImageUrl ?? "",
            QrCodeId            = qrCode?.Id ?? "",
            QrLabel             = qrCode?.Label ?? "",
            NextActionType      = nextAction?.Type ?? "",
            RawResponse         = JsonSerializer.Serialize(attachResp)
        };
    }

    public static string BuildDescription(ClientRequest payload)
    {
        var services = string.Join(
            ", ",
            payload.ClientServices.Select(x => $"{x.ServiceName} ({x.ServiceDetails})"));

        return $"Appointment booking {payload.ClientInformation.ClientBookingId}: {services}";
    }

    public async Task<string> GetPaymentIntentStatusAsync(string paymentIntentId, CancellationToken ct = default)
    {
        var resp = await GetAsync<PaymongoEnvelope<PaymentIntentData>>(
            $"payment_intents/{paymentIntentId}",
            ct);

        return resp.Data.Attributes.Status ?? "";
    }

    public async Task<string> GetPaymentIntentClientBookingIdAsync(string paymentIntentId, CancellationToken ct = default)
    {
        using var response = await _httpClient.GetAsync($"payment_intents/{paymentIntentId}", ct);

        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(ct);

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        if (!TryGetProperty(root, "data", out var data))
            return "";

        if (!TryGetProperty(data, "attributes", out var attributes))
            return "";

        if (!TryGetProperty(attributes, "metadata", out var metadata))
            return "";

        return GetString(metadata, "client_booking_id");
    }

    private async Task<T> PostAsync<T>(string endPoint, object payload, CancellationToken ct)
    {
              var json    = JsonSerializer.Serialize(payload);
        using var content = new StringContent(json, Encoding.UTF8, "application/json");
        using var resp    = await _httpClient.PostAsync(endPoint, content, ct);
              var body    = await resp.Content.ReadAsStringAsync(ct);

        if (!resp.IsSuccessStatusCode)
            throw body.CreatePaymongoException(resp.StatusCode);

        var res = JsonSerializer.Deserialize<T>(body);

        return res is null ? throw new InvalidOperationException("Unable to deserialize PayMongo response.") : res;
    }

    private async Task<T> GetAsync<T>(string endPoint, CancellationToken ct)
    {
        using var resp = await _httpClient.GetAsync(endPoint, ct);
        var body = await resp.Content.ReadAsStringAsync(ct);

        if (!resp.IsSuccessStatusCode)
            throw body.CreatePaymongoException(resp.StatusCode);

        var res = JsonSerializer.Deserialize<T>(body);

        return res is null ? throw new InvalidOperationException("Unable to deserialize PayMongo response.") : res;
    }

    private static bool TryGetProperty(JsonElement element, string propertyName, out JsonElement property)
    {
        property = default;

        if (element.ValueKind != JsonValueKind.Object)
            return false;

        return element.TryGetProperty(propertyName, out property);
    }

    private static string GetString(JsonElement element, string propertyName)
    {
        if (element.ValueKind != JsonValueKind.Object)
            return "";

        if (!element.TryGetProperty(propertyName, out var property))
            return "";

        if (property.ValueKind == JsonValueKind.Null || property.ValueKind == JsonValueKind.Undefined)
            return "";

        return property.ValueKind == JsonValueKind.String
            ? property.GetString() ?? ""
            : property.ToString();
    }
}
