using Abg.Domain.__Base__;
using Abg.Domain.Client;
using Abg.Domain.Schedules;
using System.Globalization;
using static Abg.Domain.Constants;

namespace Abg.Domain.Algorithms;

public static class ServiceSectionCapacityAlgorithms
{
    public static bool IsTimeSlotFull(
        BaseSvcStructure svc,
        string title,
        DateTime date,
        string timeSlot,
        List<ApptSchedRec> appointmentSchedules,
        ScheduleCfg scheduleCfg)
    {
        return IsTimeSlotFull(
            svc,
            title,
            date,
            timeSlot,
            appointmentSchedules,
            [],
            scheduleCfg,
            "");
    }

    public static bool IsTimeSlotFull(
        BaseSvcStructure svc,
        string title,
        DateTime date,
        string timeSlot,
        List<ApptSchedRec> appointmentSchedules,
        List<ClientService> currentBookings,
        ScheduleCfg scheduleCfg,
        string currentCardKey)
    {
        var slotDateTime = ServiceSectionTimeAlgorithms.CombineDateAndTime(date, timeSlot);
        var rule = ResolveCapacityRule(svc, title, slotDateTime, scheduleCfg);

        if (rule.Capacity <= 0)
            return true;

        var existingCount = CountExistingBookings(
            svc,
            title,
            slotDateTime,
            appointmentSchedules,
            rule);

        var currentCount = CountCurrentBookings(
            slotDateTime,
            currentBookings,
            currentCardKey,
            rule);

        return existingCount + currentCount >= rule.Capacity;
    }

    public static string GetFullSlotLabel(BaseSvcStructure svc, string title, string timeSlot)
    {
        if (IsFootspaOrPedicureService(title))
            return $"{timeSlot} - Booked by Footspa or Pedicure";

        return $"{timeSlot} - Full";
    }

    public static string GetFullSlotLabel(
        BaseSvcStructure svc,
        string title,
        DateTime date,
        string timeSlot,
        ScheduleCfg scheduleCfg)
    {
        var slotDateTime = ServiceSectionTimeAlgorithms.CombineDateAndTime(date, timeSlot);
        var rule = ResolveCapacityRule(svc, title, slotDateTime, scheduleCfg);

        return rule.Source switch
        {
            CapacitySource.CustomizedServiceDate when rule.Capacity <= 0 => $"{timeSlot} - Closed by date capacity",
            CapacitySource.CustomizedServiceDate => $"{timeSlot} - Date capacity full",
            CapacitySource.CustomizedDay when rule.Capacity <= 0 => $"{timeSlot} - Closed by day capacity",
            CapacitySource.CustomizedDay => $"{timeSlot} - Day capacity full",
            _ when IsFootspaOrPedicureService(title) => $"{timeSlot} - Booked by Footspa or Pedicure",
            _ when rule.Capacity <= 0 => $"{timeSlot} - Not available",
            _ => $"{timeSlot} - Full"
        };
    }

    public static string GetFullSlotMessage(BaseSvcStructure svc, string title, string timeSlot)
    {
        if (IsFootspaOrPedicureService(title))
            return "This time is not available because it is already booked by a Footspa or Pedicure appointment.";

        return "This time slot is already full for selected bookings.";
    }

    public static string GetFullSlotMessage(
        BaseSvcStructure svc,
        string title,
        DateTime date,
        string timeSlot,
        ScheduleCfg scheduleCfg)
    {
        var slotDateTime = ServiceSectionTimeAlgorithms.CombineDateAndTime(date, timeSlot);
        var rule = ResolveCapacityRule(svc, title, slotDateTime, scheduleCfg);

        var serviceName = string.IsNullOrWhiteSpace(svc.Details)
            ? title
            : svc.Details;

        var dateText = slotDateTime.ToString("MMMM dd, yyyy", CultureInfo.InvariantCulture);
        var dayText = slotDateTime.ToString("dddd", CultureInfo.InvariantCulture);
        var timeText = slotDateTime.ToString("h:mm tt", CultureInfo.InvariantCulture);

        return rule.Source switch
        {
            CapacitySource.CustomizedServiceDate when rule.Capacity <= 0 =>
                $"{serviceName} is not available on {dateText} at {timeText} because the date capacity is set to 0.",

            CapacitySource.CustomizedServiceDate =>
                $"{serviceName} has already reached the date capacity for {dateText} at {timeText}.",

            CapacitySource.CustomizedDay when rule.Capacity <= 0 =>
                $"{serviceName} is not available every {dayText} at {timeText} because the day capacity is set to 0.",

            CapacitySource.CustomizedDay =>
                $"{serviceName} has already reached the day capacity every {dayText} at {timeText}.",

            _ when IsFootspaOrPedicureService(title) =>
                "This time is not available because it is already booked by a Footspa or Pedicure appointment.",

            _ when rule.Capacity <= 0 =>
                $"{serviceName} is not available on {dateText} at {timeText}.",

            _ =>
                "This time slot is already full for selected bookings."
        };
    }

