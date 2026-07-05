using Abg.Domain.Client;
using static Abg.Domain.Constants;

namespace Abg.Data.Firebase;

/// <summary>
/// Live booking state: pending client requests holding schedule slots
/// until payment succeeds or the hold expires (CLAUDE.md §5).
/// </summary>
public interface IBookingHoldStore
{
    Task<List<ClientRequest>> GetHoldsAsync(CancellationToken ct = default);
    Task<ClientRequest?>      GetHoldAsync(string bookingId, CancellationToken ct = default);
    Task                      PutHoldAsync(ClientRequest request, CancellationToken ct = default);
    Task                      SetHoldStatusAsync(string bookingId, ClientStatus status, CancellationToken ct = default);
    Task                      DeleteHoldAsync(string bookingId, CancellationToken ct = default);
}
