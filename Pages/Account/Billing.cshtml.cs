using System.Security.Claims;
using apiship.Data;
using apiship.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace apiship.Pages.Account;

public class BillingModel : PageModel
{
    private readonly AppDbContext _db;
    private readonly IAppSettingsService _settings;
    private readonly IPaystackService _paystack;
    private readonly ILogger<BillingModel> _logger;

    public BillingModel(AppDbContext db, IAppSettingsService settings,
        IPaystackService paystack, ILogger<BillingModel> logger)
    {
        _db = db;
        _settings = settings;
        _paystack = paystack;
        _logger = logger;
    }

    public Customer Customer { get; private set; } = default!;
    public IReadOnlyList<Project> Projects { get; private set; } = Array.Empty<Project>();
    public AppSetting Settings { get; private set; } = new();

    public int StoreCount => Projects.Count;
    public int PriceMonthlyUsd => Settings.PriceMonthlyUsd;
    public int PriceYearlyUsd => Settings.PriceYearlyUsd;
    public int MonthlyTotalUsd => StoreCount * PriceMonthlyUsd;
    public int YearlyTotalUsd => StoreCount * PriceYearlyUsd;
    public decimal Rate => Settings.EffectiveRate;

    public bool AllSubscribed => Projects.Count > 0 && Projects.All(p => p.IsSubscribed);
    public DateTime? TrialEnds => Projects.Where(p => p.IsInTrial).Select(p => (DateTime?)p.TrialEndsUtc).Min();

    [TempData]
    public string? StatusMessage { get; set; }

    public long NgnFor(int usd) => (long)Math.Round(usd * Rate, MidpointRounding.AwayFromZero);

    private int CurrentCustomerId =>
        int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;

    public async Task<IActionResult> OnGetAsync()
    {
        if (!await LoadAsync())
        {
            return RedirectToPage("/Account/Login");
        }
        return Page();
    }

    public async Task<IActionResult> OnPostSubscribeAsync(string cycle)
    {
        if (!await LoadAsync())
        {
            return RedirectToPage("/Account/Login");
        }

        if (!Settings.HasPaystackKey)
        {
            StatusMessage = "Payments are not configured yet. Please contact support.";
            return RedirectToPage();
        }

        if (StoreCount == 0)
        {
            StatusMessage = "Add a store before subscribing.";
            return RedirectToPage();
        }

        var billingCycle = string.Equals(cycle, "yearly", StringComparison.OrdinalIgnoreCase)
            ? BillingCycle.Yearly
            : BillingCycle.Monthly;

        var amountUsd = billingCycle == BillingCycle.Yearly ? YearlyTotalUsd : MonthlyTotalUsd;
        var amountKobo = NgnFor(amountUsd) * 100;
        var reference = "ps_" + Guid.NewGuid().ToString("N")[..18];

        var payment = new Payment
        {
            Reference = reference,
            CustomerId = Customer.Id,
            Cycle = billingCycle,
            StoreCount = StoreCount,
            AmountUsd = amountUsd,
            AmountKobo = amountKobo,
            Rate = Rate
        };
        _db.Payments.Add(payment);
        await _db.SaveChangesAsync();

        var callbackUrl = Url.Page("/Account/Billing", pageHandler: "Callback",
            values: new { reference }, protocol: Request.Scheme)!;
        var email = Customer.Billing?.BillingEmail ?? Customer.Email;

        var init = await _paystack.InitializeAsync(Settings.PaystackSecretKey!, email, amountKobo, reference, callbackUrl);
        if (init is null)
        {
            payment.Status = PaymentStatus.Failed;
            await _db.SaveChangesAsync();
            StatusMessage = "We couldn't start the payment. Check the Paystack key or try again.";
            return RedirectToPage();
        }

        _logger.LogInformation("Payment {Reference} initialized for customer {Email}.", reference, email);
        return Redirect(init.Value.AuthorizationUrl);
    }

    public async Task<IActionResult> OnGetCallbackAsync(string reference)
    {
        var settings = await _settings.GetAsync();
        if (settings.HasPaystackKey && await _paystack.VerifyAsync(settings.PaystackSecretKey!, reference))
        {
            await SubscriptionActivator.ActivateAsync(_db, reference);
            StatusMessage = "Payment successful — your subscription is now active.";
        }
        else
        {
            StatusMessage = "Payment was not completed. You can try again below.";
        }
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostLogoutAsync()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToPage("/Index");
    }

    private async Task<bool> LoadAsync()
    {
        var customer = await _db.Customers
            .Include(c => c.Billing)
            .Include(c => c.Projects.OrderByDescending(p => p.CreatedUtc))
                .ThenInclude(p => p.ApiKeys)
            .FirstOrDefaultAsync(c => c.Id == CurrentCustomerId);

        if (customer is null)
        {
            return false;
        }

        Customer = customer;
        Projects = customer.Projects;
        Settings = await _settings.GetAsync();
        return true;
    }
}
