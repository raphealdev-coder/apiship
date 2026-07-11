using System.Security.Claims;
using apiship.Data;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.EntityFrameworkCore;

namespace apiship.Pages.Account;

public class IndexModel : PageModel
{
    private readonly AppDbContext _db;
    private readonly IWebHostEnvironment _env;

    public IndexModel(AppDbContext db, IWebHostEnvironment env)
    {
        _db = db;
        _env = env;
    }

    public Customer Customer { get; private set; } = default!;
    public IReadOnlyList<Project> Projects { get; private set; } = Array.Empty<Project>();

    private int CurrentCustomerId =>
        int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;

    public async Task<IActionResult> OnGetAsync()
    {
        var customer = await _db.Customers
            .AsNoTracking()
            .Include(c => c.Projects.OrderByDescending(p => p.CreatedUtc))
            .FirstOrDefaultAsync(c => c.Id == CurrentCustomerId);

        if (customer is null)
        {
            return await SignOutAndRedirect();
        }

        Customer = customer;
        Projects = customer.Projects;
        return Page();
    }

    public async Task<IActionResult> OnGetDownloadAsync(int projectId)
    {
        var project = await _db.Projects
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == projectId && p.CustomerId == CurrentCustomerId);

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

    public async Task<IActionResult> OnPostLogoutAsync()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToPage("/Index");
    }

    private async Task<IActionResult> SignOutAndRedirect()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToPage("/Account/Login");
    }

    // Resolve the delivered file path, guarding against path traversal.
    private string ResultPath(Project project)
    {
        var root = Path.GetFullPath(Path.Combine(_env.ContentRootPath, "Uploads"));
        var full = Path.GetFullPath(Path.Combine(root, project.DeploymentId, "result",
            Path.GetFileName(project.ResultFileName!)));
        return full.StartsWith(root, StringComparison.Ordinal) ? full : string.Empty;
    }
}
