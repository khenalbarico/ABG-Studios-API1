using Abg.Domain.Client;
using static Abg.Domain.Constants;

namespace Abg.Domain.Algorithms;

public static class CheckoutPolicyAlgorithms
{
    public static bool RequiresNailsRules(ClientRequest request)
    {
        return request.ClientServices.Any(x =>
            CheckoutKeywordAlgorithms.ContainsKeyword(x.ServiceName, "nail") ||
            CheckoutKeywordAlgorithms.ContainsKeyword(x.ServiceDetails, "nail"));
    }

    public static bool RequiresConsentForm(ClientRequest request)
    {
        return request.ClientServices.Any(x =>
            CheckoutKeywordAlgorithms.ContainsKeyword(x.ServiceName, "lash") ||
            CheckoutKeywordAlgorithms.ContainsKeyword(x.ServiceName, "brow") ||
            CheckoutKeywordAlgorithms.ContainsKeyword(x.ServiceDetails, "lash") ||
            CheckoutKeywordAlgorithms.ContainsKeyword(x.ServiceDetails, "brow"));
    }

    public static CheckoutFlowStep ResolveNextStep(
           ClientRequest request,
           bool          nailsRulesAccepted,
           bool          consentAccepted)
    {
        var needsNailsRules  = RequiresNailsRules(request);
        var needsConsentForm = RequiresConsentForm(request);

        if (needsNailsRules && !nailsRulesAccepted)
            return CheckoutFlowStep.NailsRules;

        if (needsConsentForm && !consentAccepted)
            return CheckoutFlowStep.ConsentForm;

        return CheckoutFlowStep.Payment;
    }
}
