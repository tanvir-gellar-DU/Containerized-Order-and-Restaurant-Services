namespace Order;

/// <summary>
/// In-memory order storage. A normal Dictionary is NOT safe when many requests
/// use it at the same time, so every read and write goes through lock(_lock).
/// Only one thread can be inside a lock block at a time (this is our mutex).
/// In A2 this class gets replaced by PostgreSQL.
/// </summary>
public class OrderStore
{
    private readonly Dictionary<string, OrderRecord> _orders = new Dictionary<string, OrderRecord>();
    private readonly object _lock = new object();

    public void Add(OrderRecord order)
    {
        lock (_lock)
        {
            _orders[order.OrderId] = order;
        }
    }

    public OrderRecord? Get(string orderId)
    {
        lock (_lock)
        {
            if (_orders.ContainsKey(orderId))
            {
                return _orders[orderId];
            }
            return null;
        }
    }

    public int Count()
    {
        lock (_lock)
        {
            return _orders.Count;
        }
    }
}
