using Abg.Data.Firebase;
using Abg.Data.Paymongo;
using Abg.Data.Paymongo.Models;
using Abg.Data.Tables;
using Abg.Domain.Client;
using Abg.Domain.Schedules;
using Abg.Domain.Service;
using Abg.Domain.__Base__;
using static Abg.Domain.Constants;

namespace FunctionApp1.Tests.TestSupport;

public sealed class FakeHoldStore : IBookingHoldStore
{
    public Dictionary<string, ClientRequest> Holds { get; } = [];
    public List<string> Deleted { get; } = [];

    public Task<List<ClientRequest>> GetHoldsAsync(CancellationToken ct = default)
        => Task.FromResult(Holds.Values.ToList());

    public Task<ClientRequest?> GetHoldAsync(string bookingId, CancellationToken ct = default)
        => Task.FromResult(Holds.GetValueOrDefault(bookingId));

    public Task PutHoldAsync(ClientRequest request, CancellationToken ct = default)
    {
        Holds[request.ClientInformation.ClientBookingId] = request;
        return Task.CompletedTask;
    }

    public Task SetHoldStatusAsync(string bookingId, ClientStatus status, CancellationToken ct = default)
    {
        if (Holds.TryGetValue(bookingId, out var hold))
            hold.Status = status;

        return Task.CompletedTask;
    }

    public Task DeleteHoldAsync(string bookingId, CancellationToken ct = default)
    {
        Holds.Remove(bookingId);
        Deleted.Add(bookingId);
        return Task.CompletedTask;
    }
}

public sealed class FakeServiceStore : IServiceStore
{
    public ServiceCollectionResp Catalog { get; set; } = new();

    public Task<ServiceCollectionResp> GetAllAsync(CancellationToken ct = default)
        => Task.FromResult(Catalog);

    public Task UpsertAsync(string category, BaseSvcStructure service, CancellationToken ct = default)
        => Task.CompletedTask;

    public Task DeleteAsync(string category, string serviceUid, CancellationToken ct = default)
        => Task.CompletedTask;
}

public sealed class FakeScheduleConfigStore : IScheduleConfigStore
{
    public ScheduleCfg Config { get; set; } = new();

    public Task<ScheduleCfg> GetAsync(CancellationToken ct = default) => Task.FromResult(Config);

    public Task UpsertAsync(ScheduleCfg cfg, CancellationToken ct = default)
    {
        Config = cfg;
        return Task.CompletedTask;
    }
}

public sealed class FakeUserStore : IUserStore
{
    public List<AppUser> Upserted { get; } = [];

    public Task UpsertAsync(AppUser user, CancellationToken ct = default)
    {
        Upserted.Add(user);
        return Task.CompletedTask;
    }
}

public sealed class FakeBookingStore : IBookingStore
{
    public Dictionary<string, ClientRequest> Bookings { get; } = [];

    public Task<ClientRequest?> GetAsync(string bookingId, CancellationToken ct = default)
        => Task.FromResult(Bookings.GetValueOrDefault(bookingId));

    public Task UpsertAsync(ClientRequest request, CancellationToken ct = default)
    {
        Bookings[request.ClientInformation.ClientBookingId] = request;
        return Task.CompletedTask;
    }

    public Task UpdateStatusAsync(string bookingId, ClientStatus status, CancellationToken ct = default)
    {
        if (Bookings.TryGetValue(bookingId, out var booking))
            booking.Status = status;

        return Task.CompletedTask;
    }

    public Task UpdateServiceStatusAsync(string bookingId, string serviceUid, ClientServiceStatus status, CancellationToken ct = default)
        => Task.CompletedTask;
}

public sealed class FakeAppointmentStore : IAppointmentStore
{
    public List<ClientRequest> Added { get; } = [];

    public Task AddForBookingAsync(ClientRequest request, CancellationToken ct = default)
    {
        Added.Add(request);
        return Task.CompletedTask;
    }

    public Task<List<ApptSchedRec>> GetRangeAsync(DateTime fromDate, DateTime toDate, CancellationToken ct = default)
        => Task.FromResult(new List<ApptSchedRec>());
}

public sealed class FakePurchaseStore : IPurchaseStore
{
    public List<(string BookingId, string PaymentIntentId, string Status)> Records { get; } = [];

    public Task UpsertAsync(string bookingId, PaymongoQrphChargeResult charge, string status, CancellationToken ct = default)
    {
        Records.Add((bookingId, charge.PaymentIntentId, status));
        return Task.CompletedTask;
    }

    public Task UpdateStatusAsync(string bookingId, string paymentIntentId, string status, CancellationToken ct = default)
    {
        Records.Add((bookingId, paymentIntentId, status));
        return Task.CompletedTask;
    }
}

public sealed class FakePaymongoClient : IPaymongoClient
{
    public PaymongoQrphChargeResult ChargeResult { get; set; } = new()
    {
        PaymentIntentId     = "pi_123",
        PaymentIntentStatus = "awaiting_next_action",
        QrImageUrl          = "https://qr.example/img.png",
        AmountCentavos      = 75000,
        AmountPhp           = 750m
    };

    public string IntentStatus    { get; set; } = "succeeded";
    public string IntentBookingId { get; set; } = "";
    public List<ClientRequest> Charged { get; } = [];

    public Task<PaymongoQrphChargeResult> CreateQrphChargeAsync(ClientRequest req, CancellationToken ct = default)
    {
        Charged.Add(req);
        return Task.FromResult(ChargeResult);
    }

    public Task<string> GetPaymentIntentStatusAsync(string paymentIntentId, CancellationToken ct = default)
        => Task.FromResult(IntentStatus);

    public Task<string> GetPaymentIntentClientBookingIdAsync(string paymentIntentId, CancellationToken ct = default)
        => Task.FromResult(IntentBookingId);
}
