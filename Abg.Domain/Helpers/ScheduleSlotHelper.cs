using Abg.Domain.__Base__;
using Abg.Domain.Client;
using Abg.Domain.Schedules;
using Abg.Domain.Service;
using System.Globalization;
using static Abg.Domain.Constants;

namespace Abg.Domain.Helpers;

public static class ScheduleSlotHelper
{
    public static string ToServiceDateKey(this DateTime value)
        => value.ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture);

    public static bool TryParseServiceDateKey(this string value, out DateTime result)
    {
        return DateTime.TryParseExact(
            value,
            "yyyy-MM-ddTHH:mm:ss",
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out result);
    }

    public static string ResolveSlotKey(this DateTime serviceDate, Dictionary<string, int> capacities)
    {
        foreach (var entry in capacities)
        {
            if (IsMatchingHourSlot(serviceDate, entry.Key))
                return entry.Key;
        }

        throw new InvalidOperationException(
            $"No matching capacity slot found for {serviceDate:yyyy-MM-dd hh:mm tt}.");
    }

    public static List<ClientRequest> GetValidRequests(
        this List<ClientRequest> existingRequests,
        string bookingId,
        DateTime now,
        TimeSpan holdDuration,
        out List<string> expiredIds)
    {
        var valid = new List<ClientRequest>();
        expiredIds = [];

        foreach (var existing in existingRequests)
        {
            var existingId = existing.ClientInformation.ClientBookingId;

            if (existingId == bookingId)
                continue;

            if (IsExpiredPending(existing, now, holdDuration))
            {
                expiredIds.Add(existingId);
                continue;
            }

            valid.Add(existing);
        }

        return valid;
    }

    public static void ValidateSlots(
        this List<ClientService> incomingServices,
        List<ClientRequest> validRequests,
        ScheduleCfg cfg,
        ServiceCollectionResp services)
    {
        foreach (var svc in incomingServices)
        {
            var serviceDefinition = ResolveServiceDefinition(svc, services);
            var rule = ResolveCapacityRule(svc, serviceDefinition, cfg);

            var existingCount = validRequests
                .SelectMany(x => x.ClientServices ?? [])
                .Count(x => IsSameCapacitySlot(x, svc, rule, services));

            var incomingCount = incomingServices
                .Count(x => IsSameCapacitySlot(x, svc, rule, services));

            if (existingCount + incomingCount > rule.Capacity)
                throw new InvalidOperationException(
                    BuildCapacityErrorMessage(svc, rule));
        }
    }

    private static string BuildCapacityErrorMessage(ClientService svc, CapacityRule rule)
    {
        var serviceName = string.IsNullOrWhiteSpace(svc.ServiceName)
            ? "This service"
            : svc.ServiceName;

        var dateText = svc.ServiceDate.ToString("MMMM dd, yyyy", CultureInfo.InvariantCulture);
        var dayText  = svc.ServiceDate.ToString("dddd", CultureInfo.InvariantCulture);
        var timeText = svc.ServiceDate.ToString("h:mm tt", CultureInfo.InvariantCulture);

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

            _ when rule.Capacity <= 0 =>
                $"{serviceName} is not available on {dateText} at {timeText}.",

            _ =>
                "The date is currently on queue, please try again booking this date after 5 mins."
        };
    }

    private static CapacityRule ResolveCapacityRule(
                   ClientService    svc,
                   BaseSvcStructure serviceDefinition,
                   ScheduleCfg      cfg)
    {
        var serviceUid = serviceDefinition.Uid;
        var isNails    = serviceDefinition is NailsService;

        foreach (var custom in cfg.CustomizedServiceAccomodationCapacity ?? [])
        {
            if (!IsSameUid(custom.Uid, serviceUid))
                continue;

            var capacities = custom.ThisServiceAccomodationCapacity ?? [];

            if (TryResolveServiceDateSlotKey(svc.ServiceDate, capacities, out var slotKey))
            {
                return new CapacityRule
                {
                    Source     = CapacitySource.CustomizedServiceDate,
                    ServiceUid = serviceUid,
                    IsNails    = isNails,
                    SlotKey    = slotKey,
                    Capacity   = capacities.TryGetValue(slotKey, out var serviceDateCap) ? serviceDateCap : 0,
                    Capacities = capacities
                };
            }
        }

        foreach (var custom in cfg.CustomizedDayAccomodationCapacity ?? [])
        {
            if (!IsSameUid(custom.Uid, serviceUid))
                continue;

            if (!IsSameDay(svc.ServiceDate, custom.Day))
                continue;

            var capacities = custom.NailsAccommodationCapacities ?? [];

            if (TryResolveHourSlotKey(svc.ServiceDate, capacities, out var slotKey))
            {
                return new CapacityRule
                {
                    Source     = CapacitySource.CustomizedDay,
                    ServiceUid = serviceUid,
                    IsNails    = isNails,
                    SlotKey    = slotKey,
                    Capacity   = capacities.TryGetValue(slotKey, out var dayCap) ? dayCap : 0,
                    Capacities = capacities
                };
            }
        }

        var defaultCapacities = isNails
            ? cfg.NailsAccommodationCapacities ?? []
            : cfg.OtherServicesAccommodationCapacities ?? [];

        var defaultSlotKey = svc.ServiceDate.ResolveSlotKey(defaultCapacities);

        return new CapacityRule
        {
            Source     = CapacitySource.Default,
            ServiceUid = serviceUid,
            IsNails    = isNails,
            SlotKey    = defaultSlotKey,
            Capacity   = defaultCapacities.TryGetValue(defaultSlotKey, out var defaultCap) ? defaultCap : 0,
            Capacities = defaultCapacities
        };
    }

    private static bool IsSameCapacitySlot(
        ClientService         service,
        ClientService         target,
        CapacityRule          rule,
        ServiceCollectionResp services)
    {
        BaseSvcStructure serviceDefinition;

        try
        {
            serviceDefinition = ResolveServiceDefinition(service, services);
        }
        catch
        {
            return false;
        }

        return rule.Source switch
        {
            CapacitySource.CustomizedServiceDate => IsSameCustomizedServiceDateSlot(
                service,
                target,
                serviceDefinition,
                rule),

            CapacitySource.CustomizedDay => IsSameCustomizedDaySlot(
                service,
                target,
                serviceDefinition,
                rule),

            _ => IsSameDefaultSlot(
                service,
                target,
                serviceDefinition,
                rule)
        };
    }

    private static bool IsSameCustomizedServiceDateSlot(
        ClientService service,
        ClientService target,
        BaseSvcStructure serviceDefinition,
        CapacityRule rule)
    {
        if (!IsSameUid(serviceDefinition.Uid, rule.ServiceUid))
            return false;

        if (service.ServiceDate.Date != target.ServiceDate.Date)
            return false;

        return TryResolveServiceDateSlotKey(service.ServiceDate, rule.Capacities, out var serviceSlotKey) &&
               serviceSlotKey == rule.SlotKey;
    }

    private static bool IsSameCustomizedDaySlot(
        ClientService service,
        ClientService target,
        BaseSvcStructure serviceDefinition,
        CapacityRule rule)
    {
        if (!IsSameUid(serviceDefinition.Uid, rule.ServiceUid))
            return false;

        if (service.ServiceDate.Date != target.ServiceDate.Date)
            return false;

        return TryResolveHourSlotKey(service.ServiceDate, rule.Capacities, out var serviceSlotKey) &&
               serviceSlotKey == rule.SlotKey;
    }

    private static bool IsSameDefaultSlot(
        ClientService service,
        ClientService target,
        BaseSvcStructure serviceDefinition,
        CapacityRule rule)
    {
        var sameDate = service.ServiceDate.Date == target.ServiceDate.Date;
        var sameType = serviceDefinition is NailsService == rule.IsNails;

        if (!sameDate || !sameType)
            return false;

        try
        {
            return service.ServiceDate.ResolveSlotKey(rule.Capacities) == rule.SlotKey;
        }
        catch
        {
            return false;
        }
    }

    private static BaseSvcStructure ResolveServiceDefinition(
        ClientService clientService,
        ServiceCollectionResp services)
    {
        var allServices = GetAllServices(services);

        var service = allServices.FirstOrDefault(x =>
            IsSameUid(x.Uid, clientService.ServiceUid));

        if (service is not null)
            return service;

        throw new InvalidOperationException(
            $"Service configuration was not found for {clientService.ServiceName}.");
    }

    private static List<BaseSvcStructure> GetAllServices(ServiceCollectionResp services)
    {
        return
        [
            ..services.Nails.Cast<BaseSvcStructure>(),
            ..services.Lashes.Cast<BaseSvcStructure>(),
            ..services.Eyebrows.Cast<BaseSvcStructure>(),
            ..services.Footspa.Cast<BaseSvcStructure>(),
            ..services.Pedicure.Cast<BaseSvcStructure>()
        ];
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

    private static bool IsExpiredPending(
        ClientRequest request,
        DateTime now,
        TimeSpan holdDuration)
    {
        return request.Status == ClientStatus.Pending &&
               now - request.ClientInformation.BookingDate >= holdDuration;
    }

    private static bool IsSameUid(string? first, string? second)
    {
        var firstUid  = ExtractUid(first);
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

        var parts = cleaned
            .Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (parts.Length >= 2)
            return parts[1];

        return cleaned;
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

    private static bool IsMatchingServiceDateSlot(DateTime serviceDate, string slotKey)
    {
        var normalizedKey = NormalizeSlotKey(slotKey);

        if (normalizedKey.TryParseServiceDateKey(out var exactSlotTime))
        {
            return serviceDate.Date == exactSlotTime.Date &&
                   serviceDate.Hour == exactSlotTime.Hour;
        }

        if (DateTime.TryParse(normalizedKey, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsedSlotTime))
        {
            return serviceDate.Date == parsedSlotTime.Date &&
                   serviceDate.Hour == parsedSlotTime.Hour;
        }

        return false;
    }

    private static bool IsMatchingHourSlot(DateTime serviceDate, string slotKey)
    {
        var normalizedKey = NormalizeSlotKey(slotKey);

        if (!DateTime.TryParse(normalizedKey, CultureInfo.InvariantCulture, DateTimeStyles.None, out var slotTime))
            return false;

        return serviceDate.Hour == slotTime.Hour;
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
