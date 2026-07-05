using Azure;
using Azure.Data.Tables;

namespace Abg.Data.Tables;

public sealed class AppUser
{
    public string UserId   { get; set; } = "";
    public string Provider { get; set; } = "";
    public string Email    { get; set; } = "";
    public string Name     { get; set; } = "";
}

public interface IUserStore
{
    Task UpsertAsync(AppUser user, CancellationToken ct = default);
}

public sealed class UserTableStore(TableServiceClient serviceClient, string tableName = TableNames.Users)
    : TableStoreBase(serviceClient, tableName), IUserStore
{
    const string PartitionKey = "user";

    public async Task UpsertAsync(AppUser user, CancellationToken ct = default)
    {
        var table = await GetTableAsync(ct);
        var now   = DateTime.UtcNow;

        var entity = new TableEntity(PartitionKey, user.UserId)
        {
            ["Provider"]     = user.Provider,
            ["Email"]        = user.Email,
            ["Name"]         = user.Name,
            ["LastSeenUtc"]  = now
        };

        try
        {
            entity["FirstSeenUtc"] = now;
            await table.AddEntityAsync(entity, ct);
        }
        catch (RequestFailedException ex) when (ex.Status == 409)
        {
            entity.Remove("FirstSeenUtc");
            await table.UpdateEntityAsync(entity, ETag.All, TableUpdateMode.Merge, ct);
        }
    }
}
