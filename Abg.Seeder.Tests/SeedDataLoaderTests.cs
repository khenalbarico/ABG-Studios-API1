using Abg.Seeder;

namespace Abg.Seeder.Tests;

public class SeedDataLoaderTests
{
    const string ValidServicesJson = """
        {
          "Nails": [
            { "Uid": "NAS-100", "Cost": 500, "Details": "Gel polish",
              "ScheduleSlots": { "IsAvailable": true, "DaySlots": ["Monday"], "TimeSlots": ["10:00 AM - 11:00 AM"] } }
          ],
          "Lash": [
            { "Uid": "LAS-200", "Cost": 700, "Details": "Classic set" }
          ]
        }
        """;

    [Fact]
    public void Loads_services_per_category()
    {
        var data = SeedDataLoader.Load(ValidServicesJson);

        Assert.Equal(2, data.Services.Count);
        Assert.Equal("NAS-100", data.Services["Nails"].Single().Uid);
        Assert.Equal(500m, data.Services["Nails"].Single().Cost);
        Assert.True(data.Services["Nails"].Single().ScheduleSlots.IsAvailable);
        Assert.Null(data.ScheduleCfg);
    }

    [Fact]
    public void Loads_optional_schedule_config()
    {
        var data = SeedDataLoader.Load(ValidServicesJson, """
            { "NailsAccommodationCapacities": { "10:00 AM": 2 } }
            """);

        Assert.NotNull(data.ScheduleCfg);
        Assert.Equal(2, data.ScheduleCfg.NailsAccommodationCapacities["10:00 AM"]);
    }

    [Fact]
    public void Rejects_unknown_category()
    {
        var ex = Assert.Throws<InvalidOperationException>(() => SeedDataLoader.Load("""
            { "Massage": [ { "Uid": "MAS-100", "Cost": 100 } ] }
            """));

        Assert.Contains("Massage", ex.Message);
    }

    [Fact]
    public void Rejects_service_without_uid()
    {
        var ex = Assert.Throws<InvalidOperationException>(() => SeedDataLoader.Load("""
            { "Nails": [ { "Cost": 100, "Details": "No uid" } ] }
            """));

        Assert.Contains("Uid", ex.Message);
    }

    [Fact]
    public void Rejects_negative_cost()
    {
        var ex = Assert.Throws<InvalidOperationException>(() => SeedDataLoader.Load("""
            { "Nails": [ { "Uid": "NAS-100", "Cost": -5 } ] }
            """));

        Assert.Contains("negative", ex.Message);
    }

    [Fact]
    public void Rejects_empty_map()
        => Assert.Throws<InvalidOperationException>(() => SeedDataLoader.Load("{}"));
}
