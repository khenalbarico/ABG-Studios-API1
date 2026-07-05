using Abg.Data.Firebase;
using Abg.Data.Paymongo;
using Abg.Data.Tables;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using static Abg.Domain.Constants;

namespace FunctionApp1.Services;

public sealed record WebhookOutcome(string Result, bool Accepted = true);

public sealed class PaymongoWebhookProcessor(
    IBookingHoldStore _holds,
    IBookingStore     _bookings,
    IAppointmentStore _appointments,
    IPurchaseStore    _purchases,
    IPaymongoClient   _paymongo,
    PaymongoOptions   _options,
    ILogger<PaymongoWebhookProcessor> _logger)
{
    public async Task<WebhookOutcome> ProcessAsync(string body, string? signatureHeader, CancellationToken ct = default)
    {
        if (!PaymongoWebhookVerifier.IsValid(signatureHeader, body, _options.WebhookSecretKey))
        {
            _logger.LogWarning("PayMongo webhook rejected: invalid or missing signature.");
            return new WebhookOutcome("invalid-signature", Accepted: false);
        }

        if (string.IsNullOrWhiteSpace(body))
            return new WebhookOutcome("empty-body");

        string eventType, paymentIntentId, bookingId;

        try
        {
            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;

            if (!TryGetProperty(root, "data", out var eventData) ||
                !TryGetProperty(eventData, "attributes", out var eventAttributes))
            {
                return new WebhookOutcome("missing-event-data");
            }

            eventType = GetString(eventAttributes, "type");

            if (!TryGetProperty(eventAttributes, "data", out var resourceData) ||
                !TryGetProperty(resourceData, "attributes", out var resourceAttributes))
            {
                return new WebhookOutcome("missing-resource-data");
            }

            paymentIntentId = GetString(resourceAttributes, "payment_intent_id");
            bookingId       = GetBookingId(resourceAttributes);
        }
        catch (JsonException)
        {
            return new WebhookOutcome("invalid-json");
        }

        if (string.IsNullOrWhiteSpace(bookingId) && !string.IsNullOrWhiteSpace(paymentIntentId))
            bookingId = await _paymongo.GetPaymentIntentClientBookingIdAsync(paymentIntentId, ct);

        if (string.IsNullOrWhiteSpace(bookingId))
        {
            _logger.LogWarning(
                "PayMongo webhook booking ID was not resolved. EventType: {EventType}, PaymentIntentId: {PaymentIntentId}",
                eventType,
                paymentIntentId);

            return new WebhookOutcome("booking-id-not-resolved");
        }

        return eventType switch
        {
            "payment.paid"   => await HandlePaymentPaidAsync(bookingId, paymentIntentId, ct),
            "payment.failed" => HandlePaymentFailed(bookingId, paymentIntentId),
            "qrph.expired"   => await HandleQrExpiredAsync(bookingId, ct),
            _                => new WebhookOutcome("unhandled-event")
        };
    }

    private async Task<WebhookOutcome> HandlePaymentPaidAsync(string bookingId, string paymentIntentId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(paymentIntentId))
            return new WebhookOutcome("payment-intent-id-not-resolved");

        var existing = await _bookings.GetAsync(bookingId, ct);

        if (existing is { Status: ClientStatus.Paid })
            return new WebhookOutcome("already-paid");

        var hold = await _holds.GetHoldAsync(bookingId, ct);

        if (hold is null)
        {
            _logger.LogWarning(
                "PayMongo payment.paid received but no booking hold exists. BookingId: {BookingId}, PaymentIntentId: {PaymentIntentId}",
                bookingId,
                paymentIntentId);

            return new WebhookOutcome("booking-not-found");
        }

        // Never trust the webhook alone: re-check the intent status with Paymongo.
        var status = await _paymongo.GetPaymentIntentStatusAsync(paymentIntentId, ct);

        if (status != "succeeded")
        {
            _logger.LogWarning(
                "PayMongo payment.paid received but intent status is {Status}. BookingId: {BookingId}",
                status,
                bookingId);

            return new WebhookOutcome($"payment-intent-status-{status}");
        }

        hold.Status = ClientStatus.Paid;

        await _bookings.UpsertAsync(hold, ct);
        await _appointments.AddForBookingAsync(hold, ct);
        await _purchases.UpdateStatusAsync(bookingId, paymentIntentId, "succeeded", ct);
        await _holds.DeleteHoldAsync(bookingId, ct);

        return new WebhookOutcome("payment-paid");
    }

    private WebhookOutcome HandlePaymentFailed(string bookingId, string paymentIntentId)
    {
        _logger.LogWarning(
            "PayMongo payment.failed received. BookingId: {BookingId}, PaymentIntentId: {PaymentIntentId}",
            bookingId,
            paymentIntentId);

        return new WebhookOutcome("payment-failed");
    }

    private async Task<WebhookOutcome> HandleQrExpiredAsync(string bookingId, CancellationToken ct)
    {
        var hold = await _holds.GetHoldAsync(bookingId, ct);

        if (hold is { Status: ClientStatus.Pending })
            await _holds.DeleteHoldAsync(bookingId, ct);

        return new WebhookOutcome("qrph-expired");
    }

    private static string GetBookingId(JsonElement resourceAttributes)
    {
        if (!TryGetProperty(resourceAttributes, "metadata", out var metadata))
            return "";

        return GetString(metadata, "client_booking_id");
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
        if (!TryGetProperty(element, propertyName, out var property))
            return "";

        if (property.ValueKind == JsonValueKind.Null || property.ValueKind == JsonValueKind.Undefined)
            return "";

        return property.ValueKind == JsonValueKind.String
            ? property.GetString() ?? ""
            : property.ToString();
    }
}
