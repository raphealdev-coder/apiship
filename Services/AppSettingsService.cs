using apiship.Data;
using Microsoft.EntityFrameworkCore;

namespace apiship.Services;

/// <summary>Loads and persists the single global <see cref="AppSetting"/> row.</summary>
public interface IAppSettingsService
{
    Task<AppSetting> GetAsync();
    Task SaveAsync(AppSetting updated);
}

public class AppSettingsService : IAppSettingsService
{
    private readonly AppDbContext _db;

    public AppSettingsService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<AppSetting> GetAsync()
    {
        var settings = await _db.AppSettings.FirstOrDefaultAsync(s => s.Id == 1);
        if (settings is null)
        {
            settings = new AppSetting { Id = 1 };
            _db.AppSettings.Add(settings);
            await _db.SaveChangesAsync();
        }
        return settings;
    }

    public async Task SaveAsync(AppSetting updated)
    {
        var settings = await GetAsync();
        settings.PaystackSecretKey = updated.PaystackSecretKey;
        settings.PriceMonthlyUsd = updated.PriceMonthlyUsd;
        settings.PriceYearlyUsd = updated.PriceYearlyUsd;
        settings.UsdToNgnRate = updated.UsdToNgnRate;
        settings.ReminderEnabled = updated.ReminderEnabled;
        settings.ReminderExpiryUtc = updated.ReminderExpiryUtc;
        settings.ReminderMessage = updated.ReminderMessage;
        settings.UpdatedUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync();
    }
}
