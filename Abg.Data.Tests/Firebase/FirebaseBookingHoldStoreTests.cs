using Abg.Data.Firebase;
using Abg.Data.Tests.TestSupport;
using Abg.Domain.Client;
using System.Net;
using System.Text.Json;
using static Abg.Domain.Constants;

namespace Abg.Data.Tests.Firebase;

public class FirebaseBookingHoldStoreTests
{
    static (FirebaseBookingHoldStore Store, MockHttpHandler Handler) StoreWith(params (HttpStatusCode, string)[] responses)
    {
        var handler = new MockHttpHandler();

        foreach (var (status, body) in responses)
            handler.Enqueue(status, body);

        var store = new FirebaseBookingHoldStore(
            new HttpClient(handler),
            new FirebaseOptions { DatabaseUrl = "https://abg-dev.firebaseio.test/", AuthToken = "db-secret" });

        return (store, handler);
    }

    [Fact]
    public async Task GetHolds_returns_empty_list_when_node_is_null()
    {
        var (store, handler) = StoreWith((HttpStatusCode.OK, "null"));

        var holds = await store.GetHoldsAsync();

        Assert.Empty(holds);
        Assert.Equal("https://abg-dev.firebaseio.test/ClientRequests.json?auth=db-secret", handler.Requests[0].Url);
    }

    [Fact]
    public async Task GetHolds_maps_keyed_children_to_requests()
    {
        var (store, _) = StoreWith((HttpStatusCode.OK, """
            {"070526-11111111":{"ClientInformation":{"ClientBookingId":"070526-11111111","Email":"a@b.c"},"Status":0},
             "070526-22222222":{"ClientInformation":{"ClientBookingId":"070526-22222222","Email":"d@e.f"},"Status":1}}
            """));

        var holds = await store.GetHoldsAsync();

        Assert.Equal(2, holds.Count);
        Assert.Contains(holds, x => x.ClientInformation.ClientBookingId == "070526-11111111" && x.Status == ClientStatus.Pending);
        Assert.Contains(holds, x => x.ClientInformation.ClientBookingId == "070526-22222222" && x.Status == ClientStatus.Paid);
    }

    [Fact]
    public async Task GetHold_returns_null_for_missing_booking()
    {
        var (store, handler) = StoreWith((HttpStatusCode.OK, "null"));

        var hold = await store.GetHoldAsync("070526-99999999");

        Assert.Null(hold);
        Assert.Equal("https://abg-dev.firebaseio.test/ClientRequests/070526-99999999.json?auth=db-secret", handler.Requests[0].Url);
    }

    [Fact]
    public async Task PutHold_puts_request_under_booking_id_with_pascal_case_and_enum_numbers()
    {
        var (store, handler) = StoreWith((HttpStatusCode.OK, "{}"));

        await store.PutHoldAsync(new ClientRequest
        {
            ClientInformation = new ClientInformation { ClientBookingId = "070526-11111111" },
            Status            = ClientStatus.Pending
        });

        var request = handler.Requests[0];

        Assert.Equal(HttpMethod.Put, request.Method);
        Assert.Equal("https://abg-dev.firebaseio.test/ClientRequests/070526-11111111.json?auth=db-secret", request.Url);

        using var body = JsonDocument.Parse(request.Body);

        Assert.Equal("070526-11111111", body.RootElement.GetProperty("ClientInformation").GetProperty("ClientBookingId").GetString());
        Assert.Equal(0, body.RootElement.GetProperty("Status").GetInt32());
    }

    [Fact]
    public async Task SetHoldStatus_patches_only_the_status_field()
    {
        var (store, handler) = StoreWith((HttpStatusCode.OK, "{}"));

        await store.SetHoldStatusAsync("070526-11111111", ClientStatus.Paid);

        var request = handler.Requests[0];

        Assert.Equal(HttpMethod.Patch, request.Method);
        Assert.Equal("""{"Status":1}""", request.Body);
    }

    [Fact]
    public async Task DeleteHold_issues_delete_on_booking_node()
    {
        var (store, handler) = StoreWith((HttpStatusCode.OK, "null"));

        await store.DeleteHoldAsync("070526-11111111");

        Assert.Equal(HttpMethod.Delete, handler.Requests[0].Method);
        Assert.Equal("https://abg-dev.firebaseio.test/ClientRequests/070526-11111111.json?auth=db-secret", handler.Requests[0].Url);
    }

    [Fact]
    public async Task Failed_responses_throw()
    {
        var (store, _) = StoreWith((HttpStatusCode.Unauthorized, "{}"));

        await Assert.ThrowsAsync<HttpRequestException>(() => store.GetHoldsAsync());
    }
}
