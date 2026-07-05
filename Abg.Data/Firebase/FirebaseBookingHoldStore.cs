using Abg.Domain.Client;
using System.Text;
using System.Text.Json;
using static Abg.Domain.Constants;

namespace Abg.Data.Firebase;

public sealed class FirebaseBookingHoldStore(HttpClient _httpClient, FirebaseOptions _options) : IBookingHoldStore
{
    const string HoldsNode = "ClientRequests";

    static readonly JsonSerializerOptions JsonOptions = new()
    {
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    public async Task<List<ClientRequest>> GetHoldsAsync(CancellationToken ct = default)
    {
        var holds = await GetAsync<Dictionary<string, ClientRequest>>(NodeUrl(), ct);

        return holds is null ? [] : [.. holds.Values];
    }

    public async Task<ClientRequest?> GetHoldAsync(string bookingId, CancellationToken ct = default)
        => await GetAsync<ClientRequest>(NodeUrl(bookingId), ct);

    public async Task PutHoldAsync(ClientRequest request, CancellationToken ct = default)
    {
        var bookingId = request.ClientInformation.ClientBookingId;
        var json      = JsonSerializer.Serialize(request, JsonOptions);

        using var content  = new StringContent(json, Encoding.UTF8, "application/json");
        using var response = await _httpClient.PutAsync(NodeUrl(bookingId), content, ct);

        response.EnsureSuccessStatusCode();
    }

    public async Task SetHoldStatusAsync(string bookingId, ClientStatus status, CancellationToken ct = default)
    {
        var json = JsonSerializer.Serialize(new Dictionary<string, int> { ["Status"] = (int)status });

        using var content  = new StringContent(json, Encoding.UTF8, "application/json");
        using var request  = new HttpRequestMessage(HttpMethod.Patch, NodeUrl(bookingId)) { Content = content };
        using var response = await _httpClient.SendAsync(request, ct);

        response.EnsureSuccessStatusCode();
    }

    public async Task DeleteHoldAsync(string bookingId, CancellationToken ct = default)
    {
        using var response = await _httpClient.DeleteAsync(NodeUrl(bookingId), ct);

        response.EnsureSuccessStatusCode();
    }

    private async Task<T?> GetAsync<T>(string url, CancellationToken ct) where T : class
    {
        using var response = await _httpClient.GetAsync(url, ct);

        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadAsStringAsync(ct);

        if (string.IsNullOrWhiteSpace(body) || body == "null")
            return null;

        return JsonSerializer.Deserialize<T>(body, JsonOptions);
    }

    private string NodeUrl(string? child = null)
    {
        var baseUrl = _options.DatabaseUrl.TrimEnd('/');
        var path    = child is null ? HoldsNode : $"{HoldsNode}/{Uri.EscapeDataString(child)}";
        var auth    = string.IsNullOrWhiteSpace(_options.AuthToken) ? "" : $"?auth={Uri.EscapeDataString(_options.AuthToken)}";

        return $"{baseUrl}/{path}.json{auth}";
    }
}
