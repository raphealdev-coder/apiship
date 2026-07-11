using System.ComponentModel.DataAnnotations;
using apiship.Data;
using apiship.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace apiship.Pages;

public class GetStartedModel : PageModel
{
    private readonly IWebHostEnvironment _env;
    private readonly AppDbContext _db;
    private readonly IEmailSender _email;
    private readonly IApiKeyGenerator _keys;
    private readonly ILogger<GetStartedModel> _logger;

    // Allowed website bundle formats and max upload size (25 MB).
    private static readonly string[] AllowedExtensions = { ".zip" };
    private const long MaxFileBytes = 25 * 1024 * 1024;

    public GetStartedModel(IWebHostEnvironment env, AppDbContext db, IEmailSender email,
        IApiKeyGenerator keys, ILogger<GetStartedModel> logger)
    {
        _env = env;
        _db = db;
        _email = email;
        _keys = keys;
        _logger = logger;
    }

    // Available plans come from the shared catalog.
    public static IReadOnlyList<PlanOption> Plans => PlanCatalog.Plans;

    [BindProperty]
    public OnboardingInput Input { get; set; } = new();

    [BindProperty]
    public IFormFile? Upload { get; set; }

    // Carried through the review step so the confirm post can finalise.
    [BindProperty]
    public string? PendingDeploymentId { get; set; }

    [BindProperty]
    public string? UploadedFileName { get; set; }

    public Stage CurrentStage { get; private set; } = Stage.Form;
    public string? SiteUrl { get; private set; }

    // The freshly generated API key, shown exactly once on the success screen.
    public string? NewApiKey { get; private set; }

    public enum Stage { Form, Review, Done }

    public PlanOption SelectedPlan =>
        Plans.FirstOrDefault(p => string.Equals(p.Name, Input.Plan, StringComparison.OrdinalIgnoreCase))
        ?? Plans[0];

    public void OnGet(string? plan)
    {
        if (!string.IsNullOrWhiteSpace(plan) &&
            Plans.Any(p => string.Equals(p.Name, plan, StringComparison.OrdinalIgnoreCase)))
        {
            Input.Plan = Plans.First(p => string.Equals(p.Name, plan, StringComparison.OrdinalIgnoreCase)).Name;
        }
        else
        {
            Input.Plan = Plans[0].Name;
        }
    }

    // Step 1 -> validate details + save the upload, then show the review summary.
    public async Task<IActionResult> OnPostReviewAsync()
    {
        // Validate the chosen plan is a known one.
        if (!Plans.Any(p => string.Equals(p.Name, Input.Plan, StringComparison.OrdinalIgnoreCase)))
        {
            ModelState.AddModelError("Input.Plan", "Please choose a valid plan.");
        }

        // Validate the uploaded website bundle.
        if (Upload is null || Upload.Length == 0)
        {
            ModelState.AddModelError("Upload", "Please upload your website files as a .zip archive.");
        }
        else
        {
            var ext = Path.GetExtension(Upload.FileName).ToLowerInvariant();
            if (!AllowedExtensions.Contains(ext))
            {
                ModelState.AddModelError("Upload", "Only .zip archives are allowed.");
            }
            if (Upload.Length > MaxFileBytes)
            {
                ModelState.AddModelError("Upload", "The file exceeds the 25 MB limit.");
            }
        }

        if (!ModelState.IsValid)
        {
            CurrentStage = Stage.Form;
            return Page();
        }

        // Store the upload in a non-web-accessible folder using a safe, generated name.
        PendingDeploymentId = "dep_" + Guid.NewGuid().ToString("N")[..12];
        var uploadRoot = Path.Combine(_env.ContentRootPath, "Uploads", PendingDeploymentId);
        Directory.CreateDirectory(uploadRoot);

        UploadedFileName = Path.GetFileName(Upload!.FileName); // strip any path components
        var destination = Path.Combine(uploadRoot, UploadedFileName);
        await using (var stream = System.IO.File.Create(destination))
        {
            await Upload.CopyToAsync(stream);
        }

        CurrentStage = Stage.Review;
        return Page();
    }

