namespace apiship.Data;

/// <summary>
/// A customer's billing details (address + payment method on file).
/// For this demo no real card data is stored — only a brand and last-4 label.
/// </summary>
public class BillingProfile
{
    public int Id { get; set; }

    public int CustomerId { get; set; }
    public Customer? Customer { get; set; }

    public string? BillingName { get; set; }
    public string? BillingEmail { get; set; }
    public string? AddressLine1 { get; set; }
    public string? AddressLine2 { get; set; }
    public string? City { get; set; }
    public string? State { get; set; }
    public string? PostalCode { get; set; }
    public string? Country { get; set; }

    // Display-only payment method reference (never a full card number).
    public string? CardBrand { get; set; }
    public string? CardLast4 { get; set; }

    public DateTime? UpdatedUtc { get; set; }

    public bool HasAddress =>
        !string.IsNullOrWhiteSpace(AddressLine1) &&
        !string.IsNullOrWhiteSpace(City) &&
        !string.IsNullOrWhiteSpace(Country);

    public bool HasPaymentMethod => !string.IsNullOrWhiteSpace(CardLast4);

    public bool IsComplete => HasAddress && HasPaymentMethod;
}
