using Abg.Domain.__Base__;
using Abg.Domain.Service;
using Azure.Data.Tables;
using System.Globalization;
using System.Text.Json;

namespace Abg.Data.Tables;

public interface IServiceStore
{
    Task<ServiceCollectionResp> GetAllAsync(CancellationToken ct = default);
    Task UpsertAsync(string category, BaseSvcStructure service, CancellationToken ct = default);
    Task DeleteAsync(string category, string serviceUid, CancellationToken ct = default);
}

public sealed class ServiceTableStore(TableServiceClient serviceClient, string tableName = TableNames.Services)
    : TableStoreBase(serviceClient, tableName), IServiceStore
{
    public static class Categories
    {
        public const string Nails    = "Nails";
        public const string Lash     = "Lash";
        public const string Eyebrows = "Eyebrows";
        public const string Footspa  = "Footspa";
        public const string Pedicure = "Pedicure";

        public static readonly string[] All = [Nails, Lash, Eyebrows, Footspa, Pedicure];
    }

    public async Task<ServiceCollectionResp> GetAllAsync(CancellationToken ct = default)
    {
        var table = await GetTableAsync(ct);

        var nailsTask    = GetCategoryAsync<NailsService>(table, Categories.Nails, ct);
        var lashesTask   = GetCategoryAsync<LashesService>(table, Categories.Lash, ct);
        var eyebrowsTask = GetCategoryAsync<EyebrowsService>(table, Categories.Eyebrows, ct);
        var footspaTask  = GetCategoryAsync<FootspaService>(table, Categories.Footspa, ct);
        var pedicureTask = GetCategoryAsync<PedicureService>(table, Categories.Pedicure, ct);

        await Task.WhenAll(nailsTask, lashesTask, eyebrowsTask, footspaTask, pedicureTask);

        return new ServiceCollectionResp
        {
            Nails    = nailsTask.Result,
            Lashes   = lashesTask.Result,
            Eyebrows = eyebrowsTask.Result,
            Footspa  = footspaTask.Result,
            Pedicure = pedicureTask.Result
        };
    }

    public async Task UpsertAsync(string category, BaseSvcStructure service, CancellationToken ct = default)
    {
        ValidateCategory(category);

        var table = await GetTableAsync(ct);

        var entity = new TableEntity(category, service.Uid)
        {
            ["Cost"]              = service.Cost.ToString(CultureInfo.InvariantCulture),
            ["Details"]           = service.Details,
            ["ScheduleSlotsJson"] = JsonSerializer.Serialize(service.ScheduleSlots ?? new BaseSchedSlot())
        };

        await table.UpsertEntityAsync(entity, TableUpdateMode.Replace, ct);
    }

    public async Task DeleteAsync(string category, string serviceUid, CancellationToken ct = default)
    {
        ValidateCategory(category);

        var table = await GetTableAsync(ct);

        await table.DeleteEntityAsync(category, serviceUid, cancellationToken: ct);
    }

    private static void ValidateCategory(string category)
    {
        if (!Categories.All.Contains(category.Trim()))
            throw new InvalidOperationException($"Unsupported service category: {category}");
    }

    private static async Task<List<T>> GetCategoryAsync<T>(TableClient table, string category, CancellationToken ct)
        where T : BaseSvcStructure, new()
    {
        var services = new List<T>();

        await foreach (var entity in table.QueryAsync<TableEntity>(x => x.PartitionKey == category, cancellationToken: ct))
        {
            services.Add(new T
            {
                Uid           = entity.RowKey,
                Cost          = decimal.TryParse(entity.GetString("Cost"), NumberStyles.Number, CultureInfo.InvariantCulture, out var cost) ? cost : 0m,
                Details       = entity.GetString("Details") ?? "",
                ScheduleSlots = Deserialize(entity.GetString("ScheduleSlotsJson"))
            });
        }

        return services;
    }

    private static BaseSchedSlot Deserialize(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return new BaseSchedSlot();

        return JsonSerializer.Deserialize<BaseSchedSlot>(json) ?? new BaseSchedSlot();
    }
}
