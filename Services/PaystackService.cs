using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace apiship.Services;

/// <summary>
/// Thin client for Paystack's transaction API. Uses the merchant secret key to
/// initialize a transaction (returns a hosted checkout URL) and to verify one.
/// </summary>
public interface IPaystackService
{
    /// <summary>Initializes a transaction. Returns (authorizationUrl, reference) or null on failure.</summary>
    Task<(string AuthorizationUrl, string Reference)?> InitializeAsync(
        string secretKey, string email, long amountKobo, string reference, string callbackUrl);

    /// <summary>Verifies a transaction reference; true when Paystack reports it as successful.</summary>
    Task<bool> VerifyAsync(string secretKey, string reference);

    /// <summary>Validates a webhook body against the x-paystack-signature header (HMAC-SHA512).</summary>
    bool VerifySignature(string secretKey, string rawBody, string? signature);
}

public class PaystackService : IPaystackService
{
    private readonly HttpClient _http;
    private readonly ILogger<PaystackService> _logger;

    public PaystackService(HttpClient http, ILogger<PaystackService> logger)
    {
        _http = http;
        _logger = logger;
    }

    public async Task<(string AuthorizationUrl, string Reference)?> InitializeAsync(
        string secretKey, string email, long amountKobo, string reference, string callbackUrl)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.paystack.co/transaction/initialize");
            request.Headers.Authorization = new("Bearer", secretKey);
            var payload = JsonSerializer.Serialize(new
            {
                email,
                amount = amountKobo,
                reference,
                callback_url = callbackUrl,
                currency = "NGN"
            });
            request.Content = new StringContent(payload, Encoding.UTF8, "application/json");

            using var response = await _http.SendAsync(request);
            var json = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Paystack initialize failed ({Status}): {Body}", response.StatusCode, json);
                return null;
            }

            using var doc = JsonDocument.Parse(json);
            var data = doc.RootElement.GetProperty("data");
            var url = data.GetProperty("authorization_url").GetString();
            var refCode = data.GetProperty("reference").GetString();
            return url is null || refCode is null ? null : (url, refCode);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Paystack initialize threw.");
            return null;
        }
    }

    public async Task<bool> VerifyAsync(string secretKey, string reference)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get,
                $"https://api.paystack.co/transaction/verify/{Uri.EscapeDataString(reference)}");
            request.Headers.Authorization = new("Bearer", secretKey);

            using var response = await _http.SendAsync(request);
            var json = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Paystack verify failed ({Status}): {Body}", response.StatusCode, json);
                return false;
            }

            using var doc = JsonDocument.Parse(json);
            var status = doc.RootElement.GetProperty("data").GetProperty("status").GetString();
            return string.Equals(status, "success", StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Paystack verify threw.");
            return false;
        }
    }

    public bool VerifySignature(string secretKey, string rawBody, string? signature)
    {
        if (string.IsNullOrEmpty(signature))
        {
            return false;
        }

        using var hmac = new HMACSHA512(Encoding.UTF8.GetBytes(secretKey));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(rawBody));
        var computed = Convert.ToHexString(hash).ToLowerInvariant();
        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(computed),
            Encoding.UTF8.GetBytes(signature.ToLowerInvariant()));
    }
}