    public static bool TryGetCapacityForSlot(
        BaseSvcStructure svc,
        string title,
        string timeSlot,
        ScheduleCfg scheduleCfg,
        out int capacity)
    {
        capacity = 0;

        var map = GetCapacityMap(title, scheduleCfg);

        if (map.TryGetValue(timeSlot, out capacity))
            return true;

        var normalizedSelected = ServiceSectionTimeAlgorithms.NormalizeTimeRangeLabel(timeSlot);

        foreach (var kvp in map)
        {
            if (string.Equals(
                    ServiceSectionTimeAlgorithms.NormalizeTimeRangeLabel(kvp.Key),
                    normalizedSelected,
                    StringComparison.OrdinalIgnoreCase))
            {
                capacity = kvp.Value;
                return true;
            }
        }

        return false;
    }

    public static bool TryGetCapacityForSlot(
        BaseSvcStructure svc,
        string title,
        DateTime date,
        string timeSlot,
        ScheduleCfg scheduleCfg,
        out int capacity)
    {
        var slotDateTime = ServiceSectionTimeAlgorithms.CombineDateAndTime(date, timeSlot);
        var rule = ResolveCapacityRule(svc, title, slotDateTime, scheduleCfg);

        capacity = rule.Capacity;

        return !string.IsNullOrWhiteSpace(rule.SlotKey);
    }

    public static Dictionary<string, int> GetCapacityMap(string title, ScheduleCfg scheduleCfg)
    {
        if (IsNailService(title))
            return scheduleCfg.NailsAccommodationCapacities ?? [];

        if (IsFootspaOrPedicureService(title))
            return (scheduleCfg.OtherServicesAccommodationCapacities ?? [])
                .ToDictionary(x => x.Key, _ => 1);

        return scheduleCfg.OtherServicesAccommodationCapacities ?? [];
    }

    public static bool MatchesCapacityCategory(BaseSvcStructure svc, string title, ApptSchedRec record)
    {
        if (IsNailService(title))
            return record.Services.Any(IsNailServiceRecord);

        if (IsFootspaOrPedicureService(title))
            return record.Services.Any(IsFootspaOrPedicureServiceRecord);

        return record.Services.Any(IsRegularOtherServiceRecord);
    }

    public static bool IsNailService(string title)
        => title.Equals("Nails", StringComparison.OrdinalIgnoreCase);

    public static bool IsFootspaService(string title)
        => title.Equals("Footspa", StringComparison.OrdinalIgnoreCase);

    public static bool IsPedicureService(string title)
        => title.Equals("Pedicure", StringComparison.OrdinalIgnoreCase);

    public static bool IsFootspaOrPedicureService(string title)
        => IsFootspaService(title) || IsPedicureService(title);

    public static bool IsNailServiceRecord(ApptSchedService service)
        => service.ServiceName.Equals("Nails", StringComparison.OrdinalIgnoreCase);

    public static bool IsFootspaServiceRecord(ApptSchedService service)
        => service.ServiceName.Equals("Footspa", StringComparison.OrdinalIgnoreCase);

    public static bool IsPedicureServiceRecord(ApptSchedService service)
        => service.ServiceName.Equals("Pedicure", StringComparison.OrdinalIgnoreCase);

    public static bool IsFootspaOrPedicureServiceRecord(ApptSchedService service)
        => IsFootspaServiceRecord(service) || IsPedicureServiceRecord(service);

    public static bool IsRegularOtherServiceRecord(ApptSchedService service)
        => !IsNailServiceRecord(service) && !IsFootspaOrPedicureServiceRecord(service);

