using Abg.Data.Tables;
using Abg.Data.Tests.TestSupport;
using Abg.Domain.Client;
using static Abg.Domain.Constants;

namespace Abg.Data.Tests.Tables;

public class BookingHoldTableStoreTests(AzuriteFixture _azurite) : IClassFixture<AzuriteFixture>
{
    static ClientRequest Hold(string bookingId, string email = "client@example.com") => new()
    {
        ClientInformation = new ClientInformation
        {
            ClientBookingId = bookingId,
            Email           = email,
            FirstName       = "Ana",
            LastName        = "Cruz",
            BookingDate     = new DateTime(2026, 7, 6, 12, 0, 0)
        },
        ClientServices =
        [
            new ClientService
            {
                ServiceUid  = "NAS-100",
                ServiceName = "Nails",
                ServiceCost = 500m,
                ServiceDate = new DateTime(2026, 7, 10, 10, 0, 0)
            }
        ]
    };

    [SkippableFact]
    public async Task GetHolds_returns_empty_list_when_table_is_empty()
    {
        var store = new BookingHoldTableStore(_azurite.CreateClient(), AzuriteFixture.UniqueTableName());

        Assert.Empty(await store.GetHoldsAsync());
    }

    [SkippableFact]
    public async Task PutHold_round_trips_the_full_request()
    {
        var store = new BookingHoldTableStore(_azurite.CreateClient(), AzuriteFixture.UniqueTableName());

        await store.PutHoldAsync(Hold("070626-11111111"));

        var loaded = await store.GetHoldAsync("070626-11111111");

        Assert.NotNull(loaded);
        Assert.Equal("client@example.com", loaded.ClientInformation.Email);
        Assert.Equal(new DateTime(2026, 7, 6, 12, 0, 0), loaded.ClientInformation.BookingDate);
        Assert.Equal(ClientStatus.Pending, loaded.Status);

        var service = Assert.Single(loaded.ClientServices);
        Assert.Equal("NAS-100", service.ServiceUid);
        Assert.Equal(500m, service.ServiceCost);
    }

    [SkippableFact]
    public async Task PutHold_is_an_upsert_for_the_same_booking()
    {
        var store = new BookingHoldTableStore(_azurite.CreateClient(), AzuriteFixture.UniqueTableName());

        await store.PutHoldAsync(Hold("070626-11111111"));
        await store.PutHoldAsync(Hold("070626-11111111", email: "updated@example.com"));

        var holds = await store.GetHoldsAsync();

        var hold = Assert.Single(holds);
        Assert.Equal("updated@example.com", hold.ClientInformation.Email);
    }

    [SkippableFact]
    public async Task GetHolds_returns_every_active_hold()
    {
        var store = new BookingHoldTableStore(_azurite.CreateClient(), AzuriteFixture.UniqueTableName());

        await store.PutHoldAsync(Hold("070626-11111111"));
        await store.PutHoldAsync(Hold("070626-22222222"));

        var holds = await store.GetHoldsAsync();

        Assert.Equal(2, holds.Count);
        Assert.Contains(holds, x => x.ClientInformation.ClientBookingId == "070626-11111111");
        Assert.Contains(holds, x => x.ClientInformation.ClientBookingId == "070626-22222222");
    }

    [SkippableFact]
    public async Task GetHold_returns_null_for_missing_booking()
    {
        var store = new BookingHoldTableStore(_azurite.CreateClient(), AzuriteFixture.UniqueTableName());

        Assert.Null(await store.GetHoldAsync("070626-99999999"));
    }

    [SkippableFact]
    public async Task DeleteHold_removes_the_hold()
    {
        var store = new BookingHoldTableStore(_azurite.CreateClient(), AzuriteFixture.UniqueTableName());

        await store.PutHoldAsync(Hold("070626-11111111"));
        await store.DeleteHoldAsync("070626-11111111");

        Assert.Null(await store.GetHoldAsync("070626-11111111"));
        Assert.Empty(await store.GetHoldsAsync());
    }

    [SkippableFact]
    public async Task DeleteHold_on_missing_booking_is_a_noop()
    {
        var store = new BookingHoldTableStore(_azurite.CreateClient(), AzuriteFixture.UniqueTableName());

        await store.DeleteHoldAsync("070626-99999999");
    }
}
