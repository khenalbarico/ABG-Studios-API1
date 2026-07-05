using Abg.Data.Tables;
using Abg.Domain.Schedules;
using System.Text.Json;

namespace Abg.Seeder;

public static class SeedDataLoader
{
    static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling         = JsonCommentHandling.Skip,
        AllowTrailingCommas         = true
    };

    public static SeedData Load(string servicesJson, string? scheduleCfgJson = null)
    {
        var services = JsonSerializer.Deserialize<Dictionary<string, List<SeedService>>>(servicesJson, JsonOptions)
            ?? throw new InvalidOperationException("services.json is empty or not a category-to-services map.");

        var data = new SeedData { Services = services };

        Validate(data);

        if (!string.IsNullOrWhiteSpace(scheduleCfgJson))
        {
            data.ScheduleCfg = JsonSerializer.Deserialize<ScheduleCfg>(scheduleCfgJson, JsonOptions)
                ?? throw new InvalidOperationException("schedulecfg.json could not be parsed.");
        }

        return data;
    }

    static void Validate(SeedData data)
    {
        if (data.Services.Count == 0)
            throw new InvalidOperationException("services.json contains no categories.");

        foreach (var (category, services) in data.Services)
        {
            if (!ServiceTableStore.Categories.All.Contains(category))
                throw new InvalidOperationException(
                    $"Unknown category '{category}'. Expected one of: {string.Join(", ", ServiceTableStore.Categories.All)}.");

            foreach (var service in services)
            {
                // A stable UID is what makes re-running the seeder idempotent.
                if (string.IsNullOrWhiteSpace(service.Uid))
                    throw new InvalidOperationException(
                        $"A service under '{category}' has no Uid. Every service needs a stable Uid (e.g. NAS-100).");

                if (service.Cost < 0)
                    throw new InvalidOperationException(
                        $"Service '{service.Uid}' has a negative cost.");
            }
        }
    }
}
