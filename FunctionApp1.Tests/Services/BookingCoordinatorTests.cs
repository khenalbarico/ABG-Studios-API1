using Abg.Domain.Client;
using Abg.Domain.Schedules;
using Abg.Domain.Service;
using FunctionApp1.Auth;
using FunctionApp1.Services;
using FunctionApp1.Tests.TestSupport;
using static Abg.Domain.Constants;

namespace FunctionApp1.Tests.Services;

public class BookingCoordinatorTests
{
    static readonly DateTime Slot = DateTime.Today.AddDays(7).AddHours(10);

    static ClientPrincipal Principal() => new()
    {
        IdentityProvider = "google",
        UserId           = "abc123",
        UserDetails      = "ana@example.com",
        UserRoles        = ["authenticated"]
    };

    static (BookingCoordinator Coordinator, FakeHoldStore Holds, FakeUserStore Users) Build(int nailsCapacity = 2)
    {
        var services = new FakeServiceStore
        {
            Catalog = new ServiceCollectionResp
            {
                Nails = [new NailsService { Uid = "NAS-100", Cost = 500m, Details = "Gel polish" }]
            }
        };

        var config = new FakeScheduleConfigStore
        {
            Config = new ScheduleCfg
            {
                NailsAccommodationCapacities = new Dictionary<string, int> { ["10:00 AM"] = nailsCapacity }
            }
        };

        var holds = new FakeHoldStore();
        var users = new FakeUserStore();

        return (new BookingCoordinator(holds, services, config, users), holds, users);
    }

    static ClientRequest Request(decimal claimedCost = 1m) => new()
    {
        ClientInformation = new ClientInformation
        {
            Email         = "form-email@example.com",
            FirstName     = "Ana",
            LastName      = "Cruz",
            ContactNumber = "09171234567"
        },
        ClientServices =
        [
            new ClientService
            {
                ServiceUid  = "NAS-100",
                ServiceName = "Nails",
                ServiceCost = claimedCost,
                ServiceDate = Slot
            }
        ]
    };

    [Fact]
    public async Task Creates_hold_with_server_assigned_id_and_principal_attribution()
    {
        var (coordinator, holds, users) = Build();

        var bookingId = await coordinator.CreateAsync(Request(), Principal());

        Assert.Matches(@"^\d{6}-\d{8}$", bookingId);

        var hold = Assert.Single(holds.Holds).Value;

        Assert.Equal(bookingId, hold.ClientInformation.ClientBookingId);
        Assert.Equal("google:abc123", hold.UserId);
        Assert.Equal("ana@example.com", hold.ClientInformation.Email);
        Assert.Equal(ClientStatus.Pending, hold.Status);

        var user = Assert.Single(users.Upserted);
        Assert.Equal("google:abc123", user.UserId);
    }

    [Fact]
    public async Task Overwrites_client_submitted_cost_with_catalog_cost()
    {
        var (coordinator, holds, _) = Build();

        await coordinator.CreateAsync(Request(claimedCost: 1m), Principal());

        var service = Assert.Single(holds.Holds).Value.ClientServices.Single();

        Assert.Equal(500m, service.ServiceCost);
        Assert.Equal("Gel polish", service.ServiceDetails);
    }

    [Fact]
    public async Task Rejects_booking_when_slot_capacity_is_taken_by_valid_holds()
    {
        var (coordinator, holds, _) = Build(nailsCapacity: 1);

        holds.Holds["070526-99999999"] = new ClientRequest
        {
            ClientInformation = new ClientInformation
            {
                ClientBookingId = "070526-99999999",
                BookingDate     = DateTime.Now
            },
            Status         = ClientStatus.Pending,
            ClientServices =
            [
                new ClientService { ServiceUid = "NAS-100", ServiceName = "Nails", ServiceDate = Slot }
            ]
        };

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => coordinator.CreateAsync(Request(), Principal()));

        Assert.Single(holds.Holds);
    }

    [Fact]
    public async Task Deletes_expired_pending_holds_during_validation()
    {
        var (coordinator, holds, _) = Build(nailsCapacity: 1);

        holds.Holds["070526-88888888"] = new ClientRequest
        {
            ClientInformation = new ClientInformation
            {
                ClientBookingId = "070526-88888888",
                BookingDate     = DateTime.Now.AddMinutes(-30)
            },
            Status         = ClientStatus.Pending,
            ClientServices =
            [
                new ClientService { ServiceUid = "NAS-100", ServiceName = "Nails", ServiceDate = Slot }
            ]
        };

        var bookingId = await coordinator.CreateAsync(Request(), Principal());

        Assert.Contains("070526-88888888", holds.Deleted);
        Assert.True(holds.Holds.ContainsKey(bookingId));
        Assert.False(holds.Holds.ContainsKey("070526-88888888"));
    }

    [Fact]
    public async Task Rejects_empty_service_list()
    {
        var (coordinator, _, _) = Build();

        var request = Request();
        request.ClientServices.Clear();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => coordinator.CreateAsync(request, Principal()));
    }

    [Fact]
    public async Task Rejects_unknown_service_uid()
    {
        var (coordinator, _, _) = Build();

        var request = Request();
        request.ClientServices[0].ServiceUid = "NAS-999";

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => coordinator.CreateAsync(request, Principal()));
    }
}
