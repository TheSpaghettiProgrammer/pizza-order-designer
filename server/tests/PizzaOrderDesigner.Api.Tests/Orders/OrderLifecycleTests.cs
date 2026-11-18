using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace PizzaOrderDesigner.Api.Tests.Orders;

/// <summary>
/// #35: the lifecycle guarantees, end to end over HTTP against real Postgres.
/// Retries never duplicate or resurrect an order; what you see is what you pay; only NotStarted orders change;
/// a cancel racing the kitchen's start has exactly one winner.
/// </summary>
[Collection("postgres")]
public class OrderLifecycleTests(PostgresApiFactory factory, Xunit.Abstractions.ITestOutputHelper output)
{
    private readonly HttpClient client = factory.CreateClient();

    // 1 × Large with pepperoni: 12.96, tax 0.84 → 13.80
    private static object Body(decimal expectedTotal = 13.80m, int quantity = 1) => new
    {
        lines = new[] { new { sizeCode = "large", toppingCodes = new[] { "pepperoni" }, quantity } },
        promoCode = (string?)null,
        tip = (object?)null,
        expectedTotal,
    };

    private async Task<HttpResponseMessage> Submit(object body, string? key = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/orders") { Content = JsonContent.Create(body) };
        request.Headers.Add("Idempotency-Key", key ?? Guid.NewGuid().ToString());
        return await client.SendAsync(request);
    }

    private static async Task<JsonElement> Json(HttpResponseMessage response) =>
        JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.Clone();

    private static async Task<string> Code(HttpResponseMessage response) => (await Json(response)).GetProperty("code").GetString()!;

    private async Task<string> NewOrderId() => (await Json(await Submit(Body()))).GetProperty("id").GetString()!;

    private Task<HttpResponseMessage> Advance(string id) => client.PostAsync($"/api/kitchen/orders/{id}/advance", null);

    // ---------------------------------------------------------------- submit and retries (#16)

    [Fact]
    public async Task Submit_creates_the_order_with_its_priced_snapshot()
    {
        var response = await Submit(Body());
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(response.Headers.Location);
        Assert.NotNull(response.Headers.ETag);
        var order = await Json(response);
        Assert.Equal("NotStarted", order.GetProperty("status").GetString());
        Assert.Equal(13.80m, order.GetProperty("pricing").GetProperty("total").GetDecimal());
        Assert.Equal("1 × Large pizza — Pepperoni", order.GetProperty("pricing").GetProperty("description").GetString());
    }

