namespace Order;

// ---------- What the client sends to POST /orders ----------

public class CreateOrderRequest
{
    public string? RestaurantId { get; set; }
    public List<OrderItemRequest>? Items { get; set; }
}

public class OrderItemRequest
{
    public string? MenuItemId { get; set; }
    public int? Quantity { get; set; }   // nullable so we can tell "missing" apart from 0
}

// ---------- What we store and return ----------

public class OrderItem
{
    public string MenuItemId { get; set; } = "";
    public int Quantity { get; set; }
}

public class OrderRecord
{
    public string OrderId { get; set; } = "";
    public string RestaurantId { get; set; } = "";
    public List<OrderItem> Items { get; set; } = new List<OrderItem>();
    public string Status { get; set; } = "";
    public DateTime CreatedAt { get; set; }
    public string CorrelationId { get; set; } = "";
}

public class CreateOrderResponse
{
    public string OrderId { get; set; } = "";
    public string Status { get; set; } = "";
    public DateTime CreatedAt { get; set; }
}

public class ErrorResponse
{
    public string Error { get; set; } = "";
    public string Message { get; set; } = "";
    public string CorrelationId { get; set; } = "";
}

// ---------- What Restaurant Service returns (only the fields we need) ----------

public class RestaurantInfo
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public List<MenuItemInfo> Menu { get; set; } = new List<MenuItemInfo>();
}

public class MenuItemInfo
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public decimal Price { get; set; }
}
