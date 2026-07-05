namespace Abg.Domain;

public sealed class Constants
{
    public enum ClientStatus
    {
        Pending,
        Paid,
    }

    public enum ClientServiceStatus
    {
        Pending,
        Serving,
        Completed
    }

    public enum ServiceBranch
    {
        Anabu,
        Manila
    }

    public enum ServiceDesigns
    {
        Simple,
        Complex,
        Intricate
    }

    public enum TimeSlotStatus
    {
        Available,
        BookedByYou,
        Full
    }

    public enum CheckoutFlowStep
    {
        Payment,
        NailsRules,
        ConsentForm
    }

    public enum CapacitySource
    {
        CustomizedServiceDate,
        CustomizedDay,
        Default
    }

    public static readonly Dictionary<ServiceBranch, string> BranchNames = new()
    {
        { ServiceBranch.Anabu,  "Anabu Doyets Imus Cavite" },
        { ServiceBranch.Manila, "The Manila Residence Tower II TAFT Manila" }
    };
}
