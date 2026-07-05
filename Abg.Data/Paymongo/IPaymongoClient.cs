using Abg.Data.Paymongo.Models;
using Abg.Domain.Client;

namespace Abg.Data.Paymongo;

public interface IPaymongoClient
{
    Task<PaymongoQrphChargeResult> CreateQrphChargeAsync(
         ClientRequest     req,
         CancellationToken ct = default);

    Task<string> GetPaymentIntentStatusAsync(
         string            paymentIntentId,
         CancellationToken ct = default);

    Task<string> GetPaymentIntentClientBookingIdAsync(
         string            paymentIntentId,
         CancellationToken ct = default);
}
