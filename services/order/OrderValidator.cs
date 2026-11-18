namespace Order;

/// <summary>
/// Checks the request body before we call any other service.
/// Returns an error message, or null when the request is valid.
/// </summary>
public static class OrderValidator
{
    public const int MaxQuantity = 100;

    public static string? Validate(CreateOrderRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.RestaurantId))
        {
            return "restaurant_id is required";
        }

        if (request.Items == null || request.Items.Count == 0)
        {
            return "items must contain at least one item";
        }

        for (int i = 0; i < request.Items.Count; i++)
        {
            OrderItemRequest item = request.Items[i];

            if (item == null)
            {
                return "items[" + i + "] must not be null";
            }

            if (string.IsNullOrWhiteSpace(item.MenuItemId))
            {
                return "items[" + i + "].menu_item_id is required";
            }

            if (item.Quantity == null)
            {
                return "items[" + i + "].quantity is required";
            }

            if (item.Quantity <= 0 || item.Quantity > MaxQuantity)
            {
                return "items[" + i + "].quantity must be between 1 and " + MaxQuantity;
            }
        }

        return null;
    }
}
