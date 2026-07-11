namespace apiship.Data;

/// <summary>A subscription plan option offered by ApiShip.</summary>
public record PlanOption(string Name, int Price, string Summary);

/// <summary>
/// Single source of truth for the available plans, shared by onboarding,
/// the customer billing area and the admin views.
/// </summary>
public static class PlanCatalog
{
    public static readonly IReadOnlyList<PlanOption> Plans = new List<PlanOption>
    {
        new("Starter", 50, "100k API calls / month · 3 endpoints"),
        new("Growth", 120, "2M API calls / month · unlimited endpoints"),
        new("Scale", 300, "20M API calls / month · 99.99% SLA")
    };

    public static PlanOption Default => Plans[0];

    public static PlanOption? Find(string? name) =>
        Plans.FirstOrDefault(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase));

    public static bool IsValid(string? name) => Find(name) is not null;
}
