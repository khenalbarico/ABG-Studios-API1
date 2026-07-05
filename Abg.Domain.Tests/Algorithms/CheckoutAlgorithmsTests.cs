using Abg.Domain.Algorithms;
using Abg.Domain.Client;
using static Abg.Domain.Constants;

namespace Abg.Domain.Tests.Algorithms;

public class CheckoutKeywordAlgorithmsTests
{
    [Theory]
    [InlineData("Gel Nails", "nail", true)]
    [InlineData("NAILS deluxe", "nail", true)]
    [InlineData("Lash lift", "nail", false)]
    [InlineData("", "nail", false)]
    [InlineData("   ", "nail", false)]
    [InlineData(null, "nail", false)]
    public void ContainsKeyword_matches_case_insensitively(string? value, string keyword, bool expected)
        => Assert.Equal(expected, CheckoutKeywordAlgorithms.ContainsKeyword(value, keyword));
}

public class CheckoutPaymentAlgorithmsTests
{
    [Theory]
    [InlineData("succeeded", true)]
    [InlineData("SUCCEEDED", true)]
    [InlineData("awaiting_payment_method", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsPaymentSuccessful_only_for_succeeded(string? status, bool expected)
        => Assert.Equal(expected, CheckoutPaymentAlgorithms.IsPaymentSuccessful(status));
}

public class CheckoutPolicyAlgorithmsTests
{
    static ClientRequest RequestWith(params (string name, string details)[] services) => new()
    {
        ClientServices = [.. services.Select(s => new ClientService
        {
            ServiceName    = s.name,
            ServiceDetails = s.details
        })]
    };

    [Fact]
    public void RequiresNailsRules_when_any_service_mentions_nail()
    {
        Assert.True(CheckoutPolicyAlgorithms.RequiresNailsRules(RequestWith(("Nails", "Gel polish"))));
        Assert.True(CheckoutPolicyAlgorithms.RequiresNailsRules(RequestWith(("Spa", "nail art add-on"))));
        Assert.False(CheckoutPolicyAlgorithms.RequiresNailsRules(RequestWith(("Footspa", "Classic"))));
    }

    [Fact]
    public void RequiresConsentForm_when_any_service_mentions_lash_or_brow()
    {
        Assert.True(CheckoutPolicyAlgorithms.RequiresConsentForm(RequestWith(("Lashes", "Volume set"))));
        Assert.True(CheckoutPolicyAlgorithms.RequiresConsentForm(RequestWith(("Eyebrows", "Brow lamination"))));
        Assert.False(CheckoutPolicyAlgorithms.RequiresConsentForm(RequestWith(("Nails", "Gel polish"))));
    }

    [Fact]
    public void ResolveNextStep_orders_nails_rules_then_consent_then_payment()
    {
        var both = RequestWith(("Nails", "Gel"), ("Lashes", "Classic"));

        Assert.Equal(CheckoutFlowStep.NailsRules,  CheckoutPolicyAlgorithms.ResolveNextStep(both, nailsRulesAccepted: false, consentAccepted: false));
        Assert.Equal(CheckoutFlowStep.ConsentForm, CheckoutPolicyAlgorithms.ResolveNextStep(both, nailsRulesAccepted: true,  consentAccepted: false));
        Assert.Equal(CheckoutFlowStep.Payment,     CheckoutPolicyAlgorithms.ResolveNextStep(both, nailsRulesAccepted: true,  consentAccepted: true));
    }

    [Fact]
    public void ResolveNextStep_goes_straight_to_payment_when_no_policies_apply()
    {
        var footspa = RequestWith(("Footspa", "Classic"));

        Assert.Equal(CheckoutFlowStep.Payment, CheckoutPolicyAlgorithms.ResolveNextStep(footspa, false, false));
    }
}

public class CheckoutRequestAlgorithmsTests
{
    [Fact]
    public void PrepareClientInformation_sets_booking_date_and_id_format()
    {
        var request = new ClientRequest();
        var before  = DateTime.Now;

        CheckoutRequestAlgorithms.PrepareClientInformation(request);

        var after = DateTime.Now;

        Assert.InRange(request.ClientInformation.BookingDate, before, after);
        Assert.Matches(@"^\d{6}-\d{8}$", request.ClientInformation.ClientBookingId);
        Assert.StartsWith(request.ClientInformation.BookingDate.ToString("MMddyy"), request.ClientInformation.ClientBookingId);
    }
}

public class CheckoutSummaryAlgorithmsTests
{
    [Fact]
    public void GetTotalAmount_sums_service_costs()
    {
        var request = new ClientRequest
        {
            ClientServices =
            [
                new ClientService { ServiceCost = 500.50m },
                new ClientService { ServiceCost = 249.50m }
            ]
        };

        Assert.Equal(750.00m, CheckoutSummaryAlgorithms.GetTotalAmount(request));
    }

    [Fact]
    public void GetBranchDisplayName_uses_shared_branch_names()
    {
        Assert.Equal(BranchNames[ServiceBranch.Anabu],  CheckoutSummaryAlgorithms.GetBranchDisplayName(ServiceBranch.Anabu));
        Assert.Equal(BranchNames[ServiceBranch.Manila], CheckoutSummaryAlgorithms.GetBranchDisplayName(ServiceBranch.Manila));
        Assert.Equal("99", CheckoutSummaryAlgorithms.GetBranchDisplayName((ServiceBranch)99));
    }
}
