namespace apiship.Data;

public enum BillingCycle
{
    Monthly = 0,
    Yearly = 1
}

public enum PaymentStatus
{
    Pending = 0,
    Paid = 1,
    Failed = 2
}

/// <summary>
/// A subscription payment attempt processed through Paystack. One payment
/// covers all of a customer's stores for the chosen billing cycle.
/// </summary>
public class Payment
{
    public int Id { get; set; }

    // Unique Paystack transaction reference.
    public string Reference { get; set; } = string.Empty;

    public int CustomerId { get; set; }
    public Customer? Customer { get; set; }

    public BillingCycle Cycle { get; set; }
    public int StoreCount { get; set; }

    public int AmountUsd { get; set; }
    public long AmountKobo { get; set; }   // NGN charged, in kobo
    public decimal Rate { get; set; }

    public PaymentStatus Status { get; set; } = PaymentStatus.Pending;
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    public DateTime? PaidUtc { get; set; }
}
