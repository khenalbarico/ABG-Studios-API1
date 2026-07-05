namespace Abg.Domain.Schedules;

public sealed class CalendarDay
{
    public DateTime Date           { get; set; }
    public bool     IsCurrentMonth { get; set; }
    public bool     IsAllowed      { get; set; }
    public string   Title          { get; set; } = "";
}
