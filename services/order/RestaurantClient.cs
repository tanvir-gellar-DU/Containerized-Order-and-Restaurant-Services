using System.Net;
using System.Text.Json;

namespace Order;

/// <summary>
/// The three possible outcomes of asking Restaurant Service for a restaurant.
/// </summary>
public enum LookupStatus
{
    Found,
    NotFound,
    Unavailable
}

public class LookupResult
{
    public LookupStatus Status { get; set; }
    public RestaurantInfo? Restaurant { get; set; }
}

/// <summary>
/// Talks to Restaurant Service over real HTTP. Its address comes from the
/// RESTAURANT_URL environment variable (never hardcoded), and every call has a
/// short timeout so an order request can never hang forever.
/// </summary>
public class RestaurantClient
{
    private readonly HttpClient _http;
    private readonly ILogger<RestaurantClient> _logger;
    private readonly JsonSerializerOptions _jsonOptions;

    public RestaurantClient(IConfiguration config, ILogger<RestaurantClient> logger)
    {
        _logger = logger;

        string? baseUrl = config["RESTAURANT_URL"];
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            throw new Exception("RESTAURANT_URL environment variable is not set (e.g. http://localhost:8081)");
        }

        int timeoutMs = 2000;
        string? timeoutSetting = config["RESTAURANT_TIMEOUT_MS"];
        if (!string.IsNullOrWhiteSpace(timeoutSetting))
        {
            timeoutMs = int.Parse(timeoutSetting);
        }

        _http = new HttpClient();
        _http.BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/");
        _http.Timeout = TimeSpan.FromMilliseconds(timeoutMs);

        _jsonOptions = new JsonSerializerOptions();
        _jsonOptions.PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower;
        _jsonOptions.PropertyNameCaseInsensitive = true;

        _logger.LogInformation("Restaurant client using {BaseUrl} with timeout {TimeoutMs} ms", baseUrl, timeoutMs);
    }

    public async Task<LookupResult> GetRestaurant(string restaurantId, string correlationId)
    {
        LookupResult result = new LookupResult();

        // Build the request ourselves so we can attach the correlation ID header.
        HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, "restaurants/" + Uri.EscapeDataString(restaurantId));
        request.Headers.Add("X-Correlation-ID", correlationId);

        try
        {
            HttpResponseMessage response = await _http.SendAsync(request);

            if (response.StatusCode == HttpStatusCode.OK)
            {
                string body = await response.Content.ReadAsStringAsync();
                result.Restaurant = JsonSerializer.Deserialize<RestaurantInfo>(body, _jsonOptions);
                result.Status = LookupStatus.Found;
            }
            else if (response.StatusCode == HttpStatusCode.NotFound)
            {
                result.Status = LookupStatus.NotFound;
            }
            else
            {
                // Any other status (5xx, unexpected 4xx) means we cannot trust the dependency.
                _logger.LogWarning("Restaurant service returned {StatusCode} for {RestaurantId}",
                    (int)response.StatusCode, restaurantId);
                result.Status = LookupStatus.Unavailable;
            }
        }
        catch (TaskCanceledException)
        {
            // HttpClient throws this when the timeout is reached.
            _logger.LogError("Restaurant service timed out after {TimeoutMs} ms", _http.Timeout.TotalMilliseconds);
            result.Status = LookupStatus.Unavailable;
        }
        catch (HttpRequestException ex)
        {
            // Connection refused, DNS failure, container stopped, etc.
            _logger.LogError("Restaurant service unreachable: {Reason}", ex.Message);
            result.Status = LookupStatus.Unavailable;
        }
        catch (JsonException ex)
        {
            _logger.LogError("Restaurant service sent invalid JSON: {Reason}", ex.Message);
            result.Status = LookupStatus.Unavailable;
        }

        return result;
    }
}
