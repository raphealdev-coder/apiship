using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace apiship.Pages.Admin;

public class LoginModel : PageModel
{
    private readonly IConfiguration _config;
    private readonly ILogger<LoginModel> _logger;

    public LoginModel(IConfiguration config, ILogger<LoginModel> logger)
    {
        _config = config;
        _logger = logger;
    }

    [BindProperty]
    public Credentials Input { get; set; } = new();

    public string? ErrorMessage { get; private set; }

    public IActionResult OnGet()
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToPage("/Admin/Index");
        }
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            return Page();
        }

        // Credentials come from configuration / environment, never hard-coded.
        var expectedUser = _config["Admin:Username"] ?? "admin";
        var expectedPassword = _config["Admin:Password"];

        var userOk = FixedEquals(Input.Username, expectedUser);
        var passOk = !string.IsNullOrEmpty(expectedPassword) && FixedEquals(Input.Password, expectedPassword);

        if (!userOk || !passOk)
        {
            _logger.LogWarning("Failed admin login attempt for user '{User}'.", Input.Username);
            ErrorMessage = "Invalid username or password.";
            return Page();
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.Name, expectedUser),
            new(ClaimTypes.Role, "Admin")
        };
        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(identity));

        _logger.LogInformation("Admin '{User}' signed in.", expectedUser);
        return RedirectToPage("/Admin/Index");
    }

    // Constant-time comparison to avoid leaking timing information.
    private static bool FixedEquals(string a, string b)
    {
        var ba = Encoding.UTF8.GetBytes(a);
        var bb = Encoding.UTF8.GetBytes(b);
        return CryptographicOperations.FixedTimeEquals(
            SHA256.HashData(ba), SHA256.HashData(bb));
    }
}

public class Credentials
{
    [Required]
    public string Username { get; set; } = string.Empty;

    [Required]
    [DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;
}
