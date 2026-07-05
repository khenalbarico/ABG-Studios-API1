using Abg.Domain.__Base__;

namespace Abg.Domain.Algorithms;

public static class ServiceSectionKeyAlgorithms
{
    public static string BuildCardKey(string title, BaseSvcStructure svc)
        => $"{title}|{svc.Uid}|{svc.Details}|{svc.Cost}";
}
