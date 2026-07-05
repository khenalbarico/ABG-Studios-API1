using Abg.Domain.__Base__;
using Abg.Domain.Algorithms;
using Abg.Domain.Client;
using Abg.Domain.Schedules;

namespace Abg.Domain.Tests.Algorithms;

public class ServiceSectionCapacityAlgorithmsTests
{
    static readonly DateTime Date = new(2026, 7, 10);

    static BaseSvcStructure Svc(string uid = "NAS-100") => new() { Uid = uid, Details = "Gel polish", Cost = 500m };

    static ApptSchedRec Appointment(string serviceName, DateTime serviceDate, string details = "Gel polish") => new()
    {
        ServiceDate = serviceDate,
        Services    = [new ApptSchedService { ServiceName = serviceName, ServiceDetails = details }]
    };

    static ScheduleCfg NailsCfg(int capacity) => new()
    {
        NailsAccommodationCapacities = new Dictionary<string, int> { ["10:00 AM"] = capacity }
    };

    [Fact]
    public void IsTimeSlotFull_false_when_under_default_capacity()
    {
        var full = ServiceSectionCapacityAlgorithms.IsTimeSlotFull(
            Svc(), "Nails", Date, "10:00 AM - 11:00 AM",
            [Appointment("Nails", Date.AddHours(10))],
            NailsCfg(2));

        Assert.False(full);
    }

    [Fact]
    public void IsTimeSlotFull_true_when_existing_bookings_reach_capacity()
    {
        var full = ServiceSectionCapacityAlgorithms.IsTimeSlotFull(
            Svc(), "Nails", Date, "10:00 AM - 11:00 AM",
            [Appointment("Nails", Date.AddHours(10)), Appointment("Nails", Date.AddHours(10))],
            NailsCfg(2));

        Assert.True(full);
    }

    [Fact]
    public void IsTimeSlotFull_true_when_no_capacity_slot_matches()
    {
        var full = ServiceSectionCapacityAlgorithms.IsTimeSlotFull(
            Svc(), "Nails", Date, "3:00 PM - 4:00 PM", [], NailsCfg(2));

        Assert.True(full);
    }

    [Fact]
    public void IsTimeSlotFull_counts_current_cart_bookings_but_not_the_current_card()
    {
        var cfg = NailsCfg(2);
        var currentCardKey = "Nails|NAS-100|Gel polish|500";

        var otherNailsInCart = new ClientService
        {
            ServiceUid  = "Nails|NAS-200|French tips|700",
            ServiceName = "Nails",
            ServiceDate = Date.AddHours(10)
        };

        var sameCardInCart = new ClientService
        {
            ServiceUid  = currentCardKey,
            ServiceName = "Nails",
            ServiceDate = Date.AddHours(10)
        };

        var fullWithOther = ServiceSectionCapacityAlgorithms.IsTimeSlotFull(
            Svc(), "Nails", Date, "10:00 AM - 11:00 AM",
            [Appointment("Nails", Date.AddHours(10))],
            [otherNailsInCart],
            cfg,
            currentCardKey);

        var notFullWithSelf = ServiceSectionCapacityAlgorithms.IsTimeSlotFull(
            Svc(), "Nails", Date, "10:00 AM - 11:00 AM",
            [Appointment("Nails", Date.AddHours(10))],
            [sameCardInCart],
            cfg,
            currentCardKey);

        Assert.True(fullWithOther);
        Assert.False(notFullWithSelf);
    }

    [Fact]
    public void Footspa_and_pedicure_share_capacity_of_one()
    {
        var cfg = new ScheduleCfg
        {
            OtherServicesAccommodationCapacities = new Dictionary<string, int> { ["10:00 AM"] = 5 }
        };

        var full = ServiceSectionCapacityAlgorithms.IsTimeSlotFull(
            Svc("FOS-100"), "Footspa", Date, "10:00 AM - 11:00 AM",
            [Appointment("Pedicure", Date.AddHours(10))],
            cfg);

        Assert.True(full);
    }