    [Fact]
    public async Task Retrying_with_the_same_key_returns_the_same_order_and_creates_no_second_one()
    {
        var key = Guid.NewGuid().ToString();
        var first = await Submit(Body(), key);
        var retry = await Submit(Body(), key);

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, retry.StatusCode);
        Assert.Equal((await Json(first)).GetProperty("id").GetString(), (await Json(retry)).GetProperty("id").GetString());
    }

    [Fact]
    public async Task Concurrent_duplicate_submits_produce_exactly_one_order()
    {
        var key = Guid.NewGuid().ToString();
        var responses = await Task.WhenAll(Enumerable.Range(0, 10).Select(_ => Submit(Body(), key)));

        Assert.All(responses, r => Assert.True(r.IsSuccessStatusCode, $"got {(int)r.StatusCode}"));
        Assert.Equal(1, responses.Count(r => r.StatusCode == HttpStatusCode.Created));
        var ids = await Task.WhenAll(responses.Select(async r => (await Json(r)).GetProperty("id").GetString()));
        Assert.Single(ids.Distinct());
    }

    [Fact]
    public async Task Reusing_a_key_for_a_different_order_is_rejected()
    {
        var key = Guid.NewGuid().ToString();
        await Submit(Body(), key);
        var different = await Submit(Body(expectedTotal: 27.60m, quantity: 2), key);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, different.StatusCode);
        Assert.Equal("idempotency.key_reused", await Code(different));
    }

    [Fact]
    public async Task A_late_retry_never_recreates_a_cancelled_order()
    {
        var key = Guid.NewGuid().ToString();
        var id = (await Json(await Submit(Body(), key))).GetProperty("id").GetString();
        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/api/orders/{id}")).StatusCode);

        var lateRetry = await Submit(Body(), key);
        Assert.Equal(HttpStatusCode.Conflict, lateRetry.StatusCode);
        Assert.Equal("order.cancelled", await Code(lateRetry));
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/orders/{id}")).StatusCode);
    }

    [Fact]
    public async Task Submit_rejects_a_total_the_customer_did_not_see_and_stores_nothing()
    {
        var key = Guid.NewGuid().ToString();
        var response = await Submit(Body(expectedTotal: 13.79m), key);
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var problem = await Json(response);
        Assert.Equal("price.changed", problem.GetProperty("code").GetString());
        Assert.Equal(13.80m, problem.GetProperty("quote").GetProperty("total").GetDecimal());

        // Nothing was stored for that key, so it can still be used once the customer accepts the real total.
        Assert.Equal(HttpStatusCode.Created, (await Submit(Body(expectedTotal: 13.80m), key)).StatusCode);
    }

    [Fact]
    public async Task Over_long_key_and_non_guid_id_get_problem_details()
    {
        var longKey = await Submit(Body(), new string('k', 101));
        Assert.Equal(HttpStatusCode.BadRequest, longKey.StatusCode);
        Assert.Equal("idempotency.key_invalid", await Code(longKey));

        var badId = await client.GetAsync("/api/orders/not-a-guid");
        Assert.Equal(HttpStatusCode.NotFound, badId.StatusCode);
        Assert.Equal("application/problem+json", badId.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Missing_key_and_invalid_orders_are_400s_with_codes()
    {
        var noKey = await client.PostAsJsonAsync("/api/orders", Body());
        Assert.Equal(HttpStatusCode.BadRequest, noKey.StatusCode);
        Assert.Equal("idempotency.key_missing", await Code(noKey));

        var invalid = await Submit(new { lines = Array.Empty<object>(), expectedTotal = 0m });
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        Assert.Equal("order.invalid", await Code(invalid));
    }

    // ---------------------------------------------------------------- get, update, cancel (#17, #18, #19)

    [Fact]
    public async Task Update_needs_the_current_ETag_and_a_not_started_order()
    {
        var id = await NewOrderId();
        var get = await client.GetAsync($"/api/orders/{id}");
        var etag = get.Headers.ETag!.Tag;

        var update = new HttpRequestMessage(HttpMethod.Put, $"/api/orders/{id}") { Content = JsonContent.Create(Body(27.60m, 2)) };
        update.Headers.TryAddWithoutValidation("If-Match", etag);
        var updated = await client.SendAsync(update);
        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
        Assert.Equal(27.60m, (await Json(updated)).GetProperty("pricing").GetProperty("total").GetDecimal());

        // The old ETag is now stale.
        var stale = new HttpRequestMessage(HttpMethod.Put, $"/api/orders/{id}") { Content = JsonContent.Create(Body()) };
        stale.Headers.TryAddWithoutValidation("If-Match", etag);
        var staleResponse = await client.SendAsync(stale);
        Assert.Equal(HttpStatusCode.PreconditionFailed, staleResponse.StatusCode);

        // After the kitchen starts, a fresh ETag doesn't help: the order can't change.
        await Advance(id);
        var fresh = (await client.GetAsync($"/api/orders/{id}")).Headers.ETag!.Tag;
        var late = new HttpRequestMessage(HttpMethod.Put, $"/api/orders/{id}") { Content = JsonContent.Create(Body()) };
        late.Headers.TryAddWithoutValidation("If-Match", fresh);
        var lateResponse = await client.SendAsync(late);
        Assert.Equal(HttpStatusCode.Conflict, lateResponse.StatusCode);
        Assert.Equal("order.already_started", await Code(lateResponse));
    }

    [Fact]
    public async Task Update_without_If_Match_is_428()
    {
        var id = await NewOrderId();
        var response = await client.PutAsJsonAsync($"/api/orders/{id}", Body());
        Assert.Equal((HttpStatusCode)428, response.StatusCode);
    }

    [Fact]
    public async Task Cancel_deletes_only_while_not_started()
    {
        var id = await NewOrderId();
        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/api/orders/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.DeleteAsync($"/api/orders/{id}")).StatusCode); // retry → 404

        var started = await NewOrderId();
        await Advance(started);
        var refused = await client.DeleteAsync($"/api/orders/{started}");
        Assert.Equal(HttpStatusCode.Conflict, refused.StatusCode);
        Assert.Equal("order.already_started", await Code(refused));
    }

    // ---------------------------------------------------------------- state machine and the race (#20, #21)

    [Fact]
    public async Task Kitchen_advances_linearly_and_stops_at_Delivered()
    {
        var id = await NewOrderId();
        foreach (var expected in new[] { "Started", "ReadyForPickup", "Delivered" })
        {
            var response = await Advance(id);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal(expected, (await Json(response)).GetProperty("status").GetString());
        }

        var again = await Advance(id);
        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
        Assert.Equal("order.already_delivered", await Code(again));
        Assert.Equal(HttpStatusCode.NotFound, (await Advance(Guid.NewGuid().ToString())).StatusCode);
    }

    [Fact]
    public async Task Cancel_racing_the_kitchen_start_has_exactly_one_winner()
    {
        // Real parallel HTTP requests. Which side wins varies by timing; RaceGuaranteeTests forces each ordering.
        var cancelWins = 0;
        var startWins = 0;
        for (var round = 0; round < 25; round++)
        {
            var id = await NewOrderId();
            var cancelTask = client.DeleteAsync($"/api/orders/{id}");
            var startTask = Advance(id);
            var cancel = await cancelTask;
            var start = await startTask;

            var cancelled = cancel.StatusCode == HttpStatusCode.NoContent;
            var started = start.StatusCode == HttpStatusCode.OK;
            Assert.True(cancelled ^ started, $"round {round}: cancel {(int)cancel.StatusCode}, start {(int)start.StatusCode}");

            var final = await client.GetAsync($"/api/orders/{id}");
            if (cancelled)
            {
                cancelWins++;
                Assert.Equal(HttpStatusCode.NotFound, start.StatusCode);
                Assert.Equal(HttpStatusCode.NotFound, final.StatusCode);
            }
            else
            {
                startWins++;
                Assert.Equal(HttpStatusCode.Conflict, cancel.StatusCode);
                Assert.Equal("Started", (await Json(final)).GetProperty("status").GetString());
            }
        }

        output.WriteLine($"cancel won {cancelWins}, kitchen start won {startWins} of 25");
    }
}

[CollectionDefinition("postgres")]
public sealed class PostgresCollection : ICollectionFixture<PostgresApiFactory>;
