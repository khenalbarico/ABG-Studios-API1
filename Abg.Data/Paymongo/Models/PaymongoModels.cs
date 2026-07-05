using System.Text.Json.Serialization;

namespace Abg.Data.Paymongo.Models;

public sealed class PaymongoEnvelope<T>
{
    [JsonPropertyName("data")]
    public T Data { get; set; } = default!;
}

public sealed class PaymentIntentData
{
    [JsonPropertyName("id")]
    public string?                 Id         { get; set; }

    [JsonPropertyName("attributes")]
    public PaymentIntentAttributes Attributes { get; set; } = new();
}

public sealed class PaymentIntentAttributes
{
    [JsonPropertyName("amount")]
    public long                Amount     { get; set; }

    [JsonPropertyName("status")]
    public string?             Status     { get; set; }

    [JsonPropertyName("client_key")]
    public string?             ClientKey  { get; set; }

    [JsonPropertyName("next_action")]
    public PaymongoNextAction? NextAction { get; set; }
}

public sealed class PaymentMethodData
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;
}

public sealed class PaymongoNextAction
{
    [JsonPropertyName("type")]
    public string?         Type { get; set; }

    [JsonPropertyName("code")]
    public PaymongoQrCode? Code { get; set; }
}

public sealed class PaymongoQrCode
{
    [JsonPropertyName("id")]
    public string? Id       { get; set; }

    [JsonPropertyName("amount")]
    public long Amount      { get; set; }

    [JsonPropertyName("image_url")]
    public string? ImageUrl { get; set; }

    [JsonPropertyName("label")]
    public string? Label    { get; set; }
}

public sealed class PaymongoQrphChargeResult
{
    public string  PaymentIntentId     { get; set; } = string.Empty;
    public string  PaymentIntentStatus { get; set; } = string.Empty;
    public string  PaymentMethodId     { get; set; } = string.Empty;
    public long    AmountCentavos      { get; set; }
    public decimal AmountPhp           { get; set; }
    public string  QrCodeId            { get; set; } = string.Empty;
    public string  QrImageUrl          { get; set; } = string.Empty;
    public string  QrLabel             { get; set; } = string.Empty;
    public string  NextActionType      { get; set; } = string.Empty;
    public string  RawResponse         { get; set; } = string.Empty;
}
