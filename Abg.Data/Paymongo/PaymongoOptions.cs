namespace Abg.Data.Paymongo;

public sealed class PaymongoOptions
{
    public string SecretKey        { get; set; } = "";
    public string BaseUrl          { get; set; } = "https://api.paymongo.com/v1/";
    public string WebhookSecretKey { get; set; } = "";
}
