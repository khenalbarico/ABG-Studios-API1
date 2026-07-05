using Abg.Data.Paymongo.Models;
using Azure.Data.Tables;
using System.Globalization;

namespace Abg.Data.Tables;

public interface IPurchaseStore
{
    Task UpsertAsync(string bookingId, PaymongoQrphChargeResult charge, string status, CancellationToken ct = default);
}

public sealed class PurchaseTableStore(TableServiceClient serviceClient, string tableName = TableNames.Purchases)
    : TableStoreBase(serviceClient, tableName), IPurchaseStore
{
    public async Task UpsertAsync(string bookingId, PaymongoQrphChargeResult charge, string status, CancellationToken ct = default)
    {
        var table = await GetTableAsync(ct);

        var entity = new TableEntity(bookingId, charge.PaymentIntentId)
        {
            ["Status"]          = status,
            ["AmountCentavos"]  = charge.AmountCentavos,
            ["AmountPhp"]       = charge.AmountPhp.ToString(CultureInfo.InvariantCulture),
            ["PaymentMethodId"] = charge.PaymentMethodId,
            ["QrCodeId"]        = charge.QrCodeId,
            ["RecordedUtc"]     = DateTime.UtcNow
        };

        await table.UpsertEntityAsync(entity, TableUpdateMode.Replace, ct);
    }
}
