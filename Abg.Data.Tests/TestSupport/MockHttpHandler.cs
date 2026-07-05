using System.Net;
using System.Text;

namespace Abg.Data.Tests.TestSupport;

public sealed class MockHttpHandler : HttpMessageHandler
{
    readonly Queue<(HttpStatusCode Status, string Body)> _responses = new();

    public List<(HttpMethod Method, string Url, string Body)> Requests { get; } = [];

    public MockHttpHandler Enqueue(HttpStatusCode status, string body)
    {
        _responses.Enqueue((status, body));
        return this;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        var body = request.Content is null ? "" : await request.Content.ReadAsStringAsync(ct);

        Requests.Add((request.Method, request.RequestUri!.ToString(), body));

        if (_responses.Count == 0)
            throw new InvalidOperationException("No scripted response left for " + request.RequestUri);

        var (status, responseBody) = _responses.Dequeue();

        return new HttpResponseMessage(status)
        {
            Content = new StringContent(responseBody, Encoding.UTF8, "application/json")
        };
    }
}