    private static CapacityRule ResolveCapacityRule(
        BaseSvcStructure svc,
        string title,
        DateTime serviceDate,
        ScheduleCfg scheduleCfg)
    {
        var serviceUid = svc.Uid;
        var isNails = IsNailService(title);
        var isFootspaOrPedicure = IsFootspaOrPedicureService(title);

        foreach (var custom in scheduleCfg.CustomizedServiceAccomodationCapacity ?? [])
        {
            if (!IsSameUid(custom.Uid, serviceUid))
                continue;

            var capacities = custom.ThisServiceAccomodationCapacity ?? [];

            if (TryResolveServiceDateSlotKey(serviceDate, capacities, out var slotKey))
            {
                return new CapacityRule
                {
                    Source = CapacitySource.CustomizedServiceDate,
                    ServiceUid = serviceUid,
                    IsNails = isNails,
                    IsFootspaOrPedicure = isFootspaOrPedicure,
                    SlotKey = slotKey,
                    Capacity = capacities.TryGetValue(slotKey, out var serviceDateCap) ? serviceDateCap : 0,
                    Capacities = capacities
                };
            }
        }

        foreach (var custom in scheduleCfg.CustomizedDayAccomodationCapacity ?? [])
        {
            if (!IsSameUid(custom.Uid, serviceUid))
                continue;

            if (!IsSameDay(serviceDate, custom.Day))
                continue;

            var capacities = custom.NailsAccommodationCapacities ?? [];

            if (TryResolveHourSlotKey(serviceDate, capacities, out var slotKey))
            {
                return new CapacityRule
                {
                    Source = CapacitySource.CustomizedDay,
                    ServiceUid = serviceUid,
                    IsNails = isNails,
                    IsFootspaOrPedicure = isFootspaOrPedicure,
                    SlotKey = slotKey,
                    Capacity = capacities.TryGetValue(slotKey, out var dayCap) ? dayCap : 0,
                    Capacities = capacities
                };
            }
        }

        var defaultCapacities = GetCapacityMap(title, scheduleCfg);

        if (!TryResolveHourSlotKey(serviceDate, defaultCapacities, out var defaultSlotKey))
        {
            return new CapacityRule
            {
                Source = CapacitySource.Default,
                ServiceUid = serviceUid,
                IsNails = isNails,
                IsFootspaOrPedicure = isFootspaOrPedicure,
                SlotKey = "",
                Capacity = 0,
                Capacities = defaultCapacities
            };
        }

        return new CapacityRule
        {
            Source = CapacitySource.Default,
            ServiceUid = serviceUid,
            IsNails = isNails,
            IsFootspaOrPedicure = isFootspaOrPedicure,
            SlotKey = defaultSlotKey,
            Capacity = defaultCapacities.TryGetValue(defaultSlotKey, out var defaultCap) ? defaultCap : 0,
            Capacities = defaultCapacities
        };
    }

    private static int CountExistingBookings(
        BaseSvcStructure svc,
        string title,
        DateTime serviceDate,
        List<ApptSchedRec> appointmentSchedules,
        CapacityRule rule)
    {
        return appointmentSchedules
            .Where(x => IsSameDateTimeSlot(x.ServiceDate, serviceDate))
            .SelectMany(x => x.Services ?? [])
            .Count(x => IsSameScheduledCapacitySlot(x, svc, title, rule));
    }

    private static int CountCurrentBookings(
        DateTime serviceDate,
        List<ClientService> currentBookings,
        string currentCardKey,
        CapacityRule rule)
    {
        return currentBookings.Count(x =>
            !IsSameUid(x.ServiceUid, currentCardKey) &&
            IsSameDateTimeSlot(x.ServiceDate, serviceDate) &&
            IsSameCurrentCapacitySlot(x, rule));
    }

    private static bool IsSameScheduledCapacitySlot(
        ApptSchedService service,
        BaseSvcStructure targetService,
        string title,
        CapacityRule rule)
    {
        return rule.Source switch
        {
            CapacitySource.CustomizedServiceDate => IsSameScheduledService(service, targetService, title),
            CapacitySource.CustomizedDay => IsSameScheduledService(service, targetService, title),
            _ => IsScheduledServiceSameDefaultType(service, rule)
        };
    }

    private static bool IsSameCurrentCapacitySlot(
        ClientService service,
        CapacityRule rule)
    {
        return rule.Source switch
        {
            CapacitySource.CustomizedServiceDate => IsSameUid(service.ServiceUid, rule.ServiceUid),
            CapacitySource.CustomizedDay => IsSameUid(service.ServiceUid, rule.ServiceUid),
            _ => IsCurrentServiceSameDefaultType(service, rule)
        };
    }

    private static bool IsSameScheduledService(
        ApptSchedService service,
        BaseSvcStructure targetService,
        string title)
    {
        var sameTitle = string.Equals(
            service.ServiceName?.Trim(),
            title?.Trim(),
            StringComparison.OrdinalIgnoreCase);

        var sameDetails = string.Equals(
            service.ServiceDetails?.Trim(),
            targetService.Details?.Trim(),
            StringComparison.OrdinalIgnoreCase);

        return sameTitle && sameDetails;
    }

    private static bool IsScheduledServiceSameDefaultType(
        ApptSchedService service,
        CapacityRule rule)
    {
        if (rule.IsNails)
            return IsNailServiceRecord(service);

        if (rule.IsFootspaOrPedicure)
            return IsFootspaOrPedicureServiceRecord(service);

        return IsRegularOtherServiceRecord(service);
    }

