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

    public IndexModel(AppDbContext db)
    {
        _db = db;
    }

    public List<Submission> Submissions { get; private set; } = new();
    public int TotalRevenue { get; private set; }

    public async Task OnGetAsync()
    {
        Submissions = await _db.Submissions
            .OrderByDescending(s => s.CreatedUtc)
            .ToListAsync();
        TotalRevenue = Submissions.Sum(s => s.Price);
    }

    public async Task<IActionResult> OnPostLogoutAsync()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToPage("/Admin/Login");
    }
}
