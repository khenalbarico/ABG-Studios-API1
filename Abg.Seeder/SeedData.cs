using Abg.Domain.Schedules;
using Abg.Domain.__Base__;

namespace Abg.Seeder;

public sealed class SeedService
{
    public string        Uid           { get; set; } = "";
    public decimal       Cost          { get; set; }
    public string        Details       { get; set; } = "";
    public BaseSchedSlot ScheduleSlots { get; set; } = new();
}

public sealed class SeedData
{
    /// <summary>Service metadata keyed by category (Nails, Lash, Eyebrows, Footspa, Pedicure).</summary>
    public Dictionary<string, List<SeedService>> Services { get; set; } = [];

    /// <summary>Optional schedule configuration; skipped when absent.</summary>
    public ScheduleCfg? ScheduleCfg { get; set; }
}

public sealed class SeedReport
{
    public int  ServicesUpserted   { get; set; }
    public bool ScheduleCfgWritten { get; set; }
    public bool WasDryRun          { get; set; }
    public List<string> Log        { get; } = [];
}
