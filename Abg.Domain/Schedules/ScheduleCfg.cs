namespace Abg.Domain.Schedules;

public class ScheduleCfg
{
    public List<string>                              StoreHours                            { get; set; } = [];
    public Dictionary<string, int>                   NailsAccommodationCapacities          { get; set; } = [];
    public Dictionary<string, int>                   OtherServicesAccommodationCapacities  { get; set; } = [];
    public List<ThisServiceDateAccomodationCapacity> CustomizedServiceAccomodationCapacity { get; set; } = [];
    public List<ThisDayAccomodationCapacity>         CustomizedDayAccomodationCapacity     { get; set; } = [];
}
