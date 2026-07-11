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

public class AddStoreModel : PageModel
{
    private readonly AppDbContext _db;
    private readonly IApiKeyGenerator _keys;
    private readonly IAppSettingsService _settings;
    private readonly ILogger<AddStoreModel> _logger;

    public AddStoreModel(AppDbContext db, IApiKeyGenerator keys, IAppSettingsService settings,
        ILogger<AddStoreModel> logger)
    {
        _db = db;
        _keys = keys;
        _settings = settings;
        _logger = logger;
    }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public string? NewApiKey { get; private set; }

    [TempData]
    public string? StatusMessage { get; set; }

    private int CurrentCustomerId =>
        int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;

    public void OnGet()
    {
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var customer = await _db.Customers.FindAsync(CurrentCustomerId);
        if (customer is null)
        {
            return RedirectToPage("/Account/Login");
        }

        if (!ModelState.IsValid)
        {
            return Page();
        }

        var settings = await _settings.GetAsync();
        var now = DateTime.UtcNow;
        var trialEnds = now.AddDays(14);
        var slug = Slugify(Input.Name);

        var (fullKey, prefix, hash) = _keys.Create("live");

        var project = new Project
        {
            CustomerId = customer.Id,
            Name = Input.Name.Trim(),
            Location = string.IsNullOrWhiteSpace(Input.Location) ? null : Input.Location.Trim(),
            ApiType = ApiProductCatalog.Find(Input.ApiType)?.Key ?? "inventory",
            Plan = "Starter",
            Price = settings.PriceMonthlyUsd,
            DeploymentId = "dep_" + Guid.NewGuid().ToString("N")[..12],
            SiteUrl = $"https://{slug}.apiship.app",
            UploadFileName = string.Empty,
            Status = DeploymentStatus.Deployed,
            CreatedUtc = now,
            TrialEndsUtc = trialEnds,
            NextBillingUtc = trialEnds,
            BillingCycle = BillingCycle.Monthly
        };
        project.ApiKeys.Add(new ApiKey { Label = "Default", Prefix = prefix, SecretHash = hash });

        _db.Projects.Add(project);
        await _db.SaveChangesAsync();

        _logger.LogInformation("Store {Name} added by customer {Id}.", project.Name, customer.Id);
        NewApiKey = fullKey;
        StatusMessage = $"Store “{project.Name}” added.";
        return RedirectToPage("/Account/Billing");
    }

    public async Task<IActionResult> OnPostLogoutAsync()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToPage("/Index");
    }

    private static string Slugify(string value)
    {
        var chars = value.Trim().ToLowerInvariant()
            .Select(c => char.IsLetterOrDigit(c) ? c : '-')
            .ToArray();
        var slug = new string(chars).Trim('-');
        while (slug.Contains("--"))
        {
            slug = slug.Replace("--", "-");
        }
        return string.IsNullOrEmpty(slug) ? "store" : slug;
    }

    public class InputModel
    {
        [Required, StringLength(60, MinimumLength = 2)]
        [Display(Name = "Store name")]
        public string Name { get; set; } = string.Empty;

        [StringLength(80)]
        [Display(Name = "Location")]
        public string? Location { get; set; }

        [Required]
        [Display(Name = "API type")]
        public string ApiType { get; set; } = "inventory";
    }
}
