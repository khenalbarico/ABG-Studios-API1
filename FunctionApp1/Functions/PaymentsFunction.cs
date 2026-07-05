using Abg.Domain.Contracts;
using FunctionApp1.RateLimiting;
using FunctionApp1.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;

namespace FunctionApp1.Functions;

public class PaymentsFunction(PaymentCoordinator _payments, IRateLimiter _rateLimiter)
{
    [Function("CreateQrphPayment")]
    public async Task<IActionResult> CreateQrph(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "payments/qrph")] HttpRequest req,
        CancellationToken ct)
    {
        var principal = req.GetPrincipal();

        if (principal is null)
            return FunctionHelpers.Unauthenticated();

        if (!_rateLimiter.Allow("payments", principal.UserKey, limit: 5, TimeSpan.FromMinutes(5)))
            return FunctionHelpers.TooManyRequests();

        var body = await req.ReadBodyAsync<CreateQrphPaymentRequest>(ct);

        if (body is null || string.IsNullOrWhiteSpace(body.BookingId))
            return FunctionHelpers.BadRequest("A booking ID is required.");

        try
        {
            var response = await _payments.CreateQrphAsync(body.BookingId, principal, ct);

            return new OkObjectResult(response);
        }
        catch (UnauthorizedAccessException ex)
        {
            return new ObjectResult(new ApiError { Message = ex.Message }) { StatusCode = StatusCodes.Status403Forbidden };
        }
        catch (InvalidOperationException ex)
        {
            return FunctionHelpers.Conflict(ex.Message);
        }
    }

    [Function("GetPaymentStatus")]
    public async Task<IActionResult> GetStatus(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "payments/status")] HttpRequest req,
        CancellationToken ct)
    {
        var principal = req.GetPrincipal();

        if (principal is null)
            return FunctionHelpers.Unauthenticated();

        // Polling endpoint: generous enough for the 3-minute payment window at
        // a 10-15s interval, tight enough to cap runaway clients (CLAUDE.md §6).
        if (!_rateLimiter.Allow("payment-status", principal.UserKey, limit: 30, TimeSpan.FromMinutes(3)))
            return FunctionHelpers.TooManyRequests();

        var bookingId = req.Query["bookingId"].ToString();

        if (string.IsNullOrWhiteSpace(bookingId))
            return FunctionHelpers.BadRequest("A booking ID is required.");

        var status = await _payments.GetStatusAsync(bookingId, ct);

        return new OkObjectResult(status);
    }
}
