using apiship.Data;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace apiship.Pages.Admin;

public class IndexModel : PageModel
{
    private readonly AppDbContext _db;
    private readonly ILogger<IndexModel> _logger;

    private const int PageSize = 10;

    public IndexModel(AppDbContext db, ILogger<IndexModel> logger)
    {
        _db = db;
        _logger = logger;
    }

    // Current page of results.
    public IReadOnlyList<Project> Projects { get; private set; } = Array.Empty<Project>();

    // Headline metrics (calculated across every project, not just this page).
    public int TotalProjects { get; private set; }
    public int TotalCustomers { get; private set; }
    public int TotalRevenue { get; private set; }
    public int PendingCount { get; private set; }
    public int DeployedCount { get; private set; }
    public long TotalApiCalls { get; private set; }

    // Filter / sort / paging state, round-tripped through the query string.
    [BindProperty(SupportsGet = true)]
    public string? Search { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? Status { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? Plan { get; set; }

    [BindProperty(SupportsGet = true)]
    public string Sort { get; set; } = "created_desc";

    [BindProperty(SupportsGet = true)]
    public int PageNumber { get; set; } = 1;

    public int TotalPages { get; private set; }
    public int MatchCount { get; private set; }
    public IReadOnlyList<string> PlanNames { get; private set; } = Array.Empty<string>();

    public bool HasFilters =>
        !string.IsNullOrWhiteSpace(Search) ||
        !string.IsNullOrWhiteSpace(Status) ||
        !string.IsNullOrWhiteSpace(Plan);

    public async Task OnGetAsync()
    {
        await LoadAsync();
    }

    public async Task<IActionResult> OnPostStatusAsync(int id, DeploymentStatus status)
    {
        var project = await _db.Projects.FindAsync(id);
        if (project is null)
        {
            return NotFound();
        }

        project.Status = status;
        project.StatusUpdatedUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        _logger.LogInformation("Project {DeploymentId} moved to {Status} by {User}.",
            project.DeploymentId, status, User.Identity?.Name);

        // Preserve the current filter/sort/page view after the update.
        return RedirectToPage(new { Search, Status, Plan, Sort, PageNumber });
    }

    public async Task<IActionResult> OnPostLogoutAsync()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToPage("/Admin/Login");
    }

    private async Task LoadAsync()
    {
        // Global metrics are independent of the active filters.
        var all = _db.Projects.AsNoTracking();
        TotalProjects = await all.CountAsync();
        TotalCustomers = await _db.Customers.CountAsync();
        TotalRevenue = await all.SumAsync(p => (int?)p.Price) ?? 0;
        PendingCount = await all.CountAsync(p => p.Status == DeploymentStatus.Pending);
        DeployedCount = await all.CountAsync(p => p.Status == DeploymentStatus.Deployed);
        TotalApiCalls = await _db.ApiKeys.SumAsync(k => (long?)k.CallCount) ?? 0;
        PlanNames = await all.Select(p => p.Plan).Distinct().OrderBy(p => p).ToListAsync();

        // Build the filtered query.
        IQueryable<Project> query = all.Include(p => p.Customer);

        if (!string.IsNullOrWhiteSpace(Search))
        {
            var term = Search.Trim();
            query = query.Where(p =>
                EF.Functions.Like(p.Name, $"%{term}%") ||
                EF.Functions.Like(p.Customer!.FullName, $"%{term}%") ||
                EF.Functions.Like(p.Customer!.Email, $"%{term}%") ||
                EF.Functions.Like(p.DeploymentId, $"%{term}%"));
        }

        if (!string.IsNullOrWhiteSpace(Status) &&
            Enum.TryParse<DeploymentStatus>(Status, ignoreCase: true, out var statusFilter))
        {
            query = query.Where(p => p.Status == statusFilter);
        }

        if (!string.IsNullOrWhiteSpace(Plan))
        {
            query = query.Where(p => p.Plan == Plan);
        }

        query = Sort switch
        {
            "created_asc" => query.OrderBy(p => p.CreatedUtc),
            "price_desc" => query.OrderByDescending(p => p.Price).ThenByDescending(p => p.CreatedUtc),
            "price_asc" => query.OrderBy(p => p.Price).ThenByDescending(p => p.CreatedUtc),
            "name" => query.OrderBy(p => p.Name),
            "status" => query.OrderBy(p => p.Status).ThenByDescending(p => p.CreatedUtc),
            _ => query.OrderByDescending(p => p.CreatedUtc)
        };

        MatchCount = await query.CountAsync();
        TotalPages = Math.Max(1, (int)Math.Ceiling(MatchCount / (double)PageSize));
        PageNumber = Math.Clamp(PageNumber, 1, TotalPages);

        Projects = await query
            .Skip((PageNumber - 1) * PageSize)
            .Take(PageSize)
            .ToListAsync();
    }
}
