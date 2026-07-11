using Microsoft.EntityFrameworkCore;

namespace apiship.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Project> Projects => Set<Project>();
    public DbSet<ApiKey> ApiKeys => Set<ApiKey>();
    public DbSet<BillingProfile> BillingProfiles => Set<BillingProfile>();
    public DbSet<AppSetting> AppSettings => Set<AppSetting>();
    public DbSet<Payment> Payments => Set<Payment>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Customer>(e =>
        {
            e.HasIndex(c => c.Email).IsUnique();
            e.Property(c => c.FullName).HasMaxLength(120);
            e.Property(c => c.Email).HasMaxLength(200);
            e.HasMany(c => c.Projects)
             .WithOne(p => p.Customer!)
             .HasForeignKey(p => p.CustomerId)
             .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(c => c.Billing)
             .WithOne(b => b.Customer!)
             .HasForeignKey<BillingProfile>(b => b.CustomerId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Project>(e =>
        {
            e.HasIndex(p => p.DeploymentId).IsUnique();
            e.Property(p => p.Name).HasMaxLength(120);
            e.HasMany(p => p.ApiKeys)
             .WithOne(k => k.Project!)
             .HasForeignKey(k => k.ProjectId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ApiKey>(e =>
        {
            e.HasIndex(k => k.SecretHash);
            e.Property(k => k.Prefix).HasMaxLength(40);
            e.Property(k => k.SecretHash).HasMaxLength(80);
        });

        modelBuilder.Entity<BillingProfile>(e =>
        {
            e.HasIndex(b => b.CustomerId).IsUnique();
            e.Property(b => b.CardLast4).HasMaxLength(4);
        });

        modelBuilder.Entity<Payment>(e =>
        {
            e.HasIndex(p => p.Reference).IsUnique();
            e.Property(p => p.Reference).HasMaxLength(64);
            e.HasOne(p => p.Customer!)
             .WithMany()
             .HasForeignKey(p => p.CustomerId)
             .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
