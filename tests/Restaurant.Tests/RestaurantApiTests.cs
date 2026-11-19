using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Restaurant.Tests;

/// <summary>
/// Starts the Restaurant service in memory and calls its endpoints.
/// </summary>
public class RestaurantApiTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public RestaurantApiTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Healthz_Returns200()
    {
        HttpResponseMessage response = await _client.GetAsync("/healthz");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task ListRestaurants_ReturnsAtLeastThree()
    {
        HttpResponseMessage response = await _client.GetAsync("/restaurants");
        string body = await response.Content.ReadAsStringAsync();
        JsonDocument json = JsonDocument.Parse(body);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(json.RootElement.GetArrayLength() >= 3);
    }

    [Fact]
    public async Task GetRestaurant_KnownId_ReturnsMenu()
    {
        HttpResponseMessage response = await _client.GetAsync("/restaurants/r1");
        string body = await response.Content.ReadAsStringAsync();
        JsonDocument json = JsonDocument.Parse(body);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("r1", json.RootElement.GetProperty("id").GetString());
        Assert.True(json.RootElement.GetProperty("menu").GetArrayLength() > 0);
    }

    [Fact]
    public async Task GetRestaurant_UnknownId_Returns404()
    {
        HttpResponseMessage response = await _client.GetAsync("/restaurants/does-not-exist");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public void SeedData_HasAtLeastSixMenuItems()
    {
        int totalItems = 0;
        foreach (RestaurantDetail restaurant in SeedData.Restaurants.Values)
        {
            totalItems = totalItems + restaurant.Menu.Count;
        }

        Assert.True(SeedData.Restaurants.Count >= 3);
        Assert.True(totalItems >= 6);
    }
}
