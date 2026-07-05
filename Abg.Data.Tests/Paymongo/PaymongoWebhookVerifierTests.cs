using Abg.Data.Paymongo;

namespace Abg.Data.Tests.Paymongo;

public class PaymongoWebhookVerifierTests
{
    const string Secret  = "whsk_test_secret";
    const string Payload = """{"data":{"id":"evt_1","attributes":{"type":"payment.paid"}}}""";

    [Fact]
    public void Valid_test_mode_signature_is_accepted()
    {
        var signature = PaymongoWebhookVerifier.ComputeSignature("1720000000", Payload, Secret);
        var header    = $"t=1720000000,te={signature},li=";

        Assert.True(PaymongoWebhookVerifier.IsValid(header, Payload, Secret));
    }

    [Fact]
    public void Valid_live_mode_signature_is_accepted()
    {
        var signature = PaymongoWebhookVerifier.ComputeSignature("1720000000", Payload, Secret);
        var header    = $"t=1720000000,te=,li={signature}";

        Assert.True(PaymongoWebhookVerifier.IsValid(header, Payload, Secret));
    }

    [Fact]
    public void Tampered_payload_is_rejected()
    {
        var signature = PaymongoWebhookVerifier.ComputeSignature("1720000000", Payload, Secret);
        var header    = $"t=1720000000,te={signature}";

        Assert.False(PaymongoWebhookVerifier.IsValid(header, Payload + "tampered", Secret));
    }

    [Fact]
    public void Wrong_secret_is_rejected()
    {
        var signature = PaymongoWebhookVerifier.ComputeSignature("1720000000", Payload, "another-secret");
        var header    = $"t=1720000000,te={signature}";

        Assert.False(PaymongoWebhookVerifier.IsValid(header, Payload, Secret));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("garbage")]
    [InlineData("t=123")]
    public void Missing_or_malformed_headers_are_rejected(string? header)
        => Assert.False(PaymongoWebhookVerifier.IsValid(header, Payload, Secret));
}
