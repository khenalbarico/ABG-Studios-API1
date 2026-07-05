namespace Abg.Data.Firebase;

public sealed class FirebaseOptions
{
    public string DatabaseUrl { get; set; } = "";

    /// <summary>Optional database secret / auth token appended as ?auth= on REST calls.</summary>
    public string AuthToken   { get; set; } = "";
}
