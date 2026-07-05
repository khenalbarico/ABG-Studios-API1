using Abg.Data.Tables;
using Abg.Domain.Contracts;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using System.Globalization;

namespace FunctionApp1.Functions;

public class CatalogFunction(
    IServiceStore        _services,
    IScheduleConfigStore _scheduleConfig,
    IAppointmentStore    _appointments)
{
    /// <summary>Days of occupancy returned when the caller does not pass a range: current + next month.</summary>
    const int DefaultRangeDays = 62;

    [Function("GetCatalog")]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "catalog")] HttpRequest req,
        CancellationToken ct)
    {
        var from = ParseDate(req.Query["from"], DateTime.Today);
        var to   = ParseDate(req.Query["to"], from.AddDays(DefaultRangeDays));

        var servicesTask     = _services.GetAllAsync(ct);
        var configTask       = _scheduleConfig.GetAsync(ct);
        var appointmentsTask = _appointments.GetRangeAsync(from, to, ct);

        await Task.WhenAll(servicesTask, configTask, appointmentsTask);

        return new OkObjectResult(new CatalogResponse
        {
            Services     = servicesTask.Result,
            ScheduleCfg  = configTask.Result,
            Appointments = appointmentsTask.Result
        });
    }

    private static DateTime ParseDate(string? value, DateTime fallback)
    {
        if (DateTime.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
            return parsed;

        return fallback;
    }
}
