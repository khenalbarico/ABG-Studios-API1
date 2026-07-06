using Abg.Data.Paymongo;
using Abg.Data.Tables;
using Abg.Domain.Contracts;
using FunctionApp1.Auth;
using static Abg.Domain.Constants;

namespace FunctionApp1.Services;

public sealed class PaymentCoordinator(
    IBookingHoldStore _holds,
    IBookingStore     _bookings,
    IPaymongoClient   _paymongo,
    IPurchaseStore    _purchases)
{
    public async Task<CreateQrphPaymentResponse> CreateQrphAsync(
        string bookingId,
        ClientPrincipal principal,
        CancellationToken ct = default)
    {
        var hold = await _holds.GetHoldAsync(bookingId, ct)
            ?? throw new InvalidOperationException("Booking was not found or has expired. Please book again.");

        if (!string.IsNullOrWhiteSpace(hold.UserId) && hold.UserId != principal.UserKey)
            throw new UnauthorizedAccessException("This booking belongs to another account.");

        if (hold.Status == ClientStatus.Paid)
            throw new InvalidOperationException("This booking is already paid.");

        var charge = await _paymongo.CreateQrphChargeAsync(hold, ct);

        await _purchases.UpsertAsync(bookingId, charge, charge.PaymentIntentStatus, ct);

        return new CreateQrphPaymentResponse
        {
            BookingId        = bookingId,
            PaymentIntentId  = charge.PaymentIntentId,
            QrImageUrl       = charge.QrImageUrl,
            AmountPhp        = charge.AmountPhp,
            ExpiresInSeconds = PaymongoQrphClient.QrExpirySeconds
        };
    }

    public async Task<PaymentStatusResponse> GetStatusAsync(string bookingId, CancellationToken ct = default)
    {
        var booking = await _bookings.GetAsync(bookingId, ct);

        if (booking is { Status: ClientStatus.Paid })
            return new PaymentStatusResponse { Status = PaymentStatusResponse.Paid };

        var hold = await _holds.GetHoldAsync(bookingId, ct);

        return new PaymentStatusResponse
        {
            Status = hold is null ? PaymentStatusResponse.Expired : PaymentStatusResponse.Pending
        };
    }
}
