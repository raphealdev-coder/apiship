namespace apiship.Data;

/// <summary>
/// An API key that authenticates requests to a project's service.
/// The full secret is only shown once at creation; we persist a SHA-256 hash
/// plus a short, non-sensitive prefix for display.
/// </summary>
public class ApiKey
{
    public int Id { get; set; }

    public int ProjectId { get; set; }
    public Project? Project { get; set; }

    public string Label { get; set; } = "Default";

    // Non-secret, human-recognisable prefix, e.g. "ak_live_1a2b3c".
    public string Prefix { get; set; } = string.Empty;

    // SHA-256 hash (hex) of the full key. The raw key is never stored.
    public string SecretHash { get; set; } = string.Empty;

    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    public DateTime? LastUsedUtc { get; set; }
    public long CallCount { get; set; }

    public bool Revoked { get; set; }
    public DateTime? RevokedUtc { get; set; }
}
