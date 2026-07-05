using System.ComponentModel.DataAnnotations;

namespace Abg.Domain.__Base__;

public class BaseSvcStructure
{
    [Required] public string        Uid           { get; set; } = "";
               public decimal       Cost          { get; set; }
               public string        Details       { get; set; } = "";
               public BaseSchedSlot ScheduleSlots { get; set; } = new BaseSchedSlot();
}
