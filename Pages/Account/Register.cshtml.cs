using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using apiship.Data;
using apiship.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace apiship.Pages.Account;

public class RegisterModel : PageModel
{
    private readonly AppDbContext _db;
    private readonly IPasswordService _passwords;
    private readonly ILogger<RegisterModel> _logger;

    public RegisterModel(AppDbContext db, IPasswordService passwords, ILogger<RegisterModel> logger)
    {
        _db = db;
        _passwords = passwords;
        _logger = logger;
    }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public string? ErrorMessage { get; private set; }

    public IActionResult OnGet()
    {
        if (User.IsInRole("Customer"))
        {
            return RedirectToPage("/Account/Index");
        }
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            return Page();
        }

        var email = Input.Email.Trim().ToLowerInvariant();
        var customer = await _db.Customers.FirstOrDefaultAsync(c => c.Email == email);

        if (customer is not null && customer.HasLogin)
        {
            ErrorMessage = "An account with this email already exists. Please sign in instead.";
            return Page();
        }

        if (customer is null)
        {
            // Brand-new account.
            customer = new Customer
            {
                FullName = Input.FullName.Trim(),
                Email = email
            };
            _db.Customers.Add(customer);
        }
        else
        {
            // Claim an account that was created during onboarding (no password yet).
            customer.FullName = Input.FullName.Trim();
        }

        customer.PasswordHash = _passwords.Hash(Input.Password);
        await _db.SaveChangesAsync();

        await SignInAsync(customer);
        _logger.LogInformation("Customer {Email} registered.", email);
        return RedirectToPage("/Account/Index");
    }

    private async Task SignInAsync(Customer customer)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, customer.Id.ToString()),
            new(ClaimTypes.Name, customer.FullName),
            new(ClaimTypes.Email, customer.Email),
            new(ClaimTypes.Role, "Customer")
        };
        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity));
    }

    public class InputModel
    {
        [Required, StringLength(80, MinimumLength = 2)]
        [Display(Name = "Full name")]
        public string FullName { get; set; } = string.Empty;

        [Required, EmailAddress]
        [Display(Name = "Email")]
        public string Email { get; set; } = string.Empty;

        [Required, StringLength(100, MinimumLength = 8)]
        [DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;

        [Required]
        [DataType(DataType.Password)]
        [Compare(nameof(Password), ErrorMessage = "Passwords do not match.")]
        [Display(Name = "Confirm password")]
        public string ConfirmPassword { get; set; } = string.Empty;
    }
}
