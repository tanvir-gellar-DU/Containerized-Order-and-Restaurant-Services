using System.Diagnostics;
using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Order.Tests;

/// <summary>
/// Starts the Order service in memory and points RESTAURANT_URL at a fake
/// Restaurant server (or at a port where nothing is running).
/// </summary>
public class OrderApiTests : IClassFixture<FakeRestaurantServer>
{
    private readonly FakeRestaurantServer _restaurant;

    public OrderApiTests(FakeRestaurantServer restaurant)
    {
        _restaurant = restaurant;
    }

    // ---------- helpers ----------

    private static HttpClient CreateOrderClient(string restaurantUrl, int timeoutMs)
    {
        WebApplicationFactory<Program> factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("RESTAURANT_URL", restaurantUrl);
            builder.UseSetting("RESTAURANT_TIMEOUT_MS", timeoutMs.ToString());
        });
        return factory.CreateClient();
    }

    private HttpClient CreateOrderClient()
    {
        return CreateOrderClient(_restaurant.Url, 2000);
    }

    private static StringContent Json(string json)
    {
        return new StringContent(json, Encoding.UTF8, "application/json");
    }

    private const string ValidOrder =
        "{\"restaurant_id\":\"r1\",\"items\":[{\"menu_item_id\":\"m101\",\"quantity\":2}]}";

    // ---------- 1. success ----------

    [Fact]
    public async Task CreateOrder_ValidRequest_Returns201WithOrderIdStatusAndCreatedAt()
    {
        HttpClient client = CreateOrderClient();

        HttpResponseMessage response = await client.PostAsync("/orders", Json(ValidOrder));
        string body = await response.Content.ReadAsStringAsync();
        JsonDocument json = JsonDocument.Parse(body);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.False(string.IsNullOrEmpty(json.RootElement.GetProperty("order_id").GetString()));
        Assert.Equal("CREATED", json.RootElement.GetProperty("status").GetString());
        Assert.False(string.IsNullOrEmpty(json.RootElement.GetProperty("created_at").GetString()));
    }

    [Fact]
    public async Task GetOrder_AfterCreating_ReturnsTheSameOrder()
    {
        HttpClient client = CreateOrderClient();

        HttpResponseMessage created = await client.PostAsync("/orders", Json(ValidOrder));
        string orderId = JsonDocument.Parse(await created.Content.ReadAsStringAsync())
            .RootElement.GetProperty("order_id").GetString()!;

        HttpResponseMessage response = await client.GetAsync("/orders/" + orderId);
        JsonDocument json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(orderId, json.RootElement.GetProperty("order_id").GetString());
        Assert.Equal("r1", json.RootElement.GetProperty("restaurant_id").GetString());
    }

    // ---------- 2. validation (400) ----------

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(101)]
    public async Task CreateOrder_BadQuantity_Returns400(int quantity)
    {
        HttpClient client = CreateOrderClient();
        string body = "{\"restaurant_id\":\"r1\",\"items\":[{\"menu_item_id\":\"m101\",\"quantity\":" + quantity + "}]}";

        HttpResponseMessage response = await client.PostAsync("/orders", Json(body));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData("{\"items\":[{\"menu_item_id\":\"m101\",\"quantity\":1}]}")]        // no restaurant_id
    [InlineData("{\"restaurant_id\":\"r1\"}")]                                      // no items
    [InlineData("{\"restaurant_id\":\"r1\",\"items\":[]}")]                         // empty items
    [InlineData("{\"restaurant_id\":\"r1\",\"items\":[{\"quantity\":1}]}")]         // no menu_item_id
    [InlineData("{\"restaurant_id\":\"r1\",\"items\":[{\"menu_item_id\":\"m101\"}]}")] // no quantity
    public async Task CreateOrder_MissingField_Returns400(string body)
    {
        HttpClient client = CreateOrderClient();

        HttpResponseMessage response = await client.PostAsync("/orders", Json(body));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateOrder_MalformedJson_Returns400()
    {
        HttpClient client = CreateOrderClient();

        HttpResponseMessage response = await client.PostAsync("/orders", Json("{\"restaurant_id\": "));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ---------- 3. nonexistent entities (404) ----------

    [Fact]
    public async Task CreateOrder_UnknownRestaurant_Returns404()
    {
        HttpClient client = CreateOrderClient();
        string body = "{\"restaurant_id\":\"r999\",\"items\":[{\"menu_item_id\":\"m101\",\"quantity\":1}]}";

        HttpResponseMessage response = await client.PostAsync("/orders", Json(body));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task CreateOrder_UnknownMenuItem_Returns404()
    {
        HttpClient client = CreateOrderClient();
        string body = "{\"restaurant_id\":\"r1\",\"items\":[{\"menu_item_id\":\"m999\",\"quantity\":1}]}";

        HttpResponseMessage response = await client.PostAsync("/orders", Json(body));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetOrder_UnknownId_Returns404()
    {
        HttpClient client = CreateOrderClient();

        HttpResponseMessage response = await client.GetAsync("/orders/does-not-exist");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ---------- 4. concurrency ----------

    [Fact]
    public async Task CreateOrder_20ConcurrentRequests_Gives20UniqueOrders()
    {
        HttpClient client = CreateOrderClient();

        // Start 20 POSTs at the same time, then wait for all of them.
        List<Task<HttpResponseMessage>> requests = new List<Task<HttpResponseMessage>>();
        for (int i = 0; i < 20; i++)
        {
            requests.Add(client.PostAsync("/orders", Json(ValidOrder)));
        }
        HttpResponseMessage[] responses = await Task.WhenAll(requests);

        HashSet<string> orderIds = new HashSet<string>();
        foreach (HttpResponseMessage response in responses)
        {
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            string orderId = JsonDocument.Parse(await response.Content.ReadAsStringAsync())
                .RootElement.GetProperty("order_id").GetString()!;
            orderIds.Add(orderId);
        }
        Assert.Equal(20, orderIds.Count);

        // Every created order can be read back.
        foreach (string orderId in orderIds)
        {
            HttpResponseMessage get = await client.GetAsync("/orders/" + orderId);
            Assert.Equal(HttpStatusCode.OK, get.StatusCode);
        }
    }

    [Fact]
    public void OrderStore_ParallelAdds_LosesNothing()
    {
        OrderStore store = new OrderStore();

        Parallel.For(0, 1000, i =>
        {
            OrderRecord order = new OrderRecord();
            order.OrderId = "order-" + i;
            store.Add(order);
        });

        Assert.Equal(1000, store.Count());
    }

    // ---------- 5. dependency unavailable (503) ----------

    [Fact]
    public async Task CreateOrder_RestaurantDown_Returns503Quickly()
    {
        // Port 1 on localhost: nothing is listening, so the connection is refused.
        HttpClient client = CreateOrderClient("http://127.0.0.1:1", 2000);
        Stopwatch timer = Stopwatch.StartNew();

        HttpResponseMessage response = await client.PostAsync("/orders", Json(ValidOrder));
        timer.Stop();

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.True(timer.ElapsedMilliseconds < 3000, "took " + timer.ElapsedMilliseconds + " ms");
    }

    [Fact]
    public async Task CreateOrder_RestaurantTooSlow_Returns503AfterTimeout()
    {
        // Fake restaurant waits 5 s for "slow"; Order gives up after 500 ms.
        HttpClient client = CreateOrderClient(_restaurant.Url, 500);
        string body = "{\"restaurant_id\":\"slow\",\"items\":[{\"menu_item_id\":\"m101\",\"quantity\":1}]}";
        Stopwatch timer = Stopwatch.StartNew();

        HttpResponseMessage response = await client.PostAsync("/orders", Json(body));
        timer.Stop();

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.True(timer.ElapsedMilliseconds < 3000, "took " + timer.ElapsedMilliseconds + " ms");
    }

    // ---------- 6. correlation id ----------

    [Fact]
    public async Task CreateOrder_ForwardsCorrelationIdToRestaurant()
    {
        HttpClient client = CreateOrderClient();
        string correlationId = "test-" + Guid.NewGuid();

        HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, "/orders");
        request.Headers.Add("X-Correlation-ID", correlationId);
        request.Content = Json(ValidOrder);
        HttpResponseMessage response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(correlationId, response.Headers.GetValues("X-Correlation-ID").First());
        Assert.True(_restaurant.ReceivedCorrelationId(correlationId));
    }
}
