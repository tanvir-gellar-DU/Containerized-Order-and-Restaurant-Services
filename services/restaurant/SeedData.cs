namespace Restaurant;

/// <summary>
/// Read-only seed catalogue. The dictionary is built once at startup and never
/// mutated, so concurrent reads are safe without locking.
/// </summary>
public static class SeedData
{
    public static readonly IReadOnlyDictionary<string, RestaurantDetail> Restaurants =
        new List<RestaurantDetail>
        {
            new("r1", "Abesh Restaurant", "Bangladeshi", new List<MenuItem>
            {
                new("m101", "Kacchi Biryani", 350.00m),
                new("m102", "Chicken Roast", 220.00m),
                new("m103", "Borhani", 60.00m),
            }),
            new("r2", "Khao San Dhanmondi", "Thai", new List<MenuItem>
            {
                new("m201", "Tom yum fried rice ", 650.00m),
                new("m202", "nun bun", 780.00m),
                new("m203", "Garlic Bread", 180.00m),
            }),
            new("r3", "Gojo Ramen Mohammadpur", "Japanese", new List<MenuItem>
            {
                new("m301", "Tonkotsu Ramen", 520.00m),
                new("m302", "Gyoza (6 pcs)", 240.00m),
                new("m303", "Matcha Ice Cream", 150.00m),
            }),
        }.ToDictionary(r => r.Id, StringComparer.Ordinal);
}
