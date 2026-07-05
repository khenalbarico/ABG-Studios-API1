namespace FunctionApp1.Auth;

public sealed class ClientPrincipal
{
    public string       IdentityProvider { get; set; } = "";
    public string       UserId           { get; set; } = "";
    public string       UserDetails      { get; set; } = "";
    public List<string> UserRoles        { get; set; } = [];

    /// <summary>Stable cross-provider key used to attribute bookings and users.</summary>
    public string UserKey => $"{IdentityProvider}:{UserId}";

    public string Email => UserDetails.Contains('@') ? UserDetails : "";

    public bool IsAuthenticated =>
        !string.IsNullOrWhiteSpace(UserId) &&
        UserRoles.Contains("authenticated", StringComparer.OrdinalIgnoreCase);
}