    private static bool IsCurrentServiceSameDefaultType(
        ClientService service,
        CapacityRule rule)
    {
        var category = ExtractCategory(service.ServiceUid);

        var isNails = IsNailService(service.ServiceName) ||
                      IsNailService(category);

        var isFootspaOrPedicure = IsFootspaOrPedicureService(service.ServiceName) ||
                                  IsFootspaOrPedicureService(category);

        if (rule.IsNails)
            return isNails;

        if (rule.IsFootspaOrPedicure)
            return isFootspaOrPedicure;

        return !isNails && !isFootspaOrPedicure;
    }

    private static bool TryResolveServiceDateSlotKey(
        DateTime serviceDate,
        Dictionary<string, int> capacities,
        out string slotKey)
    {
        foreach (var entry in capacities)
        {
            if (IsMatchingServiceDateSlot(serviceDate, entry.Key))
            {
                slotKey = entry.Key;
                return true;
            }
        }

        slotKey = "";
        return false;
    }

    private static bool TryResolveHourSlotKey(
        DateTime serviceDate,
        Dictionary<string, int> capacities,
        out string slotKey)
    {
        foreach (var entry in capacities)
        {
            if (IsMatchingHourSlot(serviceDate, entry.Key))
            {
                slotKey = entry.Key;
                return true;
            }
        }

        slotKey = "";
        return false;
    }

    private static bool IsMatchingServiceDateSlot(DateTime serviceDate, string slotKey)
    {
        var normalizedKey = NormalizeSlotKey(slotKey);

        if (DateTime.TryParseExact(
                normalizedKey,
                "yyyy-MM-ddTHH:mm:ss",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var exactSlotTime))
        {
            return IsSameDateTimeSlot(exactSlotTime, serviceDate);
        }

        if (DateTime.TryParseExact(
                normalizedKey,
                "yyyy-MM-ddTHH:mm",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var exactSlotTimeNoSeconds))
        {
            return IsSameDateTimeSlot(exactSlotTimeNoSeconds, serviceDate);
        }

        if (normalizedKey.Contains('-') &&
            DateTime.TryParse(
                normalizedKey,
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var parsedSlotTime))
        {
            return IsSameDateTimeSlot(parsedSlotTime, serviceDate);
        }

        return false;
    }

    private static bool IsMatchingHourSlot(DateTime serviceDate, string slotKey)
    {
        var normalizedKey = NormalizeSlotKey(slotKey);

        if (!TryParseTimeOnly(normalizedKey, out var slotTime))
            return false;

        return serviceDate.Hour == slotTime.Hour &&
               serviceDate.Minute == slotTime.Minute;
    }

    private static bool TryParseTimeOnly(string value, out DateTime result)
    {
        var formats = new[]
        {
            "h tt",
            "hh tt",
            "h:mm tt",
            "hh:mm tt",
            "H:mm",
            "HH:mm",
            "H:mm:ss",
            "HH:mm:ss"
        };

        if (DateTime.TryParseExact(
                value,
                formats,
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out result))
        {
            return true;
        }

        return DateTime.TryParse(
            value,
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out result);
    }

    private static bool IsSameDateTimeSlot(DateTime first, DateTime second)
    {
        return first.Date == second.Date &&
               first.Hour == second.Hour &&
               first.Minute == second.Minute;
    }

    private static bool IsSameDay(DateTime serviceDate, string day)
    {
        if (string.IsNullOrWhiteSpace(day))
            return false;

        var value = day.Trim();

        return string.Equals(value, serviceDate.DayOfWeek.ToString(), StringComparison.OrdinalIgnoreCase) ||
               string.Equals(value, serviceDate.ToString("dddd", CultureInfo.InvariantCulture), StringComparison.OrdinalIgnoreCase) ||
               string.Equals(value, serviceDate.ToString("ddd", CultureInfo.InvariantCulture), StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsSameUid(string? first, string? second)
    {
        var firstUid = ExtractUid(first);
        var secondUid = ExtractUid(second);

        return string.Equals(
            firstUid,
            secondUid,
            StringComparison.OrdinalIgnoreCase);
    }

    private static string ExtractUid(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "";

        var cleaned = value.Trim();

        if (!cleaned.Contains('|'))
            return cleaned;

        var parts = cleaned.Split(
            '|',
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (parts.Length >= 2)
            return parts[1];

        return cleaned;
    }

    private static string ExtractCategory(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "";

        var cleaned = value.Trim();

        if (!cleaned.Contains('|'))
            return cleaned;

        var parts = cleaned.Split(
            '|',
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (parts.Length >= 1)
            return parts[0];

        return cleaned;
    }

    private static string NormalizeSlotKey(string slotKey)
    {
        return slotKey
            .Trim()
            .TrimStart('[')
            .TrimEnd(']')
            .Trim();
    }
}
