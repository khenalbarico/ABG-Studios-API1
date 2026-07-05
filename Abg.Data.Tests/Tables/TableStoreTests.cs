using Abg.Data.Paymongo.Models;
using Abg.Data.Tables;
using Abg.Data.Tests.TestSupport;
using Abg.Domain.__Base__;
using Abg.Domain.Client;
using Abg.Domain.Schedules;
using Azure.Data.Tables;
using static Abg.Domain.Constants;

namespace Abg.Data.Tests.Tables;

public class TableStoreTests(AzuriteFixture _azurite) : IClassFixture<AzuriteFixture>
{
    [SkippableFact]
    public async Task ServiceStore_round_trips_services_per_category()
    {
        var store = new ServiceTableStore(_azurite.CreateClient(), AzuriteFixture.UniqueTableName());

        await store.UpsertAsync("Nails", new BaseSvcStructure
        {
            Uid           = "NAS-100",
            Cost          = 500.50m,
            Details       = "Gel polish",
            ScheduleSlots = new BaseSchedSlot { DaySlots = ["Monday"], TimeSlots = ["10:00 AM - 11:00 AM"], IsAvailable = true }
        });

        await store.UpsertAsync("Lash", new BaseSvcStructure { Uid = "LAS-200", Cost = 700m, Details = "Volume set" });

        var all = await store.GetAllAsync();

        var nails = Assert.Single(all.Nails);
        Assert.Equal("NAS-100", nails.Uid);
        Assert.Equal(500.50m, nails.Cost);
        Assert.Equal("Gel polish", nails.Details);
        Assert.Equal(["Monday"], nails.ScheduleSlots.DaySlots);
        Assert.True(nails.ScheduleSlots.IsAvailable);

        Assert.Single(all.Lashes);
        Assert.Empty(all.Eyebrows);
        Assert.Empty(all.Footspa);
        Assert.Empty(all.Pedicure);
    }

    [SkippableFact]
    public async Task ServiceStore_upsert_is_idempotent_and_delete_removes_row()
    {
        var store = new ServiceTableStore(_azurite.CreateClient(), AzuriteFixture.UniqueTableName());
        var service = new BaseSvcStructure { Uid = "NAS-100", Cost = 500m, Details = "Gel polish" };

        await store.UpsertAsync("Nails", service);
        await store.UpsertAsync("Nails", service);

        Assert.Single((await store.GetAllAsync()).Nails);

        await store.DeleteAsync("Nails", "NAS-100");

        Assert.Empty((await store.GetAllAsync()).Nails);
    }

