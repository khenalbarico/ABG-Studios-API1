namespace Abg.Domain.Schedules;

public sealed class ApptSchedRec
{
    public string                 ServiceDateKey  { get; set; } = "";
    public DateTime               ServiceDate     { get; set; }
    public string                 ClientBookingId { get; set; } = "";
    public List<ApptSchedService> Services        { get; set; } = [];
}
