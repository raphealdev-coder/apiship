using apiship.Data;
using apiship.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.ComponentModel.DataAnnotations;

namespace apiship.Pages.Admin;

public class SettingsModel : PageModel
{
    private readonly IAppSettingsService _settings;
    private readonly ILogger<SettingsModel> _logger;

    public SettingsModel(IAppSettingsService settings, ILogger<SettingsModel> logger)
    {
        _settings = settings;
        _logger = logger;
    }

    [BindProperty]
    public PaymentInput Payment { get; set; } = new();

    [BindProperty]
    public ReminderInput Reminder { get; set; } = new();

    public bool HasPaystackKey { get; private set; }

    [TempData]
    public string? StatusMessage { get; set; }

    public async Task OnGetAsync()
    {
        var s = await _settings.GetAsync();
        HasPaystackKey = s.HasPaystackKey;
        Payment = new PaymentInput
        {
            PriceMonthlyUsd = s.PriceMonthlyUsd,
            PriceYearlyUsd = s.PriceYearlyUsd,
            UsdToNgnRate = s.UsdToNgnRate
        };
        Reminder = new ReminderInput
        {
            Enabled = s.ReminderEnabled,
            ExpiryUtc = s.ReminderExpiryUtc,
            Message = s.ReminderMessage
        };
    }

    public async Task<IActionResult> OnPostPaymentAsync()
    {
        var s = await _settings.GetAsync();

        // A blank key field means "keep the existing key".
        if (!string.IsNullOrWhiteSpace(Payment.PaystackSecretKey))
        {
            s.PaystackSecretKey = Payment.PaystackSecretKey.Trim();
        }
        s.PriceMonthlyUsd = Math.Max(0, Payment.PriceMonthlyUsd);
        s.PriceYearlyUsd = Math.Max(0, Payment.PriceYearlyUsd);
        s.UsdToNgnRate = Payment.UsdToNgnRate is > 0 ? Payment.UsdToNgnRate : null;

        await _settings.SaveAsync(s);
        _logger.LogInformation("Payment settings updated by {User}.", User.Identity?.Name);
        StatusMessage = "Payment settings saved.";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostReminderAsync()
    {
        var s = await _settings.GetAsync();
        s.ReminderEnabled = Reminder.Enabled;
        s.ReminderExpiryUtc = Reminder.ExpiryUtc;
        s.ReminderMessage = string.IsNullOrWhiteSpace(Reminder.Message) ? null : Reminder.Message.Trim();

        await _settings.SaveAsync(s);
        StatusMessage = "Reminder banner saved.";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostLogoutAsync()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToPage("/Admin/Login");
    }

    public class PaymentInput
    {
        public string? PaystackSecretKey { get; set; }
        public int PriceMonthlyUsd { get; set; } = 50;
        public int PriceYearlyUsd { get; set; } = 550;
        public decimal? UsdToNgnRate { get; set; }
    }

    public class ReminderInput
    {
        public bool Enabled { get; set; }

        [DataType(DataType.Date)]
        public DateTime? ExpiryUtc { get; set; }

        public string? Message { get; set; }
    }
}