    [SkippableFact]
    public async Task ServiceStore_rejects_unknown_category()
    {
        var store = new ServiceTableStore(_azurite.CreateClient(), AzuriteFixture.UniqueTableName());

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => store.UpsertAsync("Massage", new BaseSvcStructure { Uid = "X" }));
    }

    [SkippableFact]
    public async Task ScheduleConfigStore_returns_default_when_missing_and_round_trips()
    {
        var store = new ScheduleConfigTableStore(_azurite.CreateClient(), AzuriteFixture.UniqueTableName());

        var missing = await store.GetAsync();
        Assert.Empty(missing.NailsAccommodationCapacities);

        await store.UpsertAsync(new ScheduleCfg
        {
            StoreHours                   = ["10:00 AM - 7:00 PM"],
            NailsAccommodationCapacities = new Dictionary<string, int> { ["10:00 AM"] = 2 }
        });

        var loaded = await store.GetAsync();

        Assert.Equal(["10:00 AM - 7:00 PM"], loaded.StoreHours);
        Assert.Equal(2, loaded.NailsAccommodationCapacities["10:00 AM"]);
    }

    static ClientRequest Booking(string bookingId) => new()
    {
        ClientInformation = new ClientInformation
        {
            ClientBookingId = bookingId,
            Email           = "client@example.com",
            FirstName       = "Ana",
            LastName        = "Cruz",
            BookingDate     = new DateTime(2026, 7, 5, 12, 0, 0)
        },
        ClientServices =
        [
            new ClientService
            {
                ServiceUid  = "NAS-100",
                ServiceName = "Nails",
                ServiceCost = 500m,
                ServiceDate = new DateTime(2026, 7, 10, 10, 0, 0)
            },
            new ClientService
            {
                ServiceUid  = "LAS-200",
                ServiceName = "Lashes",
                ServiceCost = 700m,
                ServiceDate = new DateTime(2026, 7, 11, 14, 0, 0)
            }
        ]
    };

    [SkippableFact]
    public async Task BookingStore_round_trips_and_updates_statuses()
    {
        var store = new BookingTableStore(_azurite.CreateClient(), AzuriteFixture.UniqueTableName());

        var booking = Booking("070526-11111111");
        booking.UserId = "google:abc";

        await store.UpsertAsync(booking);

        var loaded = await store.GetAsync("070526-11111111");

        Assert.NotNull(loaded);
        Assert.Equal("client@example.com", loaded.ClientInformation.Email);
        Assert.Equal(2, loaded.ClientServices.Count);
        Assert.Equal(ClientStatus.Pending, loaded.Status);

        await store.UpdateStatusAsync("070526-11111111", ClientStatus.Paid);
        Assert.Equal(ClientStatus.Paid, (await store.GetAsync("070526-11111111"))!.Status);

        await store.UpdateServiceStatusAsync("070526-11111111", "NAS-100", ClientServiceStatus.Serving);

        var afterServing = await store.GetAsync("070526-11111111");
        Assert.Equal(ClientServiceStatus.Serving, afterServing!.ClientServices.Single(x => x.ServiceUid == "NAS-100").Status);
        Assert.Equal(ClientServiceStatus.Pending, afterServing.ClientServices.Single(x => x.ServiceUid == "LAS-200").Status);
    }

    [SkippableFact]
    public async Task BookingStore_missing_booking_returns_null_and_updates_are_noops()
    {
        var store = new BookingTableStore(_azurite.CreateClient(), AzuriteFixture.UniqueTableName());

        Assert.Null(await store.GetAsync("070526-00000000"));

        await store.UpdateStatusAsync("070526-00000000", ClientStatus.Paid);
        await store.UpdateServiceStatusAsync("070526-00000000", "NAS-100", ClientServiceStatus.Serving);
    }

    [SkippableFact]
    public async Task AppointmentStore_groups_range_results_per_booking_and_slot()
    {
        var store = new AppointmentTableStore(_azurite.CreateClient(), AzuriteFixture.UniqueTableName());

        await store.AddForBookingAsync(Booking("070526-11111111"));

        var inRange = await store.GetRangeAsync(new DateTime(2026, 7, 10), new DateTime(2026, 7, 31));

        Assert.Equal(2, inRange.Count);

        var nailsRec = Assert.Single(inRange, x => x.Services.Any(s => s.ServiceName == "Nails"));
        Assert.Equal(new DateTime(2026, 7, 10, 10, 0, 0), nailsRec.ServiceDate);
        Assert.Equal("070526-11111111", nailsRec.ClientBookingId);

        var narrow = await store.GetRangeAsync(new DateTime(2026, 7, 11), new DateTime(2026, 7, 11));

        Assert.Single(narrow);
        Assert.Equal("Lashes", narrow[0].Services.Single().ServiceName);
    }

    [SkippableFact]
    public async Task PurchaseStore_records_charge_snapshot()
    {
        var tableName = AzuriteFixture.UniqueTableName();
        var client    = _azurite.CreateClient();
        var store     = new PurchaseTableStore(client, tableName);

        await store.UpsertAsync("070526-11111111", new PaymongoQrphChargeResult
        {
            PaymentIntentId = "pi_123",
            AmountCentavos  = 75000,
            AmountPhp       = 750m,
            PaymentMethodId = "pm_456",
            QrCodeId        = "qr_789"
        }, status: "awaiting_next_action");

        var entity = await client.GetTableClient(tableName)
            .GetEntityAsync<TableEntity>("070526-11111111", "pi_123");

        Assert.Equal("awaiting_next_action", entity.Value.GetString("Status"));
        Assert.Equal(75000L, entity.Value.GetInt64("AmountCentavos"));
    }

    [SkippableFact]
    public async Task UserStore_preserves_first_seen_across_upserts()
    {
        var tableName = AzuriteFixture.UniqueTableName();
        var client    = _azurite.CreateClient();
        var store     = new UserTableStore(client, tableName);

        var user = new AppUser { UserId = "google:abc", Provider = "google", Email = "a@b.c", Name = "Ana" };

        await store.UpsertAsync(user);

        var table = client.GetTableClient(tableName);
        var first = await table.GetEntityAsync<TableEntity>("user", "google:abc");
        var firstSeen = first.Value.GetDateTime("FirstSeenUtc");

        user.Name = "Ana Cruz";
        await store.UpsertAsync(user);

        var second = await table.GetEntityAsync<TableEntity>("user", "google:abc");

        Assert.Equal(firstSeen, second.Value.GetDateTime("FirstSeenUtc"));
        Assert.Equal("Ana Cruz", second.Value.GetString("Name"));
        Assert.True(second.Value.GetDateTime("LastSeenUtc") >= firstSeen);
    }
}
