using apiship.Data;
using apiship.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

namespace apiship.Pages.Admin;

public class CustomerModel : PageModel
{
    private readonly AppDbContext _db;
    private readonly IPasswordService _passwords;
    private readonly ILogger<CustomerModel> _logger;

    public CustomerModel(AppDbContext db, IPasswordService passwords, ILogger<CustomerModel> logger)
    {
        _db = db;
        _passwords = passwords;
        _logger = logger;
    }

    public Customer Customer { get; private set; } = default!;
    public int MonthlySpend { get; private set; }
    public long TotalApiCalls { get; private set; }

    [BindProperty]
    public EditInput Edit { get; set; } = new();

    [BindProperty]
    public string? NewPassword { get; set; }

    [TempData]
    public string? StatusMessage { get; set; }

    public async Task<IActionResult> OnGetAsync(int id)
    {
        var customer = await _db.Customers
            .AsNoTracking()
            .Include(c => c.Projects.OrderByDescending(p => p.CreatedUtc))
                .ThenInclude(p => p.ApiKeys)
            .FirstOrDefaultAsync(c => c.Id == id);

        if (customer is null)
        {
            return NotFound();
        }

        Customer = customer;
        MonthlySpend = customer.Projects.Sum(p => p.Price);
        TotalApiCalls = customer.Projects.SelectMany(p => p.ApiKeys).Sum(k => k.CallCount);
        Edit = new EditInput
        {
            FullName = customer.FullName,
            Email = customer.Email,
            Company = customer.Company
        };
        return Page();
    }

    public async Task<IActionResult> OnPostEditAsync(int id)
    {
        var customer = await _db.Customers.FindAsync(id);
        if (customer is null)
        {
            return NotFound();
        }

        if (!ModelState.IsValid)
        {
            // Reload display data for the page render.
            await OnGetAsync(id);
            return Page();
        }

        var newEmail = Edit.Email.Trim().ToLowerInvariant();
        if (newEmail != customer.Email &&
            await _db.Customers.AnyAsync(c => c.Email == newEmail && c.Id != id))
        {
            ModelState.AddModelError("Edit.Email", "That email is already in use.");
            await OnGetAsync(id);
            return Page();
        }

        customer.FullName = Edit.FullName.Trim();
        customer.Email = newEmail;
        customer.Company = string.IsNullOrWhiteSpace(Edit.Company) ? null : Edit.Company.Trim();
        await _db.SaveChangesAsync();

        _logger.LogInformation("Customer {Email} details edited by {User}.", customer.Email, User.Identity?.Name);
        StatusMessage = "Customer details updated.";
        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostToggleAsync(int id)
    {
        var customer = await _db.Customers.FindAsync(id);
        if (customer is null)
        {
            return NotFound();
        }

        customer.Status = customer.Status == CustomerStatus.Active
            ? CustomerStatus.Suspended
            : CustomerStatus.Active;
        await _db.SaveChangesAsync();

        _logger.LogInformation("Customer {Email} set to {Status} by {User}.",
            customer.Email, customer.Status, User.Identity?.Name);

        StatusMessage = $"Customer {customer.Status.Label().ToLowerInvariant()}.";
        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostSetPasswordAsync(int id)
    {
        var customer = await _db.Customers.FindAsync(id);
        if (customer is null)
        {
            return NotFound();
        }

        if (string.IsNullOrWhiteSpace(NewPassword) || NewPassword.Length < 8)
        {
            StatusMessage = "Password must be at least 8 characters.";
            return RedirectToPage(new { id });
        }

        customer.PasswordHash = _passwords.Hash(NewPassword);
        await _db.SaveChangesAsync();

        _logger.LogInformation("Password set for customer {Email} by {User}.", customer.Email, User.Identity?.Name);
        StatusMessage = "Password updated. Share it with the customer securely.";
        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostDeleteAsync(int id)
    {
        var customer = await _db.Customers.FindAsync(id);
        if (customer is null)
        {
            return NotFound();
        }

        // Cascade delete removes the customer's projects and API keys too.
        _db.Customers.Remove(customer);
        await _db.SaveChangesAsync();

        _logger.LogInformation("Customer {Email} deleted by {User}.", customer.Email, User.Identity?.Name);
        StatusMessage = $"Customer {customer.FullName} deleted.";
        return RedirectToPage("/Admin/Customers");
    }

    public async Task<IActionResult> OnPostLogoutAsync()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToPage("/Admin/Login");
    }

    public class EditInput
    {
        [Required, StringLength(120, MinimumLength = 2)]
        [Display(Name = "Full name")]
        public string FullName { get; set; } = string.Empty;

        [Required, EmailAddress]
        [Display(Name = "Email")]
        public string Email { get; set; } = string.Empty;

        [StringLength(100)]
        [Display(Name = "Company")]
        public string? Company { get; set; }
    }
}
