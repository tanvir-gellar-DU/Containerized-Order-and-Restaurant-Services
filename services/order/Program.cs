using System.Text.Json;
using Order;

var builder = WebApplication.CreateBuilder(args);

// ---------- Configuration ----------
// PORT defaults to 8080 so Order and Restaurant (8081) can run side by side.
string port = builder.Configuration["PORT"] ?? "8080";
builder.WebHost.UseUrls("http://0.0.0.0:" + port);

// ---------- Logging: one JSON line per log, including correlation_id ----------
builder.Logging.ClearProviders();
builder.Logging.AddJsonConsole(options =>
{
    options.IncludeScopes = true;
    options.TimestampFormat = "yyyy-MM-ddTHH:mm:ss.fffZ ";
    options.UseUtcTimestamp = true;
});
builder.Logging.AddFilter("Microsoft.AspNetCore", LogLevel.Warning);

// ---------- JSON uses snake_case (restaurant_id, order_id, ...) ----------
JsonSerializerOptions jsonOptions = new JsonSerializerOptions();
jsonOptions.PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower;
jsonOptions.PropertyNameCaseInsensitive = true;

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower;
});

// ---------- Services shared by all requests (one instance each) ----------
builder.Services.AddSingleton<OrderStore>();
builder.Services.AddSingleton<RestaurantClient>();

var app = builder.Build();

// Create the RestaurantClient now so a missing RESTAURANT_URL fails at startup, not on the first order.
app.Services.GetRequiredService<RestaurantClient>();

app.UseMiddleware<CorrelationIdMiddleware>();

// ---------- GET /healthz ----------
app.MapGet("/healthz", () =>
{
    return Results.Ok(new { status = "ok", service = "order" });
});

// ---------- POST /orders ----------
app.MapPost("/orders", async (HttpContext context, OrderStore store, RestaurantClient restaurantClient, ILogger<Program> logger) =>
{
    string correlationId = CorrelationIdMiddleware.GetCorrelationId(context);

    // 1. Read the JSON body ourselves so bad JSON gives our own 400 message.
    CreateOrderRequest? request;
    try
    {
        request = await JsonSerializer.DeserializeAsync<CreateOrderRequest>(context.Request.Body, jsonOptions);
    }
    catch (JsonException)
    {
        return Error(400, "malformed_json", "request body is not valid JSON", correlationId);
    }

    if (request == null)
    {
        return Error(400, "malformed_json", "request body is empty", correlationId);
    }

    // 2. Check required fields and quantities.
    string? validationError = OrderValidator.Validate(request);
    if (validationError != null)
    {
        logger.LogWarning("Order rejected: {Reason}", validationError);
        return Error(400, "validation_failed", validationError, correlationId);
    }

    // 3. Ask Restaurant Service (over HTTP) whether the restaurant exists.
    LookupResult lookup = await restaurantClient.GetRestaurant(request.RestaurantId!, correlationId);

    if (lookup.Status == LookupStatus.Unavailable)
    {
        return Error(503, "dependency_unavailable", "restaurant service is unavailable, try again later", correlationId);
    }

    if (lookup.Status == LookupStatus.NotFound || lookup.Restaurant == null)
    {
        logger.LogWarning("Order rejected: restaurant {RestaurantId} not found", request.RestaurantId);
        return Error(404, "restaurant_not_found", "restaurant '" + request.RestaurantId + "' not found", correlationId);
    }

    // 4. Every menu_item_id must be on that restaurant's menu.
    foreach (OrderItemRequest item in request.Items!)
    {
        bool found = false;
        foreach (MenuItemInfo menuItem in lookup.Restaurant.Menu)
        {
            if (menuItem.Id == item.MenuItemId)
            {
                found = true;
                break;
            }
        }

        if (!found)
        {
            logger.LogWarning("Order rejected: menu item {MenuItemId} not in restaurant {RestaurantId}",
                item.MenuItemId, request.RestaurantId);
            return Error(404, "menu_item_not_found",
                "menu item '" + item.MenuItemId + "' not found in restaurant '" + request.RestaurantId + "'", correlationId);
        }
    }

    // 5. Everything is valid: create and save the order.
    OrderRecord order = new OrderRecord();
    order.OrderId = Guid.NewGuid().ToString();
    order.RestaurantId = request.RestaurantId!;
    order.Status = "CREATED";
    order.CreatedAt = DateTime.UtcNow;
    order.CorrelationId = correlationId;
    foreach (OrderItemRequest item in request.Items!)
    {
        OrderItem orderItem = new OrderItem();
        orderItem.MenuItemId = item.MenuItemId!;
        orderItem.Quantity = item.Quantity!.Value;
        order.Items.Add(orderItem);
    }

    store.Add(order);
    logger.LogInformation("Order {OrderId} created for restaurant {RestaurantId}", order.OrderId, order.RestaurantId);

    CreateOrderResponse response = new CreateOrderResponse();
    response.OrderId = order.OrderId;
    response.Status = order.Status;
    response.CreatedAt = order.CreatedAt;

    return Results.Created("/orders/" + order.OrderId, response);
});

// ---------- GET /orders/{id} ----------
app.MapGet("/orders/{id}", (string id, HttpContext context, OrderStore store) =>
{
    OrderRecord? order = store.Get(id);
    if (order == null)
    {
        return Error(404, "order_not_found", "order '" + id + "' not found", CorrelationIdMiddleware.GetCorrelationId(context));
    }
    return Results.Ok(order);
});

app.Logger.LogInformation("Order service listening on port {Port}", port);
app.Run();


// Helper that builds the same JSON error body for every error.
static IResult Error(int statusCode, string error, string message, string correlationId)
{
    ErrorResponse body = new ErrorResponse();
    body.Error = error;
    body.Message = message;
    body.CorrelationId = correlationId;
    return Results.Json(body, statusCode: statusCode);
}

// Lets the test project (Part 4) start this app in memory.
public partial class Program { }
