using Abg.Domain.Schedules;
using Azure;
using Azure.Data.Tables;
using System.Text.Json;

namespace Abg.Data.Tables;

public interface IScheduleConfigStore
{
    Task<ScheduleCfg> GetAsync(CancellationToken ct = default);
    Task UpsertAsync(ScheduleCfg cfg, CancellationToken ct = default);
}

public sealed class ScheduleConfigTableStore(TableServiceClient serviceClient, string tableName = TableNames.ScheduleConfig)
    : TableStoreBase(serviceClient, tableName), IScheduleConfigStore
{
    const string PartitionKey = "config";
    const string RowKey       = "current";

    public async Task<ScheduleCfg> GetAsync(CancellationToken ct = default)
    {
        var table = await GetTableAsync(ct);

        try
        {
            var entity = await table.GetEntityAsync<TableEntity>(PartitionKey, RowKey, cancellationToken: ct);
            var json   = entity.Value.GetString("ConfigJson");

            if (string.IsNullOrWhiteSpace(json))
                return new ScheduleCfg();

            return JsonSerializer.Deserialize<ScheduleCfg>(json) ?? new ScheduleCfg();
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return new ScheduleCfg();
        }
    }

    public async Task UpsertAsync(ScheduleCfg cfg, CancellationToken ct = default)
    {
        var table = await GetTableAsync(ct);

        var entity = new TableEntity(PartitionKey, RowKey)
        {
            ["ConfigJson"] = JsonSerializer.Serialize(cfg)
        };

        await table.UpsertEntityAsync(entity, TableUpdateMode.Replace, ct);
    }
}
