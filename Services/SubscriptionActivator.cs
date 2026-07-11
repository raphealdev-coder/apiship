using apiship.Data;
using Microsoft.EntityFrameworkCore;

namespace apiship.Services;

/// <summary>
/// Marks a payment as paid and activates the customer's stores for the chosen
/// billing cycle. Idempotent — safe to call from both the webhook and the
/// browser callback for the same reference.
/// </summary>
public static class SubscriptionActivator
{
    public static async Task<bool> ActivateAsync(AppDbContext db, string reference)
    {
        var payment = await db.Payments
            .Include(p => p.Customer!)
                .ThenInclude(c => c.Projects)
            .FirstOrDefaultAsync(p => p.Reference == reference);

        if (payment is null)
        {
            return false;
        }

        if (payment.Status == PaymentStatus.Paid)
        {
            return true; // already processed
        }

        payment.Status = PaymentStatus.Paid;
        payment.PaidUtc = DateTime.UtcNow;

        var now = DateTime.UtcNow;
        var nextBilling = payment.Cycle == BillingCycle.Yearly ? now.AddYears(1) : now.AddMonths(1);

        foreach (var project in payment.Customer!.Projects)
        {
            project.IsSubscribed = true;
            project.SubscribedUtc = now;
            project.BillingCycle = payment.Cycle;
            project.NextBillingUtc = nextBilling;
        }

        await db.SaveChangesAsync();
        return true;
    }
}
