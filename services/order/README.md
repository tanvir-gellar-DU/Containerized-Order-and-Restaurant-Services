# Order Service

Creates and returns orders (ASP.NET Core 8 minimal API). Before saving an order it
checks the restaurant and menu items by calling **Restaurant Service over HTTP**.

| Endpoint | Description |
|---|---|
| `GET /healthz` | Liveness check |
| `POST /orders` | Validate and create an order → `201` `{order_id, status, created_at}` |
| `GET /orders/{id}` | Full order, `404` if unknown |

## Configuration (environment variables)

| Variable | Default | Meaning |
|---|---|---|
| `RESTAURANT_URL` | **required** | Base URL of Restaurant Service, e.g. `http://localhost:8081` locally or `http://restaurant:8081` in Docker |
| `RESTAURANT_TIMEOUT_MS` | `2000` | Max time to wait for Restaurant Service |
| `PORT` | `8080` | Port this service listens on |

The service refuses to start if `RESTAURANT_URL` is missing.

## Files

| File | What it does |
|---|---|
| `Program.cs` | Startup + the three endpoints |
| `OrderValidator.cs` | Checks required fields and quantity (1–100) |
| `RestaurantClient.cs` | HTTP call to Restaurant Service with timeout; maps result to Found / NotFound / Unavailable |
| `OrderStore.cs` | In-memory `Dictionary` protected by `lock` (safe for concurrent requests) |
| `CorrelationIdMiddleware.cs` | Reads/creates `X-Correlation-ID`, adds it to logs and response |
| `Models.cs` | Request, response and stored-order classes |

## Error handling

| Case | Status |
|---|---|
| Invalid JSON / wrong types | 400 `malformed_json` |
| Missing `restaurant_id`, empty `items`, missing `menu_item_id`/`quantity`, quantity ≤ 0 or > 100 | 400 `validation_failed` |
| Restaurant doesn't exist | 404 `restaurant_not_found` |
| Menu item not on that restaurant's menu | 404 `menu_item_not_found` |
| Unknown order id | 404 `order_not_found` |
| Restaurant Service down, timed out or 5xx | 503 `dependency_unavailable` |

## Run locally

```bash
# terminal 1
cd services/restaurant && dotnet run

# terminal 2
cd services/order && RESTAURANT_URL=http://localhost:8081 dotnet run

# terminal 3
curl -X POST localhost:8080/orders -H 'Content-Type: application/json' \
  -d '{"restaurant_id":"r1","items":[{"menu_item_id":"m101","quantity":2}]}'
curl localhost:8080/orders/<order_id>
```

Full contract and examples: [`docs/api.md`](../../docs/api.md).
