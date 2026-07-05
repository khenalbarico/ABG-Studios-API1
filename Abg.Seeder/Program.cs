using Abg.Data.Tables;
using Abg.Seeder;
using Azure.Data.Tables;
using Microsoft.Extensions.Configuration;

// Usage:
//   dotnet run --project Abg.Seeder -- [--env Development|Production] [--data <folder>] [--dry-run]
//
// Reads <data>/services.json (required) and <data>/schedulecfg.json (optional)
// and upserts them into the environment's Table Storage. Re-running is safe:
// rows are keyed by category + service UID.

var env     = GetOption(args, "--env") ?? "Development";
var dataDir = GetOption(args, "--data") ?? "data";
var dryRun  = args.Contains("--dry-run");

if (env is not ("Development" or "Production"))
{
    Console.Error.WriteLine($"Unknown environment '{env}'. Use Development or Production.");
    return 1;
}

var configuration = new ConfigurationBuilder()
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json", optional: true)
    .AddJsonFile($"appsettings.{env}.json", optional: false)
    .Build();

var connectionString = configuration["TablesConnectionString"];

if (string.IsNullOrWhiteSpace(connectionString))
{
    Console.Error.WriteLine($"TablesConnectionString is not set in appsettings.{env}.json.");
    return 1;
}

var servicesPath    = Path.Combine(dataDir, "services.json");
var scheduleCfgPath = Path.Combine(dataDir, "schedulecfg.json");

if (!File.Exists(servicesPath))
{
    Console.Error.WriteLine($"Missing {servicesPath}. See data/services.sample.json for the expected shape.");
    return 1;
}

SeedData data;

try
{
    data = SeedDataLoader.Load(
        await File.ReadAllTextAsync(servicesPath),
        File.Exists(scheduleCfgPath) ? await File.ReadAllTextAsync(scheduleCfgPath) : null);
}
catch (Exception ex)
{
    Console.Error.WriteLine($"Seed data is invalid: {ex.Message}");
    return 1;
}

Console.WriteLine($"Seeding environment: {env}{(dryRun ? " (dry run — nothing will be written)" : "")}");

var tableClient = new TableServiceClient(connectionString);
var runner      = new SeedRunner(new ServiceTableStore(tableClient), new ScheduleConfigTableStore(tableClient));
var report      = await runner.RunAsync(data, dryRun);

foreach (var line in report.Log)
    Console.WriteLine("  " + line);

Console.WriteLine($"Done. Services upserted: {report.ServicesUpserted}. Schedule config written: {report.ScheduleCfgWritten}.");

return 0;

static string? GetOption(string[] args, string name)
{
    var index = Array.IndexOf(args, name);

    return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
}
