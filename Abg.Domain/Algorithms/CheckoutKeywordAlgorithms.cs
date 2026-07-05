namespace Abg.Domain.Algorithms;

public static class CheckoutKeywordAlgorithms
{
    public static bool ContainsKeyword(string? value, string keyword)
    {
        return !string.IsNullOrWhiteSpace(value) &&
               value.Contains(keyword, StringComparison.OrdinalIgnoreCase);
    }
}
