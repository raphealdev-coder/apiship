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

public class ProfileModel : PageModel
{
    private readonly AppDbContext _db;
    private readonly IPasswordService _passwords;

    public ProfileModel(AppDbContext db, IPasswordService passwords)
    {
        _db = db;
        _passwords = passwords;
    }

    [BindProperty]
    public DetailsInput Details { get; set; } = new();

    [BindProperty]
    public PasswordInput Password { get; set; } = new();

    [BindProperty]
    public BillingInput Billing { get; set; } = new();

    [TempData]
    public string? StatusMessage { get; set; }

    public string? PasswordError { get; private set; }

    private int CurrentCustomerId =>
        int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;

    public async Task<IActionResult> OnGetAsync()
    {
        var customer = await _db.Customers
            .Include(c => c.Billing)
            .FirstOrDefaultAsync(c => c.Id == CurrentCustomerId);
        if (customer is null)
        {
            return RedirectToPage("/Account/Login");
        }

        Details = new DetailsInput
        {
            FullName = customer.FullName,
            Email = customer.Email,
            Company = customer.Company
        };
        Billing = ToBillingInput(customer.Billing);
        return Page();
    }

    public async Task<IActionResult> OnPostBillingAsync()
    {
        var customer = await _db.Customers
            .Include(c => c.Billing)
            .FirstOrDefaultAsync(c => c.Id == CurrentCustomerId);
        if (customer is null)
        {
            return RedirectToPage("/Account/Login");
        }

        ModelState.Remove("Details.FullName");
        ModelState.Remove("Details.Email");
        ModelState.Remove("Details.Company");
        ModelState.Remove("Password.CurrentPassword");
        ModelState.Remove("Password.NewPassword");
        ModelState.Remove("Password.ConfirmPassword");
        if (!ModelState.IsValid)
        {
            // Repopulate the other tabs so the page renders correctly.
            Details = new DetailsInput { FullName = customer.FullName, Email = customer.Email, Company = customer.Company };
            return Page();
        }

        var profile = customer.Billing ??= new BillingProfile { CustomerId = customer.Id };
        profile.BillingName = Trim(Billing.BillingName);
        profile.BillingEmail = Trim(Billing.BillingEmail);
        profile.AddressLine1 = Trim(Billing.AddressLine1);
        profile.AddressLine2 = Trim(Billing.AddressLine2);
        profile.City = Trim(Billing.City);
        profile.State = Trim(Billing.State);
        profile.PostalCode = Trim(Billing.PostalCode);
        profile.Country = Trim(Billing.Country);
        profile.CardBrand = Trim(Billing.CardBrand);
        profile.CardLast4 = Trim(Billing.CardLast4);
        profile.UpdatedUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        StatusMessage = "Your billing details have been saved.";
        return RedirectToPage();
    }

