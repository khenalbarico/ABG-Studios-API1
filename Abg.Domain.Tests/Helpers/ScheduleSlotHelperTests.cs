using Abg.Domain.Client;
using Abg.Domain.Helpers;
using Abg.Domain.Schedules;
using Abg.Domain.Service;
using static Abg.Domain.Constants;

namespace Abg.Domain.Tests.Helpers;

public class ScheduleSlotHelperTests
{
    static readonly DateTime Slot = new(2026, 7, 10, 10, 0, 0);

    static ServiceCollectionResp Services() => new()
    {
        Nails = [new NailsService { Uid = "NAS-100", Details = "Gel polish", Cost = 500m }]
    };

    static ScheduleCfg Cfg(int nailsCapacity) => new()
    {
        NailsAccommodationCapacities = new Dictionary<string, int> { ["10:00 AM"] = nailsCapacity }
    };

    static ClientRequest Request(string bookingId, DateTime bookingDate, ClientStatus status, params ClientService[] services) => new()
    {
        ClientInformation = new ClientInformation { ClientBookingId = bookingId, BookingDate = bookingDate },
        Status            = status,
        ClientServices    = [.. services]
    };

    static ClientService NailsBooking(string uid = "NAS-100") => new()
    {
        ServiceUid  = uid,
        ServiceName = "Nails",
        ServiceDate = Slot
    };

    [Fact]
    public void ServiceDateKey_round_trips()
    {
        var key = Slot.ToServiceDateKey();

        Assert.Equal("2026-07-10T10:00:00", key);
        Assert.True(key.TryParseServiceDateKey(out var parsed));
        Assert.Equal(Slot, parsed);
    }

    [Fact]
    public void ResolveSlotKey_returns_matching_hour_key_or_throws()
    {
        var capacities = new Dictionary<string, int> { ["10:00 AM"] = 2, ["11:00 AM"] = 2 };

        Assert.Equal("10:00 AM", Slot.ResolveSlotKey(capacities));
        Assert.Throws<InvalidOperationException>(() => Slot.AddHours(5).ResolveSlotKey(capacities));
    }

    [Fact]
    public void GetValidRequests_drops_own_booking_and_expires_stale_pending_holds()
    {
        var now = new DateTime(2026, 7, 10, 12, 0, 0);

        var mine        = Request("mine",    now.AddMinutes(-1),  ClientStatus.Pending);
        var freshHold   = Request("fresh",   now.AddMinutes(-2),  ClientStatus.Pending);
        var staleHold   = Request("stale",   now.AddMinutes(-10), ClientStatus.Pending);
        var paidBooking = Request("paid",    now.AddHours(-3),    ClientStatus.Paid);

        var valid = new List<ClientRequest> { mine, freshHold, staleHold, paidBooking }
            .GetValidRequests("mine", now, TimeSpan.FromMinutes(5), out var expiredIds);

        Assert.Equal(["fresh", "paid"], valid.Select(x => x.ClientInformation.ClientBookingId).Order());
        Assert.Equal(["stale"], expiredIds);
    }

    [Fact]
    public void ValidateSlots_passes_when_capacity_not_exceeded()
    {
        List<ClientService> incoming = [NailsBooking()];

        incoming.ValidateSlots([], Cfg(1), Services());
    }

    [Fact]
    public void ValidateSlots_throws_when_existing_holds_fill_the_slot()
    {
        List<ClientService> incoming = [NailsBooking()];

        var existing = Request("other", DateTime.Now, ClientStatus.Pending, NailsBooking());

        var ex = Assert.Throws<InvalidOperationException>(
            () => incoming.ValidateSlots([existing], Cfg(1), Services()));

        Assert.Contains("queue", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ValidateSlots_accepts_card_key_service_uids()
    {
        List<ClientService> incoming = [NailsBooking(uid: "Nails|NAS-100|Gel polish|500")];

        incoming.ValidateSlots([], Cfg(1), Services());
    }

    [Fact]
    public void ValidateSlots_throws_for_unknown_service()
    {
        List<ClientService> incoming = [NailsBooking(uid: "NAS-999")];

        var ex = Assert.Throws<InvalidOperationException>(
            () => incoming.ValidateSlots([], Cfg(1), Services()));

        Assert.Contains("not found", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ValidateSlots_reports_zero_capacity_dates_clearly()
    {
        var cfg = Cfg(5);
        cfg.CustomizedServiceAccomodationCapacity =
        [
            new ThisServiceDateAccomodationCapacity
            {
                Uid = "NAS-100",
                ThisServiceAccomodationCapacity = new Dictionary<string, int> { ["2026-07-10T10:00:00"] = 0 }
            }
        ];

        List<ClientService> incoming = [NailsBooking()];

        var ex = Assert.Throws<InvalidOperationException>(
            () => incoming.ValidateSlots([], cfg, Services()));

        Assert.Contains("capacity is set to 0", ex.Message);
    }
}
