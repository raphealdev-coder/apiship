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

public class LoginModel : PageModel
{
    private readonly AppDbContext _db;
    private readonly IPasswordService _passwords;
    private readonly ILogger<LoginModel> _logger;

    public LoginModel(AppDbContext db, IPasswordService passwords, ILogger<LoginModel> logger)
    {
        _db = db;
        _passwords = passwords;
        _logger = logger;
    }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public string? ErrorMessage { get; private set; }

    public IActionResult OnGet(string? returnUrl = null)
    {
        if (User.IsInRole("Customer"))
        {
            return RedirectToPage("/Account/Index");
        }
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(string? returnUrl = null)
    {
        if (!ModelState.IsValid)
        {
            return Page();
        }

        var email = Input.Email.Trim().ToLowerInvariant();
        var customer = await _db.Customers.FirstOrDefaultAsync(c => c.Email == email);

        // Same generic error whether the email or password is wrong.
        if (customer is null || !_passwords.Verify(Input.Password, customer.PasswordHash))
        {
            _logger.LogWarning("Failed customer login for {Email}.", email);
            ErrorMessage = "Invalid email or password.";
            return Page();
        }

        if (customer.Status == CustomerStatus.Suspended)
        {
            ErrorMessage = "This account is suspended. Please contact support.";
            return Page();
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, customer.Id.ToString()),
            new(ClaimTypes.Name, customer.FullName),
            new(ClaimTypes.Email, customer.Email),
            new(ClaimTypes.Role, "Customer")
        };
        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity));

        _logger.LogInformation("Customer {Email} signed in.", email);

        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
        {
            return LocalRedirect(returnUrl);
        }
        return RedirectToPage("/Account/Index");
    }

    public class InputModel
    {
        [Required, EmailAddress]
        [Display(Name = "Email")]
        public string Email { get; set; } = string.Empty;

        [Required]
        [DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;
    }
}
