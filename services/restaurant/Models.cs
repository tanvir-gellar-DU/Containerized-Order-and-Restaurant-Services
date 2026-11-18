namespace Restaurant;

public sealed record MenuItem(string Id, string Name, decimal Price);

public sealed record RestaurantDetail(string Id, string Name, string Cuisine, IReadOnlyList<MenuItem> Menu);

public sealed record RestaurantSummary(string Id, string Name, string Cuisine);

public sealed record ErrorResponse(string Error, string Message, string CorrelationId);
