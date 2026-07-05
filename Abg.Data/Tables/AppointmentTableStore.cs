using Abg.Domain.Client;
using Abg.Domain.Helpers;
using Abg.Domain.Schedules;
using Azure.Data.Tables;
using System.Globalization;
using static Abg.Domain.Constants;

namespace Abg.Data.Tables;

public interface IAppointmentStore
{
    Task AddForBookingAsync(ClientRequest request, CancellationToken ct = default);
    Task<List<ApptSchedRec>> GetRangeAsync(DateTime fromDate, DateTime toDate, CancellationToken ct = default);
}

/// <summary>
/// Occupancy ledger for confirmed appointments. Partitioned by service date
/// (yyyy-MM-dd) so availability reads only fetch the date range on screen.
/// </summary>
public sealed class AppointmentTableStore(TableServiceClient serviceClient, string tableName = TableNames.Appointments)
    : TableStoreBase(serviceClient, tableName), IAppointmentStore
{
    public async Task AddForBookingAsync(ClientRequest request, CancellationToken ct = default)
    {
        var table     = await GetTableAsync(ct);
        var bookingId = request.ClientInformation.ClientBookingId;

        var rows = (request.ClientServices ?? [])
            .Select((service, index) => new TableEntity(
                PartitionFor(service.ServiceDate),
                $"{service.ServiceDate:HHmm}_{bookingId}_{index}")
            {
                ["ServiceDateKey"]  = service.ServiceDate.ToServiceDateKey(),
                ["ClientBookingId"] = bookingId,
                ["ServiceName"]     = service.ServiceName,
                ["ServiceDesign"]   = service.ServiceDesign,
                ["ServiceDetails"]  = service.ServiceDetails,
                ["Branch"]          = (int)service.Branch
            });

        foreach (var row in rows)
            await table.UpsertEntityAsync(row, TableUpdateMode.Replace, ct);
    }

    public async Task<List<ApptSchedRec>> GetRangeAsync(DateTime fromDate, DateTime toDate, CancellationToken ct = default)
    {
        var table = await GetTableAsync(ct);

        var from = PartitionFor(fromDate);
        var to   = PartitionFor(toDate);

        var grouped = new Dictionary<(string DateKey, string BookingId), ApptSchedRec>();

        await foreach (var entity in table.QueryAsync<TableEntity>(
            x => x.PartitionKey.CompareTo(from) >= 0 && x.PartitionKey.CompareTo(to) <= 0,
            cancellationToken: ct))
        {
            var dateKey = entity.GetString("ServiceDateKey") ?? "";

            if (!dateKey.TryParseServiceDateKey(out var serviceDate))
                continue;

            var bookingId = entity.GetString("ClientBookingId") ?? "";
            var groupKey  = (dateKey, bookingId);

            if (!grouped.TryGetValue(groupKey, out var record))
            {
                record = new ApptSchedRec
                {
                    ServiceDateKey  = dateKey,
                    ServiceDate     = serviceDate,
                    ClientBookingId = bookingId
                };

                grouped[groupKey] = record;
            }

            record.Services.Add(new ApptSchedService
            {
                ServiceName    = entity.GetString("ServiceName") ?? "",
                ServiceDesign  = entity.GetString("ServiceDesign") ?? "",
                ServiceDetails = entity.GetString("ServiceDetails") ?? "",
                Branch         = (ServiceBranch)(entity.GetInt32("Branch") ?? 0)
            });
        }

        return [.. grouped.Values];
    }

    private static string PartitionFor(DateTime serviceDate)
        => serviceDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
}
