using System.ComponentModel.DataAnnotations;
using apiship.Data;
using apiship.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace apiship.Pages.Admin;

public class CustomerCreateModel : PageModel
{
    private readonly AppDbContext _db;
    private readonly IPasswordService _passwords;
    private readonly ILogger<CustomerCreateModel> _logger;

    public CustomerCreateModel(AppDbContext db, IPasswordService passwords, ILogger<CustomerCreateModel> logger)
    {
        _db = db;
        _passwords = passwords;
        _logger = logger;
    }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    [TempData]
    public string? StatusMessage { get; set; }

    public void OnGet()
    {
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            return Page();
        }

        var email = Input.Email.Trim().ToLowerInvariant();
        if (await _db.Customers.AnyAsync(c => c.Email == email))
        {
            ModelState.AddModelError("Input.Email", "A customer with this email already exists.");
            return Page();
        }

        var customer = new Customer
        {
            FullName = Input.FullName.Trim(),
            Email = email,
            Company = string.IsNullOrWhiteSpace(Input.Company) ? null : Input.Company.Trim(),
            Status = Input.Suspended ? CustomerStatus.Suspended : CustomerStatus.Active
        };

        // An initial password is optional; when omitted the customer can register later.
        if (!string.IsNullOrEmpty(Input.Password))
        {
            customer.PasswordHash = _passwords.Hash(Input.Password);
        }

        _db.Customers.Add(customer);
        await _db.SaveChangesAsync();

        _logger.LogInformation("Customer {Email} created by {User}.", email, User.Identity?.Name);
        StatusMessage = "Customer created.";
        return RedirectToPage("/Admin/Customer", new { id = customer.Id });
    }

    public async Task<IActionResult> OnPostLogoutAsync()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToPage("/Admin/Login");
    }

    public class InputModel
    {
        [Required, StringLength(120, MinimumLength = 2)]
        [Display(Name = "Full name")]
        public string FullName { get; set; } = string.Empty;

        [Required, EmailAddress]
        [Display(Name = "Email")]
        public string Email { get; set; } = string.Empty;

        [StringLength(100)]
        [Display(Name = "Company (optional)")]
        public string? Company { get; set; }

        [StringLength(100, MinimumLength = 8)]
        [DataType(DataType.Password)]
        [Display(Name = "Initial password (optional)")]
        public string? Password { get; set; }

        [Display(Name = "Create as suspended")]
        public bool Suspended { get; set; }
    }
}
