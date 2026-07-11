namespace apiship.Data;

/// <summary>
/// Global application settings, stored as a single row (Id = 1). Editable from
/// the admin backend: payment configuration and the staff reminder banner.
/// </summary>
public class AppSetting
{
    public int Id { get; set; } = 1;

    // --- Payment settings ---
    // Paystack secret key that receives subscription payments. Stored as-is for
    // this demo; in production keep it in a secret store / encrypted at rest.
    public string? PaystackSecretKey { get; set; }
    public int PriceMonthlyUsd { get; set; } = 50;
    public int PriceYearlyUsd { get; set; } = 550;

    // Null => use the built-in fallback rate.
    public decimal? UsdToNgnRate { get; set; }

    // --- Backend reminder banner ---
    public bool ReminderEnabled { get; set; }
    public DateTime? ReminderExpiryUtc { get; set; }
    public string? ReminderMessage { get; set; }

    public DateTime? UpdatedUtc { get; set; }

    public const decimal FallbackRate = 1380m;

    public decimal EffectiveRate => UsdToNgnRate is > 0 ? UsdToNgnRate.Value : FallbackRate;
    public bool HasPaystackKey => !string.IsNullOrWhiteSpace(PaystackSecretKey);
}
