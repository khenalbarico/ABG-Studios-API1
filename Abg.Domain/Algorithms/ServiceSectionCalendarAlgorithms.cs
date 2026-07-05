using Abg.Domain.Schedules;

namespace Abg.Domain.Algorithms;

public static class ServiceSectionCalendarAlgorithms
{
    public static List<CalendarDay> BuildCalendarDays(
                  DateTime             currentMonth,
                  IEnumerable<string>  allowedDayNames,
                  Func<DateTime, bool> hasAnyAvailableSlotForDate)
    {
        var firstDay    = new DateTime(currentMonth.Year, currentMonth.Month, 1);
        var start       = firstDay.AddDays(-(int)firstDay.DayOfWeek);
        var allowedDays = new HashSet<string>(allowedDayNames ?? [], StringComparer.OrdinalIgnoreCase);

        var days = new List<CalendarDay>();

        for (int i = 0; i < 42; i++)
        {
            var date         = start.AddDays(i);
            var isDayAllowed = allowedDays.Contains(date.DayOfWeek.ToString());
            var hasSlot      = isDayAllowed && hasAnyAvailableSlotForDate(date);

            days.Add(new CalendarDay
            {
                Date           = date,
                IsCurrentMonth = date.Month == currentMonth.Month,
                IsAllowed      = hasSlot,
                Title          = hasSlot ? "Available" : "No available time"
            });
        }

        return days;
    }
}
