using Abg.Data.Tables;
using Abg.Domain.Algorithms;
using Abg.Domain.Client;
using Abg.Domain.Helpers;
using Abg.Domain.Service;
using Abg.Domain.__Base__;
using FunctionApp1.Auth;
using static Abg.Domain.Constants;

namespace FunctionApp1.Services;

public sealed class BookingCoordinator(
    IBookingHoldStore    _holds,
    IServiceStore        _services,
    IScheduleConfigStore _scheduleConfig,
    IUserStore           _users)
{
    public static readonly TimeSpan HoldDuration = TimeSpan.FromMinutes(5);

    // Mirrors the legacy AppDbOperator serialization of booking validation.
    // Per-instance only, but booking traffic is intermittent by design.
    static readonly SemaphoreSlim _bookingLock = new(1, 1);

    public async Task<string> CreateAsync(ClientRequest request, ClientPrincipal principal, CancellationToken ct = default)
    {
        if (request.ClientServices is not { Count: > 0 })
            throw new InvalidOperationException("Select at least one service before booking.");

        CheckoutRequestAlgorithms.PrepareClientInformation(request);

        request.Status = ClientStatus.Pending;
        request.UserId = principal.UserKey;

        if (!string.IsNullOrWhiteSpace(principal.Email))
            request.ClientInformation.Email = principal.Email;

        foreach (var service in request.ClientServices)
            service.Status = ClientServiceStatus.Pending;

        var catalog = await _services.GetAllAsync(ct);

        ApplyAuthoritativeCosts(request, catalog);

        await _bookingLock.WaitAsync(ct);

        try
        {
            var cfg   = await _scheduleConfig.GetAsync(ct);
            var holds = await _holds.GetHoldsAsync(ct);

            var validHolds = holds.GetValidRequests(
                request.ClientInformation.ClientBookingId,
                DateTime.Now,
                HoldDuration,
                out var expiredIds);

            foreach (var expiredId in expiredIds)
                await _holds.DeleteHoldAsync(expiredId, ct);

            request.ClientServices.ValidateSlots(validHolds, cfg, catalog);

            await _holds.PutHoldAsync(request, ct);
        }
        finally
        {
            _bookingLock.Release();
        }

        await _users.UpsertAsync(new AppUser
        {
            UserId   = principal.UserKey,
            Provider = principal.IdentityProvider,
            Email    = principal.Email,
            Name     = $"{request.ClientInformation.FirstName} {request.ClientInformation.LastName}".Trim()
        }, ct);

        return request.ClientInformation.ClientBookingId;
    }

    /// <summary>
    /// The client-submitted cost is never trusted: the charge amount comes
    /// from the stored service metadata (legacy trusted the client here).
    /// </summary>
    static void ApplyAuthoritativeCosts(ClientRequest request, ServiceCollectionResp catalog)
    {
        List<BaseSvcStructure> allServices =
        [
            ..catalog.Nails,
            ..catalog.Lashes,
            ..catalog.Eyebrows,
            ..catalog.Footspa,
            ..catalog.Pedicure
        ];

        foreach (var service in request.ClientServices)
        {
            var definition = allServices.FirstOrDefault(x => IsSameUid(x.Uid, service.ServiceUid))
                ?? throw new InvalidOperationException($"Service configuration was not found for {service.ServiceName}.");

            service.ServiceCost    = definition.Cost;
            service.ServiceDetails = definition.Details;
        }
    }

    static bool IsSameUid(string? first, string? second)
        => string.Equals(ExtractUid(first), ExtractUid(second), StringComparison.OrdinalIgnoreCase);

    static string ExtractUid(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "";

        var cleaned = value.Trim();

        if (!cleaned.Contains('|'))
            return cleaned;

        var parts = cleaned.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        return parts.Length >= 2 ? parts[1] : cleaned;
    }
}
