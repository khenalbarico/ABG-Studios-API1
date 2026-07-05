namespace Abg.Domain.Schedules;

public class ThisDayAccomodationCapacity
{
    public string                  Uid                          { get; set; } = "";
    public string                  Day                          { get; set; } = "";
    public Dictionary<string, int> NailsAccommodationCapacities { get; set; } = [];
}
