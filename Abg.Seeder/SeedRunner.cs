using Abg.Data.Tables;
using Abg.Domain.__Base__;

namespace Abg.Seeder;

public sealed class SeedRunner(IServiceStore _services, IScheduleConfigStore _scheduleConfig)
{
    public async Task<SeedReport> RunAsync(SeedData data, bool dryRun, CancellationToken ct = default)
    {
        var report = new SeedReport { WasDryRun = dryRun };

        foreach (var (category, services) in data.Services)
        {
            foreach (var service in services)
            {
                report.Log.Add($"{(dryRun ? "[dry-run] " : "")}{category}/{service.Uid}: {service.Details} @ {service.Cost}");

                if (!dryRun)
                {
                    await _services.UpsertAsync(category, new BaseSvcStructure
                    {
                        Uid           = service.Uid,
                        Cost          = service.Cost,
                        Details       = service.Details,
                        ScheduleSlots = service.ScheduleSlots
                    }, ct);
                }

                report.ServicesUpserted++;
            }
        }

        if (data.ScheduleCfg is not null)
        {
            report.Log.Add($"{(dryRun ? "[dry-run] " : "")}ScheduleConfig: replaced current configuration");

            if (!dryRun)
                await _scheduleConfig.UpsertAsync(data.ScheduleCfg, ct);

            report.ScheduleCfgWritten = true;
        }

        return report;
    }
}
