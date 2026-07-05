using Azure.Data.Tables;

namespace Abg.Data.Tables;

public abstract class TableStoreBase(TableServiceClient _serviceClient, string _tableName)
{
    TableClient? _tableClient;

    protected async Task<TableClient> GetTableAsync(CancellationToken ct)
    {
        if (_tableClient is not null)
            return _tableClient;

        var client = _serviceClient.GetTableClient(_tableName);
        await client.CreateIfNotExistsAsync(ct);

        _tableClient = client;

        return client;
    }
}
