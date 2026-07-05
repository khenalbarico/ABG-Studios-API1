using Abg.Data.Paymongo;
using Abg.Data.Tests.TestSupport;
using Abg.Domain.Client;
using System.Net;
using System.Text.Json;

namespace Abg.Data.Tests.Paymongo;

public class PaymongoQrphClientTests
{
    static ClientRequest Request() => new()
    {
        ClientInformation = new ClientInformation
        {
            ClientBookingId = "070526-12345678",
            Email           = "client@example.com",
            FirstName       = "Ana",
            LastName        = "Cruz",
            ContactNumber   = "09171234567"
        },
        ClientServices =
        [
            new ClientService { ServiceUid = "NAS-100", ServiceName = "Nails", ServiceDetails = "Gel polish", ServiceCost = 500.50m },
            new ClientService { ServiceUid = "LAS-200", ServiceName = "Lashes", ServiceDetails = "Classic", ServiceCost = 249.50m }
        ]
    };

    static PaymongoQrphClient Client(MockHttpHandler handler)
    {
        var httpClient = new HttpClient(handler);

        PaymongoQrphClient.ConfigureHttpClient(httpClient, new PaymongoOptions
        {
            SecretKey = "sk_test_secret",
            BaseUrl   = "https://api.paymongo.test/v1/"
        });

        return new PaymongoQrphClient(httpClient);
    }

    static MockHttpHandler HandlerForSuccessfulCharge()
    {
        return new MockHttpHandler()
            .Enqueue(HttpStatusCode.OK, """
                {"data":{"id":"pi_123","attributes":{"amount":75000,"status":"awaiting_payment_method","client_key":"ck_abc"}}}
                """)
            .Enqueue(HttpStatusCode.OK, """
                {"data":{"id":"pm_456"}}
                """)
            .Enqueue(HttpStatusCode.OK, """
                {"data":{"id":"pi_123","attributes":{"amount":75000,"status":"awaiting_next_action",
                 "next_action":{"type":"render_qr_code","code":{"id":"qr_789","amount":75000,"image_url":"https://qr.example/img.png","label":"ABG"}}}}}
                """);
    }

    [Fact]
    public async Task CreateQrphCharge_posts_intent_method_and_attach_in_order()
    {
        var handler = HandlerForSuccessfulCharge();
        var client  = Client(handler);

        await client.CreateQrphChargeAsync(Request());

        Assert.Equal(3, handler.Requests.Count);
        Assert.EndsWith("payment_intents", handler.Requests[0].Url);
        Assert.EndsWith("payment_methods", handler.Requests[1].Url);
        Assert.EndsWith("payment_intents/pi_123/attach", handler.Requests[2].Url);
        Assert.All(handler.Requests, r => Assert.Equal(HttpMethod.Post, r.Method));
    }

    [Fact]
    public async Task CreateQrphCharge_sends_total_in_centavos_and_booking_metadata()
    {
        var handler = HandlerForSuccessfulCharge();
        var client  = Client(handler);

        await client.CreateQrphChargeAsync(Request());

        using var intentBody = JsonDocument.Parse(handler.Requests[0].Body);
        var attributes = intentBody.RootElement.GetProperty("data").GetProperty("attributes");

        Assert.Equal(75000, attributes.GetProperty("amount").GetInt32());
        Assert.Equal("PHP", attributes.GetProperty("currency").GetString());
        Assert.Equal("070526-12345678", attributes.GetProperty("metadata").GetProperty("client_booking_id").GetString());
    }

    [Fact]
    public async Task CreateQrphCharge_uses_three_minute_qr_expiry()
    {
        var handler = HandlerForSuccessfulCharge();
        var client  = Client(handler);

        await client.CreateQrphChargeAsync(Request());

        using var methodBody = JsonDocument.Parse(handler.Requests[1].Body);
        var attributes = methodBody.RootElement.GetProperty("data").GetProperty("attributes");

        Assert.Equal(180, attributes.GetProperty("expiry_seconds").GetInt32());
    }

    [Fact]
    public async Task CreateQrphCharge_maps_attach_response_to_result()
    {
        var client = Client(HandlerForSuccessfulCharge());

        var result = await client.CreateQrphChargeAsync(Request());

        Assert.Equal("pi_123", result.PaymentIntentId);
        Assert.Equal("awaiting_next_action", result.PaymentIntentStatus);
        Assert.Equal("pm_456", result.PaymentMethodId);
        Assert.Equal(75000, result.AmountCentavos);
        Assert.Equal(750m, result.AmountPhp);
        Assert.Equal("https://qr.example/img.png", result.QrImageUrl);
        Assert.Equal("qr_789", result.QrCodeId);
        Assert.Equal("render_qr_code", result.NextActionType);
    }

    [Fact]
    public async Task GetPaymentIntentStatus_returns_status_field()
    {
        var handler = new MockHttpHandler().Enqueue(HttpStatusCode.OK, """
            {"data":{"id":"pi_123","attributes":{"amount":75000,"status":"succeeded"}}}
            """);

        var status = await Client(handler).GetPaymentIntentStatusAsync("pi_123");

        Assert.Equal("succeeded", status);
        Assert.EndsWith("payment_intents/pi_123", handler.Requests[0].Url);
    }

    [Fact]
    public async Task GetPaymentIntentClientBookingId_reads_metadata()
    {
        var handler = new MockHttpHandler().Enqueue(HttpStatusCode.OK, """
            {"data":{"id":"pi_123","attributes":{"metadata":{"client_booking_id":"070526-12345678"}}}}
            """);

        var bookingId = await Client(handler).GetPaymentIntentClientBookingIdAsync("pi_123");

        Assert.Equal("070526-12345678", bookingId);
    }

    [Fact]
    public async Task Paymongo_error_response_surfaces_code_and_detail()
    {
        var handler = new MockHttpHandler().Enqueue(HttpStatusCode.BadRequest, """
            {"errors":[{"code":"parameter_invalid","detail":"amount is below minimum"}]}
            """);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => Client(handler).GetPaymentIntentStatusAsync("pi_123"));

        Assert.Contains("parameter_invalid", ex.Message);
        Assert.Contains("amount is below minimum", ex.Message);
    }
}
