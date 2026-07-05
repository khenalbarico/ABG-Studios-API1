using Abg.Domain.Client;
using Abg.Domain.Contracts;
using FunctionApp1.Auth;
using FunctionApp1.Services;
using FunctionApp1.Tests.TestSupport;
using static Abg.Domain.Constants;

namespace FunctionApp1.Tests.Services;

public class PaymentCoordinatorTests
{
    static ClientPrincipal Principal(string userId = "abc123") => new()
    {
        IdentityProvider = "google",
        UserId           = userId,
        UserDetails      = "ana@example.com",
        UserRoles        = ["authenticated"]
    };

    static ClientRequest Hold(string bookingId, string userId = "google:abc123") => new()
    {
        ClientInformation = new ClientInformation { ClientBookingId = bookingId },
        UserId            = userId,
        Status            = ClientStatus.Pending
    };

    static (PaymentCoordinator Coordinator, FakeHoldStore Holds, FakeBookingStore Bookings, FakePurchaseStore Purchases, FakePaymongoClient Paymongo) Build()
    {
        var holds     = new FakeHoldStore();
        var bookings  = new FakeBookingStore();
        var purchases = new FakePurchaseStore();
        var paymongo  = new FakePaymongoClient();

        return (new PaymentCoordinator(holds, bookings, paymongo, purchases), holds, bookings, purchases, paymongo);
    }

    [Fact]
    public async Task Creates_charge_and_records_purchase()
    {
        var (coordinator, holds, _, purchases, paymongo) = Build();
        holds.Holds["070526-11111111"] = Hold("070526-11111111");

        var response = await coordinator.CreateQrphAsync("070526-11111111", Principal());

        Assert.Equal("pi_123", response.PaymentIntentId);
        Assert.Equal("https://qr.example/img.png", response.QrImageUrl);
        Assert.Equal(750m, response.AmountPhp);
        Assert.Equal(180, response.ExpiresInSeconds);

        Assert.Single(paymongo.Charged);
        Assert.Equal(("070526-11111111", "pi_123", "awaiting_next_action"), purchases.Records.Single());
    }

    [Fact]
    public async Task Missing_hold_is_rejected()
    {
        var (coordinator, _, _, _, _) = Build();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => coordinator.CreateQrphAsync("070526-00000000", Principal()));
    }

    [Fact]
    public async Task Another_users_booking_is_forbidden()
    {
        var (coordinator, holds, _, _, _) = Build();
        holds.Holds["070526-11111111"] = Hold("070526-11111111", userId: "google:someone-else");

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => coordinator.CreateQrphAsync("070526-11111111", Principal()));
    }

    [Fact]
    public async Task Already_paid_hold_is_rejected()
    {
        var (coordinator, holds, _, _, _) = Build();

        var hold = Hold("070526-11111111");
        hold.Status = ClientStatus.Paid;
        holds.Holds["070526-11111111"] = hold;

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => coordinator.CreateQrphAsync("070526-11111111", Principal()));
    }

    [Fact]
    public async Task Status_is_paid_when_booking_record_is_paid()
    {
        var (coordinator, _, bookings, _, _) = Build();

        var booking = Hold("070526-11111111");
        booking.Status = ClientStatus.Paid;
        bookings.Bookings["070526-11111111"] = booking;

        var status = await coordinator.GetStatusAsync("070526-11111111");

        Assert.Equal(PaymentStatusResponse.Paid, status.Status);
    }

    [Fact]
    public async Task Status_is_pending_while_hold_exists()
    {
        var (coordinator, holds, _, _, _) = Build();
        holds.Holds["070526-11111111"] = Hold("070526-11111111");

        var status = await coordinator.GetStatusAsync("070526-11111111");

        Assert.Equal(PaymentStatusResponse.Pending, status.Status);
    }

    [Fact]
    public async Task Status_is_expired_when_neither_record_nor_hold_exists()
    {
        var (coordinator, _, _, _, _) = Build();

        var status = await coordinator.GetStatusAsync("070526-11111111");

        Assert.Equal(PaymentStatusResponse.Expired, status.Status);
    }
}
