namespace Abg.Domain.Utilities;

public static class UidGenerator
{
    public static string GenerateUid(this string category)
    {
        var prefix = category switch
        {
            "Nails"    => "NAS",
            "Lash"     => "LAS",
            "Eyebrows" => "EYS",
            "Footspa"  => "FOS",
            "Pedicure" => "PES",
            _          => "SVS"
        };

        return $"{prefix}-{Random.Shared.Next(100, 999)}";
    }

    public static string NormalizeCategory(this string category)
    {
        return category?.ToLower() switch
        {
            "nails"    => "Nails",
            "lash"     => "Lash",
            "eyebrows" => "Eyebrows",
            "footspa"  => "Footspa",
            "pedicure" => "Pedicure",
            _          => "Nails"
        };
    }
}
