namespace apiship.Data;

/// <summary>A type of API the connector can provide for a store/project.</summary>
public record ApiProduct(string Key, string Name, string Icon, string Summary);

/// <summary>
/// The catalog of API product types shown on the landing page and selectable
/// during onboarding.
/// </summary>
public static class ApiProductCatalog
{
    public static readonly IReadOnlyList<ApiProduct> Products = new List<ApiProduct>
    {
        new("inventory", "Inventory Sync API", "📦", "Keep stock levels consistent across every store in real time."),
        new("orders", "Orders API", "🧾", "Push and pull orders between your stores, POS and systems."),
        new("pricing", "Pricing API", "🏷️", "Broadcast price changes to every channel instantly."),
        new("webhooks", "Webhooks API", "🔔", "Subscribe to real-time events the moment they happen."),
        new("catalog", "Catalog API", "🗂️", "Manage products, variants and media from one source of truth."),
        new("analytics", "Analytics API", "📊", "Pull unified sales and traffic metrics across stores.")
    };

    public static ApiProduct? Find(string? key) =>
        Products.FirstOrDefault(p => string.Equals(p.Key, key, StringComparison.OrdinalIgnoreCase));

    public static string DisplayName(string? key) => Find(key)?.Name ?? "API Connector";
}