    private static string? Trim(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static BillingInput ToBillingInput(BillingProfile? b) => new()
    {
        BillingName = b?.BillingName,
        BillingEmail = b?.BillingEmail,
        AddressLine1 = b?.AddressLine1,
        AddressLine2 = b?.AddressLine2,
        City = b?.City,
        State = b?.State,
        PostalCode = b?.PostalCode,
        Country = b?.Country,
        CardBrand = b?.CardBrand,
        CardLast4 = b?.CardLast4
    };

    public async Task<IActionResult> OnPostDetailsAsync()
    {
        var customer = await _db.Customers.FindAsync(CurrentCustomerId);
        if (customer is null)
        {
            return RedirectToPage("/Account/Login");
        }

        ModelState.Remove("Password.CurrentPassword");
        ModelState.Remove("Password.NewPassword");
        ModelState.Remove("Password.ConfirmPassword");
        RemoveKeys("Billing.");
        if (!ModelState.IsValid)
        {
            return Page();
        }

        var newEmail = Details.Email.Trim().ToLowerInvariant();
        if (newEmail != customer.Email &&
            await _db.Customers.AnyAsync(c => c.Email == newEmail && c.Id != customer.Id))
        {
            ModelState.AddModelError("Details.Email", "That email is already in use.");
            return Page();
        }

        customer.FullName = Details.FullName.Trim();
        customer.Email = newEmail;
        customer.Company = string.IsNullOrWhiteSpace(Details.Company) ? null : Details.Company.Trim();
        await _db.SaveChangesAsync();

        // Refresh the signed-in identity so the new name/email appear immediately.
        await RefreshSignInAsync(customer);

        StatusMessage = "Your details have been updated.";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostPasswordAsync()
    {
        var customer = await _db.Customers.FindAsync(CurrentCustomerId);
        if (customer is null)
        {
            return RedirectToPage("/Account/Login");
        }

        ModelState.Remove("Details.FullName");
        ModelState.Remove("Details.Email");
        ModelState.Remove("Details.Company");
        RemoveKeys("Billing.");
        if (!ModelState.IsValid)
        {
            return Page();
        }

        if (!_passwords.Verify(Password.CurrentPassword, customer.PasswordHash))
        {
            PasswordError = "Your current password is incorrect.";
            return Page();
        }

        customer.PasswordHash = _passwords.Hash(Password.NewPassword);
        await _db.SaveChangesAsync();

        StatusMessage = "Your password has been changed.";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostLogoutAsync()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToPage("/Index");
    }

    private async Task RefreshSignInAsync(Customer customer)
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

    private void RemoveKeys(string prefix)
    {
        foreach (var key in ModelState.Keys.Where(k => k.StartsWith(prefix, StringComparison.Ordinal)).ToList())
        {
            ModelState.Remove(key);
        }
    }

    public class DetailsInput
    {
        [Required, StringLength(80, MinimumLength = 2)]
        [Display(Name = "Full name")]
        public string FullName { get; set; } = string.Empty;

        [Required, EmailAddress]
        [Display(Name = "Email")]
        public string Email { get; set; } = string.Empty;

        [StringLength(100)]
        [Display(Name = "Company (optional)")]
        public string? Company { get; set; }
    }

    public class PasswordInput
    {
        [Required]
        [DataType(DataType.Password)]
        [Display(Name = "Current password")]
        public string CurrentPassword { get; set; } = string.Empty;

        [Required, StringLength(100, MinimumLength = 8)]
        [DataType(DataType.Password)]
        [Display(Name = "New password")]
        public string NewPassword { get; set; } = string.Empty;

        [Required]
        [DataType(DataType.Password)]
        [Compare(nameof(NewPassword), ErrorMessage = "Passwords do not match.")]
        [Display(Name = "Confirm new password")]
        public string ConfirmPassword { get; set; } = string.Empty;
    }

    public class BillingInput
    {
        [StringLength(120)]
        [Display(Name = "Billing name")]
        public string? BillingName { get; set; }

        [EmailAddress, StringLength(200)]
        [Display(Name = "Billing email")]
        public string? BillingEmail { get; set; }

        [StringLength(200)]
        [Display(Name = "Address line 1")]
        public string? AddressLine1 { get; set; }

        [StringLength(200)]
        [Display(Name = "Address line 2")]
        public string? AddressLine2 { get; set; }

        [StringLength(100)]
        [Display(Name = "City")]
        public string? City { get; set; }

        [StringLength(100)]
        [Display(Name = "State / region")]
        public string? State { get; set; }

        [StringLength(20)]
        [Display(Name = "Postal code")]
        public string? PostalCode { get; set; }

        [StringLength(100)]
        [Display(Name = "Country")]
        public string? Country { get; set; }

        [StringLength(30)]
        [Display(Name = "Card brand")]
        public string? CardBrand { get; set; }

        [StringLength(4, MinimumLength = 4)]
        [RegularExpression(@"\d{4}", ErrorMessage = "Enter the last 4 digits.")]
        [Display(Name = "Card ending")]
        public string? CardLast4 { get; set; }
    }
}
