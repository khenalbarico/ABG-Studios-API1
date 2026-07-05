namespace Abg.Domain.__Base__;

public class BaseSchedSlot
{
    public List<string> DaySlots    { get; set; } = [];
    public bool         IsAvailable { get; set; }
    public List<string> TimeSlots   { get; set; } = [];
}