    [Fact]
    public void Customized_service_date_capacity_overrides_default()
    {
        var cfg = NailsCfg(5);
        cfg.CustomizedServiceAccomodationCapacity =
        [
            new ThisServiceDateAccomodationCapacity
            {
                Uid = "NAS-100",
                ThisServiceAccomodationCapacity = new Dictionary<string, int> { ["2026-07-10T10:00:00"] = 0 }
            }
        ];

        var full = ServiceSectionCapacityAlgorithms.IsTimeSlotFull(
            Svc(), "Nails", Date, "10:00 AM - 11:00 AM", [], cfg);

        Assert.True(full);

        var label = ServiceSectionCapacityAlgorithms.GetFullSlotLabel(
            Svc(), "Nails", Date, "10:00 AM - 11:00 AM", cfg);

        Assert.Equal("10:00 AM - 11:00 AM - Closed by date capacity", label);
    }

    [Fact]
    public void Customized_day_capacity_overrides_default_for_matching_day()
    {
        var cfg = NailsCfg(5);
        cfg.CustomizedDayAccomodationCapacity =
        [
            new ThisDayAccomodationCapacity
            {
                Uid = "NAS-100",
                Day = "Friday",
                NailsAccommodationCapacities = new Dictionary<string, int> { ["10:00 AM"] = 1 }
            }
        ];

        var existingSameService = Appointment("Nails", Date.AddHours(10));

        var full = ServiceSectionCapacityAlgorithms.IsTimeSlotFull(
            Svc(), "Nails", Date, "10:00 AM - 11:00 AM",
            [existingSameService],
            cfg);

        Assert.True(full);
        Assert.Equal(DayOfWeek.Friday, Date.DayOfWeek);
    }

    [Fact]
    public void Customized_rules_match_existing_bookings_by_name_and_details()
    {
        var cfg = NailsCfg(5);
        cfg.CustomizedServiceAccomodationCapacity =
        [
            new ThisServiceDateAccomodationCapacity
            {
                Uid = "NAS-100",
                ThisServiceAccomodationCapacity = new Dictionary<string, int> { ["2026-07-10T10:00:00"] = 1 }
            }
        ];

        var differentDetails = Appointment("Nails", Date.AddHours(10), details: "French tips");

        var notFull = ServiceSectionCapacityAlgorithms.IsTimeSlotFull(
            Svc(), "Nails", Date, "10:00 AM - 11:00 AM",
            [differentDetails],
            cfg);

        var sameDetails = Appointment("Nails", Date.AddHours(10), details: "Gel polish");

        var full = ServiceSectionCapacityAlgorithms.IsTimeSlotFull(
            Svc(), "Nails", Date, "10:00 AM - 11:00 AM",
            [sameDetails],
            cfg);

        Assert.False(notFull);
        Assert.True(full);
    }

    [Fact]
    public void GetCapacityMap_picks_map_by_service_category()
    {
        var cfg = new ScheduleCfg
        {
            NailsAccommodationCapacities         = new Dictionary<string, int> { ["10:00 AM"] = 3 },
            OtherServicesAccommodationCapacities = new Dictionary<string, int> { ["10:00 AM"] = 4 }
        };

        Assert.Equal(3, ServiceSectionCapacityAlgorithms.GetCapacityMap("Nails", cfg)["10:00 AM"]);
        Assert.Equal(4, ServiceSectionCapacityAlgorithms.GetCapacityMap("Lashes", cfg)["10:00 AM"]);
        Assert.Equal(1, ServiceSectionCapacityAlgorithms.GetCapacityMap("Footspa", cfg)["10:00 AM"]);
        Assert.Equal(1, ServiceSectionCapacityAlgorithms.GetCapacityMap("Pedicure", cfg)["10:00 AM"]);
    }

    [Fact]
    public void TryGetCapacityForSlot_matches_normalized_time_range_labels()
    {
        var cfg = new ScheduleCfg
        {
            NailsAccommodationCapacities = new Dictionary<string, int> { ["10 AM - 11 AM"] = 2 }
        };

        var found = ServiceSectionCapacityAlgorithms.TryGetCapacityForSlot(
            Svc(), "Nails", "10:00 AM-11:00 AM", cfg, out var capacity);

        Assert.True(found);
        Assert.Equal(2, capacity);
    }
}
