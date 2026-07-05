using Abg.Data.Tables;
using Abg.Domain.Schedules;
using Abg.Domain.Service;
using Abg.Domain.__Base__;

namespace Abg.Seeder.Tests;

public class SeedRunnerTests
{
    sealed class RecordingServiceStore : IServiceStore
    {
        public List<(string Category, BaseSvcStructure Service)> Upserts { get; } = [];

        public Task<ServiceCollectionResp> GetAllAsync(CancellationToken ct = default)
            => Task.FromResult(new ServiceCollectionResp());

        public Task UpsertAsync(string category, BaseSvcStructure service, CancellationToken ct = default)
        {
            Upserts.Add((category, service));
            return Task.CompletedTask;
        }

        public Task DeleteAsync(string category, string serviceUid, CancellationToken ct = default)
            => Task.CompletedTask;
    }

    sealed class RecordingConfigStore : IScheduleConfigStore
    {
        public ScheduleCfg? Written { get; private set; }

        public Task<ScheduleCfg> GetAsync(CancellationToken ct = default)
            => Task.FromResult(Written ?? new ScheduleCfg());

        public Task UpsertAsync(ScheduleCfg cfg, CancellationToken ct = default)
        {
            Written = cfg;
            return Task.CompletedTask;
        }
    }

    static SeedData Data(bool withCfg = false) => new()
    {
        Services = new Dictionary<string, List<SeedService>>
        {
            ["Nails"] = [new SeedService { Uid = "NAS-100", Cost = 500m, Details = "Gel polish" }],
            ["Lash"]  = [new SeedService { Uid = "LAS-200", Cost = 700m, Details = "Classic set" }]
        },
        ScheduleCfg = withCfg ? new ScheduleCfg { StoreHours = ["10:00 AM - 7:00 PM"] } : null
    };

    [Fact]
    public async Task Upserts_every_service_and_reports_counts()
    {
        var services = new RecordingServiceStore();
        var config   = new RecordingConfigStore();
        var runner   = new SeedRunner(services, config);

        var report = await runner.RunAsync(Data(withCfg: true), dryRun: false);

        Assert.Equal(2, report.ServicesUpserted);
        Assert.True(report.ScheduleCfgWritten);
        Assert.Equal(2, services.Upserts.Count);
        Assert.Contains(services.Upserts, x => x.Category == "Nails" && x.Service.Uid == "NAS-100" && x.Service.Cost == 500m);
        Assert.NotNull(config.Written);
    }

    [Fact]
    public async Task Dry_run_writes_nothing_but_reports_everything()
    {
        var services = new RecordingServiceStore();
        var config   = new RecordingConfigStore();
        var runner   = new SeedRunner(services, config);

        var report = await runner.RunAsync(Data(withCfg: true), dryRun: true);

        Assert.True(report.WasDryRun);
        Assert.Equal(2, report.ServicesUpserted);
        Assert.Empty(services.Upserts);
        Assert.Null(config.Written);
        Assert.All(report.Log, line => Assert.StartsWith("[dry-run]", line));
    }

    [Fact]
    public async Task Skips_schedule_config_when_absent()
    {
        var config = new RecordingConfigStore();
        var runner = new SeedRunner(new RecordingServiceStore(), config);

        var report = await runner.RunAsync(Data(withCfg: false), dryRun: false);

        Assert.False(report.ScheduleCfgWritten);
        Assert.Null(config.Written);
    }
}
