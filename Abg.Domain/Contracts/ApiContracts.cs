using Abg.Domain.Schedules;
using Abg.Domain.Service;

namespace Abg.Domain.Contracts;

/// <summary>One-call payload for the storefront: catalog + schedule config + occupancy for a date range.</summary>
public sealed class CatalogResponse
{
    public ServiceCollectionResp Services     { get; set; } = new();
    public ScheduleCfg           ScheduleCfg  { get; set; } = new();
    public List<ApptSchedRec>    Appointments { get; set; } = [];
}

public sealed class CreateBookingResponse
{
    public string BookingId { get; set; } = "";
}

public sealed class CreateQrphPaymentRequest
{
    public string BookingId { get; set; } = "";
}

public sealed class CreateQrphPaymentResponse
{
    public string  BookingId        { get; set; } = "";
    public string  PaymentIntentId  { get; set; } = "";
    public string  QrImageUrl       { get; set; } = "";
    public decimal AmountPhp        { get; set; }
    public int     ExpiresInSeconds { get; set; }
}

public sealed class PaymentStatusResponse
{
    public const string Paid    = "paid";
    public const string Pending = "pending";
    public const string Expired = "expired";

    public string Status { get; set; } = "";
}

public sealed class ApiError
{
    public string Message { get; set; } = "";
}
