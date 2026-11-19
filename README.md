# Containerized Order and Restaurant Services

Two independent ASP.NET Core 8 services that talk over HTTP:

```
client ──HTTP──▶ Order Service :8080 ──HTTP──▶ Restaurant Service :8081
                 (POST/GET orders)            (read-only catalogue)
```

- **Restaurant Service** – `GET /restaurants`, `GET /restaurants/{id}`, `GET /healthz`.
  Seeded with 3 restaurants and 9 menu items.
- **Order Service** – `POST /orders`, `GET /orders/{id}`, `GET /healthz`.
  Validates the restaurant and menu items by calling Restaurant Service over HTTP
  (address from the `RESTAURANT_URL` env variable, 2 s timeout).
  Orders are kept in memory behind a `lock`.

Every request gets an `X-Correlation-ID` (taken from the request or generated). Order forwards
it to Restaurant, and both services write it in their logs, so one request can be followed across services.

## Seed data

| ID | Restaurant | Cuisine | Menu items (ID, name, price in BDT) |
|---|---|---|---|
| `r1` | Abesh Restaurant | Bangladeshi | `m101` Kacchi Biryani 350 · `m102` Chicken Roast 220 · `m103` Borhani 60 |
| `r2` | Khao San Dhanmondi | Thai | `m201` Tom yum fried rice 650 · `m202` nun bun 780 · `m203` Garlic Bread 180 |
| `r3` | Gojo Ramen Mohammadpur | Japanese | `m301` Tonkotsu Ramen 520 · `m302` Gyoza (6 pcs) 240 · `m303` Matcha Ice Cream 150 |

A menu item can only be ordered from its own restaurant (e.g. `m201` with `r1` → 404).

## Repository layout

```
services/restaurant/   Restaurant Service + Dockerfile + README
services/order/        Order Service + Dockerfile + README
tests/Restaurant.Tests xUnit tests for Restaurant Service
tests/Order.Tests      xUnit tests for Order Service (uses a fake Restaurant on a real port)
docs/api.md            API contract, JSON examples, error codes, seed catalogue
docs/outage-experiment.md  Outage experiment steps and logs
```

## Run with Docker (no .NET needed)

```bash
docker network create assignment

docker build -t restaurant-svc services/restaurant
docker build -t order-svc services/order

docker run -d --name restaurant --network assignment -p 8081:8081 restaurant-svc
docker run -d --name order --network assignment -p 8080:8080 \
  -e RESTAURANT_URL=http://restaurant:8081 order-svc
```

Inside Docker, `localhost` in the Order container is the Order container itself, so Order
reaches Restaurant by its container name (`restaurant`) on the shared network.

Stop and clean up:
```bash
docker rm -f order restaurant
docker network rm assignment
```

## API examples

```bash
curl localhost:8081/healthz
curl localhost:8080/healthz

curl localhost:8081/restaurants
curl localhost:8081/restaurants/r1

# create an order -> 201 {"order_id":"...","status":"CREATED","created_at":"..."}
curl -i -X POST localhost:8080/orders -H 'Content-Type: application/json' \
  -d '{"restaurant_id":"r1","items":[{"menu_item_id":"m101","quantity":2}]}'

# read it back
curl localhost:8080/orders/<order_id>

# 400 – bad quantity
curl -X POST localhost:8080/orders -H 'Content-Type: application/json' \
  -d '{"restaurant_id":"r1","items":[{"menu_item_id":"m101","quantity":0}]}'

# 404 – unknown restaurant
curl -X POST localhost:8080/orders -H 'Content-Type: application/json' \
  -d '{"restaurant_id":"r9","items":[{"menu_item_id":"m101","quantity":1}]}'
```

### Error codes

| Status | When |
|---|---|
| 400 | Malformed JSON, missing fields, quantity not between 1 and 100 |
| 404 | Restaurant, menu item or order does not exist |
| 503 | Restaurant Service is down, too slow (timeout) or returns 5xx |

All errors return `{"error": "...", "message": "...", "correlation_id": "..."}`.
Full details: [`docs/api.md`](docs/api.md).

## Configuration

| Service | Variable | Default |
|---|---|---|
| both | `PORT` | `8081` (Restaurant), `8080` (Order) |
| Order | `RESTAURANT_URL` | **required** |
| Order | `RESTAURANT_TIMEOUT_MS` | `2000` |

## Tests

Requires the .NET 8 SDK:

```bash
dotnet test FoodOrdering.sln
```

24 tests in total:

| Area | Tests |
|---|---|
| Success | create order → 201 with id/status/created_at; read created order back |
| Validation (400) | quantity 0, -1, 101; missing restaurant_id / items / menu_item_id / quantity; empty items; malformed JSON |
| Not found (404) | unknown restaurant; unknown menu item; unknown order |
| Concurrency | 20 concurrent POSTs → 20 unique IDs, all readable; 1000 parallel store writes lose nothing |
| Dependency unavailable (503) | Restaurant down → 503 in < 3 s; Restaurant too slow → 503 after timeout |
| Tracing | correlation ID is returned and forwarded to Restaurant |
| Restaurant Service | healthz; list ≥ 3; get with menu; unknown → 404; seed has ≥ 6 items |

## Run locally without Docker

```bash
cd services/restaurant && dotnet run                                  # terminal 1
cd services/order && RESTAURANT_URL=http://localhost:8081 dotnet run  # terminal 2
```
