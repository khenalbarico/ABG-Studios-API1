using static Abg.Domain.Constants;

namespace Abg.Domain.Schedules;

public sealed class CapacityRule
{
    public CapacitySource          Source              { get; set; }
    public string                  ServiceUid          { get; set; } = "";
    public bool                    IsNails             { get; set; }
    public bool                    IsFootspaOrPedicure { get; set; }
    public string                  SlotKey             { get; set; } = "";
    public int                     Capacity            { get; set; }
    public Dictionary<string, int> Capacities          { get; set; } = [];
}
