namespace apiship.Data;

/// <summary>
/// A deployed website / API belonging to a customer. Carries the deployment
/// lifecycle state and the API keys used to call the service.
/// </summary>
public class Project
{
    public int Id { get; set; }

    public int CustomerId { get; set; }
    public Customer? Customer { get; set; }

    public string Name { get; set; } = string.Empty;
    public string? Domain { get; set; }
    public string SiteUrl { get; set; } = string.Empty;
    public string DeploymentId { get; set; } = string.Empty;

    public string Plan { get; set; } = string.Empty;
    public int Price { get; set; }
    public string? Notes { get; set; }
    public string UploadFileName { get; set; } = string.Empty;

    // Which API product type this store/project uses.
    public string? ApiType { get; set; }

    // The finished/worked-on bundle the team delivers back to the customer.
    public string? ResultFileName { get; set; }
    public DateTime? ResultUploadedUtc { get; set; }

    public DeploymentStatus Status { get; set; } = DeploymentStatus.Pending;
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    public DateTime? StatusUpdatedUtc { get; set; }

    // Billing / subscription lifecycle.
    public DateTime TrialEndsUtc { get; set; }
    public DateTime NextBillingUtc { get; set; }
    public DateTime? PlanChangedUtc { get; set; }
    public BillingCycle BillingCycle { get; set; } = BillingCycle.Monthly;
    public bool IsSubscribed { get; set; }
    public DateTime? SubscribedUtc { get; set; }

    public List<ApiKey> ApiKeys { get; set; } = new();

    public bool HasDeliverable => !string.IsNullOrEmpty(ResultFileName);

    public bool IsInTrial => !IsSubscribed && DateTime.UtcNow < TrialEndsUtc;

    public int TrialDaysLeft => IsInTrial
        ? (int)Math.Ceiling((TrialEndsUtc - DateTime.UtcNow).TotalDays)
        : 0;

    // Whether the connector is currently serving traffic (trial or paid).
    public bool ServiceActive => IsSubscribed || IsInTrial;
}

/// <summary>
/// Lifecycle of a project as it moves through deployment.
/// </summary>
public enum DeploymentStatus
{
    Pending = 0,
    Deploying = 1,
    Deployed = 2,
    Rejected = 3
}
