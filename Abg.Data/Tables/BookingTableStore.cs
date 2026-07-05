using Abg.Domain.Client;
using Azure;
using Azure.Data.Tables;
using System.Globalization;
using System.Text.Json;
using static Abg.Domain.Constants;

namespace Abg.Data.Tables;

public interface IBookingStore
{
    Task<ClientRequest?> GetAsync(string bookingId, CancellationToken ct = default);
    Task UpsertAsync(ClientRequest request, string userId = "", CancellationToken ct = default);
    Task UpdateStatusAsync(string bookingId, ClientStatus status, CancellationToken ct = default);
    Task UpdateServiceStatusAsync(string bookingId, string serviceUid, ClientServiceStatus status, CancellationToken ct = default);
}

public sealed class BookingTableStore(TableServiceClient serviceClient, string tableName = TableNames.Bookings)
    : TableStoreBase(serviceClient, tableName), IBookingStore
{
    const string RowKey = "booking";

    public async Task<ClientRequest?> GetAsync(string bookingId, CancellationToken ct = default)
    {
        var table = await GetTableAsync(ct);

        try
        {
            var entity = await table.GetEntityAsync<TableEntity>(bookingId, RowKey, cancellationToken: ct);

            return Deserialize(entity.Value);
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return null;
        }
    }

    public async Task UpsertAsync(ClientRequest request, string userId = "", CancellationToken ct = default)
    {
        var table     = await GetTableAsync(ct);
        var bookingId = request.ClientInformation.ClientBookingId;

        var entity = new TableEntity(bookingId, RowKey)
        {
            ["RequestJson"] = JsonSerializer.Serialize(request),
            ["Status"]      = request.Status.ToString(),
            ["Email"]       = request.ClientInformation.Email,
            ["UserId"]      = userId,
            ["BookingDate"] = request.ClientInformation.BookingDate.ToString("o", CultureInfo.InvariantCulture)
        };

        await table.UpsertEntityAsync(entity, TableUpdateMode.Replace, ct);
    }

    public async Task UpdateStatusAsync(string bookingId, ClientStatus status, CancellationToken ct = default)
    {
        await MutateAsync(bookingId, request => request.Status = status, ct);
    }

    public async Task UpdateServiceStatusAsync(string bookingId, string serviceUid, ClientServiceStatus status, CancellationToken ct = default)
    {
        await MutateAsync(bookingId, request =>
        {
            var target = request.ClientServices.FirstOrDefault(x => x.ServiceUid == serviceUid);

            if (target is not null)
                target.Status = status;
        }, ct);
    }

    private async Task MutateAsync(string bookingId, Action<ClientRequest> mutate, CancellationToken ct)
    {
        var table = await GetTableAsync(ct);

        TableEntity entity;

        try
        {
            entity = await table.GetEntityAsync<TableEntity>(bookingId, RowKey, cancellationToken: ct);
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return;
        }

        var request = Deserialize(entity);

        if (request is null)
            return;

        mutate(request);

        entity["RequestJson"] = JsonSerializer.Serialize(request);
        entity["Status"]      = request.Status.ToString();

        await table.UpdateEntityAsync(entity, entity.ETag, TableUpdateMode.Replace, ct);
    }

    private static ClientRequest? Deserialize(TableEntity entity)
    {
        var json = entity.GetString("RequestJson");

        if (string.IsNullOrWhiteSpace(json))
            return null;

        return JsonSerializer.Deserialize<ClientRequest>(json);
    }
}
