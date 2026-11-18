# Restaurant Service

Read-only catalogue of restaurants and menu items (ASP.NET Core 8 minimal API).

| Endpoint | Description |
|---|---|
| `GET /healthz` | Liveness check |
| `GET /restaurants` | List restaurants (summary) |
| `GET /restaurants/{id}` | One restaurant with its menu, `404` if unknown |

Config: `PORT` (default `8081`).

Data is seeded in memory from `SeedData.cs` (3 restaurants, 9 items) and never mutated,
so concurrent reads need no locking. Full contract: [`docs/api.md`](../../docs/api.md).

Logs are single-line JSON and include `correlation_id` (taken from the `X-Correlation-ID`
request header or generated).

## Run locally (.NET 8 SDK)

```bash
cd services/restaurant
dotnet run                       # or: PORT=9000 dotnet run
curl localhost:8081/restaurants
curl localhost:8081/restaurants/r1
curl -H 'X-Correlation-ID: demo-1' localhost:8081/restaurants/r9   # 404, ID echoed back
```
