# Outage Experiment

Goal: when Restaurant Service is stopped, `POST /orders` must fail with a **bounded**
response (503 within the timeout) instead of hanging.

## Steps

```bash
# 1. both containers running (see README)
docker ps

# 2. normal order works
curl -i -X POST localhost:8080/orders -H 'Content-Type: application/json' \
  -H 'X-Correlation-ID: before-outage' \
  -d '{"restaurant_id":"r1","items":[{"menu_item_id":"m101","quantity":1}]}'

# 3. stop Restaurant Service
docker stop restaurant

# 4. order again, measuring the time
time curl -i -X POST localhost:8080/orders -H 'Content-Type: application/json' \
  -H 'X-Correlation-ID: during-outage' \
  -d '{"restaurant_id":"r1","items":[{"menu_item_id":"m101","quantity":1}]}'

# 5. collect logs
docker logs order 2>&1 | grep -E 'before-outage|during-outage'

# 6. bring it back and confirm recovery
docker start restaurant
curl -i -X POST localhost:8080/orders -H 'Content-Type: application/json' \
  -H 'X-Correlation-ID: after-outage' \
  -d '{"restaurant_id":"r1","items":[{"menu_item_id":"m101","quantity":1}]}'
```

## Expected result

| Step | Expected |
|---|---|
| 2 – before outage | `201 Created` |
| 4 – during outage | `503` `{"error":"dependency_unavailable",...}` in well under 2 s. Log says `Restaurant service unreachable: ...` (in Docker usually "Name or service not known", because a stopped container's name no longer resolves) |
| 6 – after restart | `201 Created` again, no restart of Order needed |

## Results (run on 2026-11-18, Docker containers on network `assignment`)

| Step | Result |
|---|---|
| 3 – `docker stop restaurant` | container stopped |
| 4 – order during outage | **503** `dependency_unavailable` in **0.615 s** total (server side: 509 ms), no hanging |
| 5 – Order log | `Restaurant service unreachable: Name or service not known (restaurant:8081)`, tagged `correlation_id=during-outage` |
| 6 – order after `docker start restaurant` | **201 Created**, Order Service recovered without a restart |

### Step 4 – order while Restaurant is stopped

```
$ docker stop restaurant
restaurant

$ time curl -i -X POST localhost:8080/orders -H 'Content-Type: application/json' \
    -H 'X-Correlation-ID: during-outage' \
    -d '{"restaurant_id":"r1","items":[{"menu_item_id":"m101","quantity":1}]}'
HTTP/1.1 503 Service Unavailable
Content-Type: application/json; charset=utf-8
Date: Wed, 18 Nov 2026 17:57:51 GMT
Server: Kestrel
Transfer-Encoding: chunked
X-Correlation-ID: during-outage

{"error":"dependency_unavailable","message":"restaurant service is unavailable, try again later","correlation_id":"during-outage"}
curl ...  0.01s user 0.02s system 4% cpu 0.615 total
```

### Step 5 – Order Service logs (`docker logs order`)

Readable summary:

```
2026-11-18T17:57:51.419Z  Error  Order.RestaurantClient        correlation_id=during-outage  Restaurant service unreachable: Name or service not known (restaurant:8081)
2026-11-18T17:57:51.429Z  Info   Order.CorrelationIdMiddleware correlation_id=during-outage  POST /orders -> 503 in 509 ms
```

<details>
<summary>Raw JSON log lines</summary>

```json
{"Timestamp":"2026-11-18T17:57:51.419Z ","EventId":0,"LogLevel":"Error","Category":"Order.RestaurantClient","Message":"Restaurant service unreachable: Name or service not known (restaurant:8081)","State":{"Message":"Restaurant service unreachable: Name or service not known (restaurant:8081)","Reason":"Name or service not known (restaurant:8081)","{OriginalFormat}":"Restaurant service unreachable: {Reason}"},"Scopes":[{"Message":"SpanId:9b894ec8fd056522, TraceId:63932f0584d19e1bdab67002e5bf40cb, ParentId:0000000000000000","SpanId":"9b894ec8fd056522","TraceId":"63932f0584d19e1bdab67002e5bf40cb","ParentId":"0000000000000000"},{"Message":"ConnectionId:0HNQ5J2O54CVT","ConnectionId":"0HNQ5J2O54CVT"},{"Message":"RequestPath:/orders RequestId:0HNQ5J2O54CVT:00000001","RequestId":"0HNQ5J2O54CVT:00000001","RequestPath":"/orders"},{"Message":"correlation_id=during-outage","CorrelationId":"during-outage","{OriginalFormat}":"correlation_id={CorrelationId}"}]}
{"Timestamp":"2026-11-18T17:57:51.429Z ","EventId":0,"LogLevel":"Information","Category":"Order.CorrelationIdMiddleware","Message":"POST /orders -> 503 in 509 ms","State":{"Message":"POST /orders -> 503 in 509 ms","Method":"POST","Path":"/orders","StatusCode":503,"ElapsedMs":509,"{OriginalFormat}":"{Method} {Path} -> {StatusCode} in {ElapsedMs} ms"},"Scopes":[{"Message":"SpanId:9b894ec8fd056522, TraceId:63932f0584d19e1bdab67002e5bf40cb, ParentId:0000000000000000","SpanId":"9b894ec8fd056522","TraceId":"63932f0584d19e1bdab67002e5bf40cb","ParentId":"0000000000000000"},{"Message":"ConnectionId:0HNQ5J2O54CVT","ConnectionId":"0HNQ5J2O54CVT"},{"Message":"RequestPath:/orders RequestId:0HNQ5J2O54CVT:00000001","RequestId":"0HNQ5J2O54CVT:00000001","RequestPath":"/orders"},{"Message":"correlation_id=during-outage","CorrelationId":"during-outage","{OriginalFormat}":"correlation_id={CorrelationId}"}]}
```

</details>

### Step 6 – recovery after `docker start restaurant`

```
$ docker start restaurant
restaurant

$ curl -i -X POST localhost:8080/orders -H 'Content-Type: application/json' \
    -H 'X-Correlation-ID: after-outage' \
    -d '{"restaurant_id":"r1","items":[{"menu_item_id":"m101","quantity":1}]}'
HTTP/1.1 201 Created
Content-Type: application/json; charset=utf-8
Date: Wed, 18 Nov 2026 17:59:06 GMT
Server: Kestrel
Location: /orders/394d3213-8856-4b1f-9ba1-154e9d9758e7
Transfer-Encoding: chunked
X-Correlation-ID: after-outage

{"order_id":"394d3213-8856-4b1f-9ba1-154e9d9758e7","status":"CREATED","created_at":"2026-11-18T17:59:06.4648974Z"}
```

### Conclusion

With Restaurant Service stopped, Order Service returned a 503 in about half a second and did not hang. The
error was logged with the request's correlation ID. Once Restaurant was started again, orders succeeded
without restarting Order Service.
