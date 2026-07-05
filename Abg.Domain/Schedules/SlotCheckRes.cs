namespace Abg.Domain.Schedules;

public sealed class SlotCheckRes
{
    public bool   IsAvailable { get; set; }
    public string Message     { get; set; } = "";
}
