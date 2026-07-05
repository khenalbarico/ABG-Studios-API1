using Abg.Domain.Client;
using static Abg.Domain.Constants;

namespace Abg.Domain.Algorithms;

public static class CheckoutSummaryAlgorithms
{
    public static decimal GetTotalAmount(ClientRequest request)
        => request.ClientServices.Sum(x => x.ServiceCost);

    public static string GetBranchDisplayName(ServiceBranch branch)
    {
        if (BranchNames.TryGetValue(branch, out var branchName))
            return branchName;

        return branch.ToString();
    }
}
