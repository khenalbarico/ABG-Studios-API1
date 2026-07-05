using static Abg.Domain.Constants;

namespace Abg.Domain.Schedules;

public sealed class ApptSchedService
{
    public string        ServiceName    { get; set; } = "";
    public string        ServiceDesign  { get; set; } = "";
    public string        ServiceDetails { get; set; } = "";
    public ServiceBranch Branch         { get; set; } = ServiceBranch.Anabu;
}
