namespace apiship.Data;

/// <summary>
/// Presentation helpers for <see cref="DeploymentStatus"/> so the label,
/// badge colour and icon stay consistent across the admin views.
/// </summary>
public static class DeploymentStatusExtensions
{
    public static string Label(this DeploymentStatus status) => status switch
    {
        DeploymentStatus.Pending => "Pending",
        DeploymentStatus.Deploying => "Deploying",
        DeploymentStatus.Deployed => "Deployed",
        DeploymentStatus.Rejected => "Rejected",
        _ => status.ToString()
    };

    /// <summary>CSS modifier appended to the <c>status-chip</c> base class.</summary>
    public static string ChipClass(this DeploymentStatus status) => status switch
    {
        DeploymentStatus.Pending => "status-chip--pending",
        DeploymentStatus.Deploying => "status-chip--deploying",
        DeploymentStatus.Deployed => "status-chip--deployed",
        DeploymentStatus.Rejected => "status-chip--rejected",
        _ => "status-chip--pending"
    };

    public static string Icon(this DeploymentStatus status) => status switch
    {
        DeploymentStatus.Pending => "●",
        DeploymentStatus.Deploying => "◐",
        DeploymentStatus.Deployed => "✓",
        DeploymentStatus.Rejected => "✕",
        _ => "●"
    };

    // ----- Customer status helpers -----

    public static string Label(this CustomerStatus status) => status switch
    {
        CustomerStatus.Active => "Active",
        CustomerStatus.Suspended => "Suspended",
        _ => status.ToString()
    };

    public static string ChipClass(this CustomerStatus status) => status switch
    {
        CustomerStatus.Active => "status-chip--deployed",
        CustomerStatus.Suspended => "status-chip--rejected",
        _ => "status-chip--pending"
    };
}
