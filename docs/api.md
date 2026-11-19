# API Contract

All bodies are JSON with `snake_case` field names.
Every response carries an `X-Correlation-ID` header. If the client sends one it is reused,
otherwise the service generates a UUID. Order Service forwards it to Restaurant Service so a
single request can be traced across both services' logs.

Error body (all services, all 4xx/5xx):

```json
{ "error": "not_found", "message": "restaurant 'r9' not found", "correlation_id": "6f1c..." }
```

---

## Restaurant Service (default port 8081)

### `GET /healthz`
`200 OK`
```json
{ "status": "ok", "service": "restaurant" }
```

### `GET /restaurants`
`200 OK` (summary list, no menus)
```json
[
  { "id": "r1", "name": "Abesh Restaurant",       "cuisine": "Bangladeshi" },
  { "id": "r2", "name": "Khao San Dhanmondi",     "cuisine": "Thai" },
  { "id": "r3", "name": "Gojo Ramen Mohammadpur", "cuisine": "Japanese" }
]
```

### `GET /restaurants/{id}`
`200 OK`
```json
{
  "id": "r1",
  "name": "Abesh Restaurant",
  "cuisine": "Bangladeshi",
  "menu": [
    { "id": "m101", "name": "Kacchi Biryani", "price": 350.00 },
    { "id": "m102", "name": "Chicken Roast",  "price": 220.00 },
    { "id": "m103", "name": "Borhani",        "price": 60.00 }
  ]
}
```
`404 Not Found` if the restaurant does not exist.

---

## Order Service (default port 8080)

Config: `RESTAURANT_URL` (required, e.g. `http://restaurant:8081` in Docker),
`RESTAURANT_TIMEOUT_MS` (default `2000`), `PORT` (default `8080`).

### `GET /healthz`
`200 OK`
```json
{ "status": "ok", "service": "order" }
```

### `POST /orders`
Request:
```json
{
  "restaurant_id": "r1",
  "items": [
    { "menu_item_id": "m101", "quantity": 2 },
    { "menu_item_id": "m103", "quantity": 1 }
  ]
}
```
`201 Created` (`Location: /orders/{order_id}`)
```json
{
  "order_id": "3b0f7c1e-6a51-4c3e-9a3a-0f6f0d8f2c11",
  "status": "CREATED",
  "created_at": "2026-11-17T18:30:00.123Z"
}
```

### `GET /orders/{id}`
`200 OK`
```json
{
  "order_id": "3b0f7c1e-6a51-4c3e-9a3a-0f6f0d8f2c11",
  "restaurant_id": "r1",
  "items": [
    { "menu_item_id": "m101", "quantity": 2 },
    { "menu_item_id": "m103", "quantity": 1 }
  ],
  "status": "CREATED",
  "created_at": "2026-11-17T18:30:00.123Z",
  "correlation_id": "6f1c2d9e-..."
}
```

### Error mapping

| Situation | Status | `error` |
|---|---|---|
| Body is not valid JSON | 400 | `malformed_json` |
| `restaurant_id` missing/empty, `items` missing/empty, `menu_item_id` missing | 400 | `validation_failed` |
| `quantity` missing, `<= 0` or `> 100` | 400 | `validation_failed` |
| Restaurant does not exist (Restaurant Service returned 404) | 404 | `restaurant_not_found` |
| A `menu_item_id` is not on that restaurant's menu | 404 | `menu_item_not_found` |
| `GET /orders/{id}` unknown id | 404 | `order_not_found` |
| Restaurant Service unreachable, timed out or returned 5xx | 503 | `dependency_unavailable` |

---

## Seed catalogue

| Restaurant | Cuisine | Menu items |
|---|---|---|
| `r1` Abesh Restaurant | Bangladeshi | `m101` Kacchi Biryani 350.00 · `m102` Chicken Roast 220.00 · `m103` Borhani 60.00 |
| `r2` Khao San Dhanmondi | Thai | `m201` Tom yum fried rice 650.00 · `m202` nun bun 780.00 · `m203` Garlic Bread 180.00 |
| `r3` Gojo Ramen Mohammadpur | Japanese | `m301` Tonkotsu Ramen 520.00 · `m302` Gyoza (6 pcs) 240.00 · `m303` Matcha Ice Cream 150.00 |

3 restaurants, 9 menu items.
