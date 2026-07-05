using Abg.Domain.Client;
using Abg.Domain.Contracts;
using FunctionApp1.RateLimiting;
using FunctionApp1.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;

namespace FunctionApp1.Functions;

public class BookingsFunction(BookingCoordinator _bookings, IRateLimiter _rateLimiter)
{
    static readonly TimeSpan RateWindow = TimeSpan.FromMinutes(5);
    const int RateLimit = 10;

    [Function("CreateBooking")]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "bookings")] HttpRequest req,
        CancellationToken ct)
    {
        var principal = req.GetPrincipal();

        if (principal is null)
            return FunctionHelpers.Unauthenticated();

        if (!_rateLimiter.Allow("bookings", principal.UserKey, RateLimit, RateWindow))
            return FunctionHelpers.TooManyRequests();

        var request = await req.ReadBodyAsync<ClientRequest>(ct);

        if (request is null)
            return FunctionHelpers.BadRequest("The booking request could not be read.");

        try
        {
            var bookingId = await _bookings.CreateAsync(request, principal, ct);

            return new OkObjectResult(new CreateBookingResponse { BookingId = bookingId });
        }
        catch (InvalidOperationException ex)
        {
            return FunctionHelpers.Conflict(ex.Message);
        }
    }
}
