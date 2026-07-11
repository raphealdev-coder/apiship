using apiship.Data;
using apiship.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.EntityFrameworkCore;

namespace apiship.Pages.Admin;

public class DetailsModel : PageModel
{
    private readonly AppDbContext _db;
    private readonly IWebHostEnvironment _env;
    private readonly IApiKeyGenerator _keys;
    private readonly ILogger<DetailsModel> _logger;

    public DetailsModel(AppDbContext db, IWebHostEnvironment env, IApiKeyGenerator keys,
        ILogger<DetailsModel> logger)
    {
        _db = db;
        _env = env;
        _keys = keys;
        _logger = logger;
    }

    public Project Project { get; private set; } = default!;
    public bool UploadExists { get; private set; }

    [BindProperty]
    public IFormFile? ResultUpload { get; set; }

    [TempData]
    public string? StatusMessage { get; set; }

    // Set once, right after a key is generated, so the raw secret can be shown.
    [TempData]
    public string? NewApiKey { get; set; }

    public async Task<IActionResult> OnGetAsync(int id)
    {
        var project = await LoadProjectAsync(id);
        if (project is null)
        {
            return NotFound();
        }

        Project = project;
        UploadExists = System.IO.File.Exists(UploadPath(project));
        return Page();
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

        StatusMessage = $"Status updated to {status.Label()}.";
        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostBillingAsync(int id, string? plan, BillingCycle cycle,
        DateTime? trialEnds, int monthsPaid, bool subscribed)
    {
        var project = await _db.Projects.FindAsync(id);
        if (project is null)
        {
            return NotFound();
        }

        // Plan chosen at signup can be corrected/changed here; keep price in sync.
        var planOption = PlanCatalog.Find(plan);
        if (planOption is not null)
        {
            project.Plan = planOption.Name;
            project.Price = planOption.Price;
        }

        project.BillingCycle = cycle;

        if (trialEnds is not null)
        {
            project.TrialEndsUtc = DateTime.SpecifyKind(trialEnds.Value, DateTimeKind.Utc);
        }

        var now = DateTime.UtcNow;
        if (monthsPaid > 0)
        {
            // Extend from the current paid-through date if still active, otherwise from now.
            var basis = project.IsSubscribed && project.NextBillingUtc > now ? project.NextBillingUtc : now;
            project.NextBillingUtc = basis.AddMonths(monthsPaid);
            project.IsSubscribed = true;
            project.SubscribedUtc ??= now;
            project.PlanChangedUtc = now;
        }
        else
        {
            project.IsSubscribed = subscribed;
            if (!subscribed)
            {
                project.SubscribedUtc = null;
            }
        }

        await _db.SaveChangesAsync();

        _logger.LogInformation("Billing updated for project {DeploymentId} by {User} (plan {Plan}, +{Months}mo).",
            project.DeploymentId, User.Identity?.Name, project.Plan, monthsPaid);

        StatusMessage = "Subscription & billing updated.";
        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostCreateKeyAsync(int id, string? label)
    {
        var project = await _db.Projects.FindAsync(id);
        if (project is null)
        {
            return NotFound();
        }

        var (fullKey, prefix, hash) = _keys.Create("live");
        _db.ApiKeys.Add(new ApiKey
        {
            ProjectId = project.Id,
            Label = string.IsNullOrWhiteSpace(label) ? "Secondary" : label.Trim(),
            Prefix = prefix,
            SecretHash = hash
        });
        await _db.SaveChangesAsync();

        NewApiKey = fullKey;
        StatusMessage = "New API key generated.";
        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostRevokeKeyAsync(int id, int keyId)
    {
        var key = await _db.ApiKeys.FirstOrDefaultAsync(k => k.Id == keyId && k.ProjectId == id);
        if (key is null)
        {
            return NotFound();
        }

        key.Revoked = true;
        key.RevokedUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        StatusMessage = "API key revoked.";
        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostUploadResultAsync(int id)
    {
        var project = await _db.Projects.FindAsync(id);
        if (project is null)
        {
            return NotFound();
        }

        if (ResultUpload is null || ResultUpload.Length == 0)
        {
            StatusMessage = "Please choose a file to upload.";
            return RedirectToPage(new { id });
        }

        var ext = Path.GetExtension(ResultUpload.FileName).ToLowerInvariant();
        if (ext != ".zip")
        {
            StatusMessage = "The deliverable must be a .zip archive.";
            return RedirectToPage(new { id });
        }

        if (ResultUpload.Length > 100 * 1024 * 1024)
        {
            StatusMessage = "The file exceeds the 100 MB limit.";
            return RedirectToPage(new { id });
        }

        var resultDir = Path.Combine(_env.ContentRootPath, "Uploads", project.DeploymentId, "result");
        Directory.CreateDirectory(resultDir);

        var fileName = Path.GetFileName(ResultUpload.FileName);
        var destination = Path.Combine(resultDir, fileName);
        await using (var stream = System.IO.File.Create(destination))
        {
            await ResultUpload.CopyToAsync(stream);
        }

        project.ResultFileName = fileName;
        project.ResultUploadedUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        _logger.LogInformation("Deliverable uploaded for project {DeploymentId} by {User}.",
            project.DeploymentId, User.Identity?.Name);

        StatusMessage = "Deliverable uploaded. The customer can now download it.";
        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnGetDownloadResultAsync(int id)
    {
        var project = await _db.Projects.FindAsync(id);
        if (project is null || string.IsNullOrEmpty(project.ResultFileName))
        {
            return NotFound();
        }

        var path = ResultPath(project);
        if (string.IsNullOrEmpty(path) || !System.IO.File.Exists(path))
        {
            return NotFound();
        }

        if (!new FileExtensionContentTypeProvider().TryGetContentType(path, out var contentType))
        {
            contentType = "application/octet-stream";
        }

        var bytes = await System.IO.File.ReadAllBytesAsync(path);
        return File(bytes, contentType, project.ResultFileName);
    }

    public async Task<IActionResult> OnGetDownloadAsync(int id)
    {
        var project = await _db.Projects.FindAsync(id);
        if (project is null)
        {
            return NotFound();
        }

        var path = UploadPath(project);
        if (string.IsNullOrEmpty(path) || !System.IO.File.Exists(path))
        {
            return NotFound();
        }

        if (!new FileExtensionContentTypeProvider().TryGetContentType(path, out var contentType))
        {
            contentType = "application/octet-stream";
        }

        var bytes = await System.IO.File.ReadAllBytesAsync(path);
        return File(bytes, contentType, project.UploadFileName);
    }

    private Task<Project?> LoadProjectAsync(int id) =>
        _db.Projects
            .Include(p => p.Customer)
            .Include(p => p.ApiKeys.OrderByDescending(k => k.CreatedUtc))
            .FirstOrDefaultAsync(p => p.Id == id);

    // Resolve the stored bundle path, guarding against path traversal.
    private string UploadPath(Project project)
    {
        var root = Path.GetFullPath(Path.Combine(_env.ContentRootPath, "Uploads"));
        var full = Path.GetFullPath(Path.Combine(root, project.DeploymentId,
            Path.GetFileName(project.UploadFileName)));

        // Ensure the resolved path stays inside the Uploads directory.
        return full.StartsWith(root, StringComparison.Ordinal) ? full : string.Empty;
    }

    // Resolve the delivered (worked-on) file path, guarding against path traversal.
    private string ResultPath(Project project)
    {
        var root = Path.GetFullPath(Path.Combine(_env.ContentRootPath, "Uploads"));
        var full = Path.GetFullPath(Path.Combine(root, project.DeploymentId, "result",
            Path.GetFileName(project.ResultFileName ?? string.Empty)));
        return full.StartsWith(root, StringComparison.Ordinal) ? full : string.Empty;
    }
}
