namespace apiship.Data;

/// <summary>
/// A customer account. One customer can own many projects (deployed sites / APIs).
/// </summary>
public class Customer
{
    public int Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? Company { get; set; }

    // Null until the customer registers a login (onboarding creates the record first).
    public string? PasswordHash { get; set; }

    public CustomerStatus Status { get; set; } = CustomerStatus.Active;
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;

    public List<Project> Projects { get; set; } = new();
    public BillingProfile? Billing { get; set; }

    public bool HasLogin => !string.IsNullOrEmpty(PasswordHash);
}

public enum CustomerStatus
{
    Active = 0,
    Suspended = 1
}
