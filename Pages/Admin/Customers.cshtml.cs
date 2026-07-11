using apiship.Data;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace apiship.Pages.Admin;

public class CustomersModel : PageModel
{
    private readonly AppDbContext _db;

    private const int PageSize = 12;

    public CustomersModel(AppDbContext db)
    {
        _db = db;
    }

    public record CustomerRow(Customer Customer, int ProjectCount, int MonthlySpend);

    public IReadOnlyList<CustomerRow> Rows { get; private set; } = Array.Empty<CustomerRow>();

    [BindProperty(SupportsGet = true)]
    public string? Search { get; set; }

    [BindProperty(SupportsGet = true)]
    public int PageNumber { get; set; } = 1;

    public int TotalPages { get; private set; }
    public int MatchCount { get; private set; }

    public async Task OnGetAsync()
    {
        IQueryable<Customer> query = _db.Customers.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(Search))
        {
            var term = Search.Trim();
            query = query.Where(c =>
                EF.Functions.Like(c.FullName, $"%{term}%") ||
                EF.Functions.Like(c.Email, $"%{term}%") ||
                EF.Functions.Like(c.Company ?? "", $"%{term}%"));
        }

        MatchCount = await query.CountAsync();
        TotalPages = Math.Max(1, (int)Math.Ceiling(MatchCount / (double)PageSize));
        PageNumber = Math.Clamp(PageNumber, 1, TotalPages);

        Rows = await query
            .OrderByDescending(c => c.CreatedUtc)
            .Skip((PageNumber - 1) * PageSize)
            .Take(PageSize)
            .Select(c => new CustomerRow(
                c,
                c.Projects.Count,
                c.Projects.Sum(p => (int?)p.Price) ?? 0))
            .ToListAsync();
    }

    public async Task<IActionResult> OnPostLogoutAsync()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToPage("/Admin/Login");
    }
}
