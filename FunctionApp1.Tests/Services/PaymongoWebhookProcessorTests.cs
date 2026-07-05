using Abg.Data.Paymongo;
using Abg.Domain.Client;
using FunctionApp1.Services;
using FunctionApp1.Tests.TestSupport;
using Microsoft.Extensions.Logging.Abstractions;
using static Abg.Domain.Constants;

namespace FunctionApp1.Tests.Services;

public class PaymongoWebhookProcessorTests
{
    const string Secret = "whsk_test_secret";

    static string PaidBody(string bookingId = "070526-11111111", string intentId = "pi_123") => """
        {"data":{"id":"evt_1","attributes":{"type":"payment.paid",
         "data":{"id":"pay_1","attributes":{"payment_intent_id":"@intent",
         "metadata":{"client_booking_id":"@booking"}}}}}}
        """
        .Replace("@intent", intentId)
        .Replace("@booking", bookingId);

    static string ExpiredBody(string bookingId = "070526-11111111") => """
        {"data":{"id":"evt_2","attributes":{"type":"qrph.expired",
         "data":{"id":"qr_1","attributes":{"payment_intent_id":"pi_123",
         "metadata":{"client_booking_id":"@booking"}}}}}}
        """
        .Replace("@booking", bookingId);

    static string SignatureFor(string body)
        => $"t=1720000000,te={PaymongoWebhookVerifier.ComputeSignature("1720000000", body, Secret)}";

    static (PaymongoWebhookProcessor Processor, FakeHoldStore Holds, FakeBookingStore Bookings, FakeAppointmentStore Appointments, FakePurchaseStore Purchases, FakePaymongoClient Paymongo) Build()
    {
        var holds        = new FakeHoldStore();
        var bookings     = new FakeBookingStore();
        var appointments = new FakeAppointmentStore();
        var purchases    = new FakePurchaseStore();
        var paymongo     = new FakePaymongoClient();

        var processor = new PaymongoWebhookProcessor(
            holds,
            bookings,
            appointments,
            purchases,
            paymongo,
            new PaymongoOptions { WebhookSecretKey = Secret },
            NullLogger<PaymongoWebhookProcessor>.Instance);

        return (processor, holds, bookings, appointments, purchases, paymongo);
    }

    static ClientRequest Hold(string bookingId = "070526-11111111") => new()
    {
        ClientInformation = new ClientInformation { ClientBookingId = bookingId },
        Status            = ClientStatus.Pending,
        ClientServices    =
        [
            new ClientService { ServiceUid = "NAS-100", ServiceName = "Nails", ServiceDate = DateTime.Today.AddHours(10) }
        ]
    };

    [Fact]
    public async Task Rejects_invalid_signature()
    {
        var (processor, _, _, _, _, _) = Build();
        var body = PaidBody();

        var outcome = await processor.ProcessAsync(body, "t=1,te=bad", CancellationToken.None);

        Assert.False(outcome.Accepted);
        Assert.Equal("invalid-signature", outcome.Result);
    }

    [Fact]
    public async Task Payment_paid_promotes_hold_to_booking_record_and_ledger()
    {
        var (processor, holds, bookings, appointments, purchases, _) = Build();
        holds.Holds["070526-11111111"] = Hold();

        var body    = PaidBody();
        var outcome = await processor.ProcessAsync(body, SignatureFor(body), CancellationToken.None);

        Assert.Equal("payment-paid", outcome.Result);

        var booking = bookings.Bookings["070526-11111111"];
        Assert.Equal(ClientStatus.Paid, booking.Status);

        Assert.Single(appointments.Added);
        Assert.Equal(("070526-11111111", "pi_123", "succeeded"), purchases.Records.Single());
        Assert.Empty(holds.Holds);
    }

    [Fact]
    public async Task Payment_paid_is_idempotent_for_already_paid_bookings()
    {
        var (processor, _, bookings, appointments, _, _) = Build();

        var paid = Hold();
        paid.Status = ClientStatus.Paid;
        bookings.Bookings["070526-11111111"] = paid;

        var body    = PaidBody();
        var outcome = await processor.ProcessAsync(body, SignatureFor(body), CancellationToken.None);

        Assert.Equal("already-paid", outcome.Result);
        Assert.Empty(appointments.Added);
    }

    [Fact]
    public async Task Payment_paid_without_hold_reports_booking_not_found()
    {
        var (processor, _, _, _, _, _) = Build();

        var body    = PaidBody();
        var outcome = await processor.ProcessAsync(body, SignatureFor(body), CancellationToken.None);

        Assert.Equal("booking-not-found", outcome.Result);
    }

    [Fact]
    public async Task Payment_paid_rechecks_intent_status_before_promoting()
    {
        var (processor, holds, bookings, _, _, paymongo) = Build();
        holds.Holds["070526-11111111"] = Hold();
        paymongo.IntentStatus = "awaiting_payment_method";

        var body    = PaidBody();
        var outcome = await processor.ProcessAsync(body, SignatureFor(body), CancellationToken.None);

        Assert.Equal("payment-intent-status-awaiting_payment_method", outcome.Result);
        Assert.Empty(bookings.Bookings);
        Assert.Single(holds.Holds);
    }

    [Fact]
    public async Task Booking_id_falls_back_to_paymongo_metadata_lookup()
    {
        var (processor, holds, bookings, _, _, paymongo) = Build();
        holds.Holds["070526-11111111"] = Hold();
        paymongo.IntentBookingId = "070526-11111111";

        var body    = PaidBody(bookingId: "");
        var outcome = await processor.ProcessAsync(body, SignatureFor(body), CancellationToken.None);

        Assert.Equal("payment-paid", outcome.Result);
        Assert.True(bookings.Bookings.ContainsKey("070526-11111111"));
    }

    [Fact]
    public async Task Qr_expired_deletes_pending_hold()
    {
        var (processor, holds, _, _, _, _) = Build();
        holds.Holds["070526-11111111"] = Hold();

        var body    = ExpiredBody();
        var outcome = await processor.ProcessAsync(body, SignatureFor(body), CancellationToken.None);

        Assert.Equal("qrph-expired", outcome.Result);
        Assert.Empty(holds.Holds);
    }

    [Fact]
    public async Task Unhandled_events_are_acknowledged()
    {
        var (processor, _, _, _, _, _) = Build();

        var body = """
            {"data":{"id":"evt_3","attributes":{"type":"source.chargeable",
             "data":{"id":"src_1","attributes":{"metadata":{"client_booking_id":"070526-11111111"}}}}}}
            """;

        var outcome = await processor.ProcessAsync(body, SignatureFor(body), CancellationToken.None);

        Assert.True(outcome.Accepted);
        Assert.Equal("unhandled-event", outcome.Result);
    }
}
