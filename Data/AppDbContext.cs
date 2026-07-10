using Microsoft.EntityFrameworkCore;

namespace apiship.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<Submission> Submissions => Set<Submission>();
}

public class Submission
{
    public int Id { get; set; }
    public string DeploymentId { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string WebsiteName { get; set; } = string.Empty;
    public string? Domain { get; set; }
    public string Plan { get; set; } = string.Empty;
    public int Price { get; set; }
    public string? Notes { get; set; }
    public string UploadFileName { get; set; } = string.Empty;
    public string SiteUrl { get; set; } = string.Empty;
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
}
