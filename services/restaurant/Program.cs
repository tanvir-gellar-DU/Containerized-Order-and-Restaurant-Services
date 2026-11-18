using System.Text.Json;
using Restaurant;

var builder = WebApplication.CreateBuilder(args);

// Port is configurable so the service can run independently of Order (default 8081).
var port = builder.Configuration["PORT"] ?? "8081";
builder.WebHost.UseUrls($"http://0.0.0.0:{port}");

// Single-line console logs that include the correlation_id scope.
builder.Logging.ClearProviders();
builder.Logging.AddJsonConsole(o =>
{
    o.IncludeScopes = true;
    o.TimestampFormat = "yyyy-MM-ddTHH:mm:ss.fffZ ";
    o.UseUtcTimestamp = true;
});
// Framework chatter off; our middleware writes one access-log line per request.
builder.Logging.AddFilter("Microsoft.AspNetCore", LogLevel.Warning);

// snake_case JSON to match the API contract (docs/api.md).
builder.Services.ConfigureHttpJsonOptions(o =>
{
    o.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower;
});

var app = builder.Build();

app.UseMiddleware<CorrelationIdMiddleware>();

app.MapGet("/healthz", () => Results.Ok(new { status = "ok", service = "restaurant" }));

app.MapGet("/restaurants", () =>
    Results.Ok(SeedData.Restaurants.Values
        .Select(r => new RestaurantSummary(r.Id, r.Name, r.Cuisine))
        .OrderBy(r => r.Id, StringComparer.Ordinal)));

app.MapGet("/restaurants/{id}", (string id, HttpContext ctx, ILogger<Program> logger) =>
{
    if (SeedData.Restaurants.TryGetValue(id, out var restaurant))
    {
        return Results.Ok(restaurant);
    }

    logger.LogWarning("Restaurant {RestaurantId} not found", id);
    return Results.NotFound(new ErrorResponse("not_found", $"restaurant '{id}' not found", CorrelationIdMiddleware.GetCorrelationId(ctx)));
});

app.Logger.LogInformation("Restaurant service listening on port {Port} with {Count} restaurants",
    port, SeedData.Restaurants.Count);

app.Run();

// Exposed for WebApplicationFactory in integration tests (Part 4).
public partial class Program { }
