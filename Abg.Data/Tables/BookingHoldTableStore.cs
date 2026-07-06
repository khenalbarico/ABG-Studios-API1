using Abg.Domain.Client;
using Azure;
using Azure.Data.Tables;
using System.Text.Json;

namespace Abg.Data.Tables;

/// <summary>
/// Live booking state: pending client requests holding schedule slots
/// until payment succeeds or the hold expires (CLAUDE.md §5).
/// </summary>
public interface IBookingHoldStore
{
    Task<List<ClientRequest>> GetHoldsAsync(CancellationToken ct = default);
    Task<ClientRequest?>      GetHoldAsync(string bookingId, CancellationToken ct = default);
    Task                      PutHoldAsync(ClientRequest request, CancellationToken ct = default);
    Task                      DeleteHoldAsync(string bookingId, CancellationToken ct = default);
}

public sealed class BookingHoldTableStore(TableServiceClient serviceClient, string tableName = TableNames.BookingHolds)
    : TableStoreBase(serviceClient, tableName), IBookingHoldStore
{
    const string RowKey = "hold";

    public async Task<List<ClientRequest>> GetHoldsAsync(CancellationToken ct = default)
    {
        var table = await GetTableAsync(ct);
        var holds = new List<ClientRequest>();

        // Full scan is intentional: the table only ever contains active holds
        // (deleted on payment success or expiry), so it stays a handful of rows.
        await foreach (var entity in table.QueryAsync<TableEntity>(cancellationToken: ct))
        {
            var hold = Deserialize(entity);

            if (hold is not null)
                holds.Add(hold);
        }

        return holds;
    }

    public async Task<ClientRequest?> GetHoldAsync(string bookingId, CancellationToken ct = default)
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

    public async Task PutHoldAsync(ClientRequest request, CancellationToken ct = default)
    {
        var table     = await GetTableAsync(ct);
        var bookingId = request.ClientInformation.ClientBookingId;

        var entity = new TableEntity(bookingId, RowKey)
        {
            ["RequestJson"] = JsonSerializer.Serialize(request),
            ["Status"]      = request.Status.ToString()
        };

        await table.UpsertEntityAsync(entity, TableUpdateMode.Replace, ct);
    }

    public async Task DeleteHoldAsync(string bookingId, CancellationToken ct = default)
    {
        var table = await GetTableAsync(ct);

        await table.DeleteEntityAsync(bookingId, RowKey, cancellationToken: ct);
    }

    private static ClientRequest? Deserialize(TableEntity entity)
    {
        var json = entity.GetString("RequestJson");

        if (string.IsNullOrWhiteSpace(json))
            return null;

        return JsonSerializer.Deserialize<ClientRequest>(json);
    }
}