    // Step 2 -> persist to the database, send confirmation email, show success.
    public async Task<IActionResult> OnPostConfirmAsync()
    {
        if (string.IsNullOrWhiteSpace(PendingDeploymentId) ||
            !Plans.Any(p => string.Equals(p.Name, Input.Plan, StringComparison.OrdinalIgnoreCase)))
        {
            CurrentStage = Stage.Form;
            ModelState.AddModelError(string.Empty, "Something went wrong. Please start again.");
            return Page();
        }

        var slug = Slugify(Input.WebsiteName);
        SiteUrl = $"https://{slug}.apiship.app";

        // Upsert the customer by email, then create the project + first API key.
        var email = Input.Email.Trim();
        var customer = await _db.Customers.FirstOrDefaultAsync(c => c.Email == email);
        if (customer is null)
        {
            customer = new Customer { FullName = Input.FullName, Email = email };
            _db.Customers.Add(customer);
        }
        else
        {
            customer.FullName = Input.FullName;
        }

        var (fullKey, prefix, hash) = _keys.Create("live");

        var now = DateTime.UtcNow;
        var trialEnds = now.AddDays(14);
        var project = new Project
        {
            Customer = customer,
            Name = Input.WebsiteName,
            Domain = Input.Domain,
            Plan = SelectedPlan.Name,
            Price = SelectedPlan.Price,
            Notes = Input.Notes,
            ApiType = ApiProductCatalog.Find(Input.ApiType)?.Key ?? "inventory",
            UploadFileName = UploadedFileName ?? string.Empty,
            DeploymentId = PendingDeploymentId,
            SiteUrl = SiteUrl,
            Status = DeploymentStatus.Pending,
            CreatedUtc = now,
            TrialEndsUtc = trialEnds,
            NextBillingUtc = trialEnds
        };
        project.ApiKeys.Add(new ApiKey { Label = "Default", Prefix = prefix, SecretHash = hash });

        _db.Projects.Add(project);
        await _db.SaveChangesAsync();

        NewApiKey = fullKey;

        var body = $@"<h2>Welcome to ApiShip, {System.Net.WebUtility.HtmlEncode(Input.FullName)}!</h2>
<p>Your website <strong>{System.Net.WebUtility.HtmlEncode(Input.WebsiteName)}</strong> is queued for deployment
on the <strong>{SelectedPlan.Name}</strong> plan (${SelectedPlan.Price}/mo).</p>
<ul>
  <li>Deployment ID: {project.DeploymentId}</li>
  <li>Live URL: {SiteUrl}</li>
  <li>API key (store it securely): {fullKey}</li>
</ul>
<p>We'll email you again when your site is live.</p>";

        await _email.SendAsync(Input.Email, "Your ApiShip deployment is starting", body);

        _logger.LogInformation("Project {DeploymentId} created for customer {Email} on {Plan}.",
            project.DeploymentId, email, SelectedPlan.Name);

        CurrentStage = Stage.Done;
        return Page();
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
        return string.IsNullOrEmpty(slug) ? "my-site" : slug;
    }
}

public class OnboardingInput
{
    [Required, StringLength(80, MinimumLength = 2)]
    [Display(Name = "Full name")]
    public string FullName { get; set; } = string.Empty;

    [Required, EmailAddress]
    [Display(Name = "Work email")]
    public string Email { get; set; } = string.Empty;

    [Required, StringLength(60, MinimumLength = 2)]
    [Display(Name = "Website name")]
    public string WebsiteName { get; set; } = string.Empty;

    [Url]
    [Display(Name = "Existing domain (optional)")]
    public string? Domain { get; set; }

    [Required]
    public string Plan { get; set; } = "Starter";

    [Required]
    [Display(Name = "API type")]
    public string ApiType { get; set; } = "inventory";

    [StringLength(500)]
    public string? Notes { get; set; }
}
