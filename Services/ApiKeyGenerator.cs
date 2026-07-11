using System.Security.Cryptography;

namespace apiship.Services;

/// <summary>
/// Generates and hashes API keys. The raw secret is returned only once at
/// creation time; only its hash and a short display prefix are persisted.
/// </summary>
public interface IApiKeyGenerator
{
    /// <summary>Creates a new key. Returns the full secret (show once), a display prefix and the stored hash.</summary>
    (string FullKey, string Prefix, string Hash) Create(string environment = "live");

    /// <summary>Hashes a presented key so it can be compared against stored hashes.</summary>
    string Hash(string fullKey);
}

public class ApiKeyGenerator : IApiKeyGenerator
{
    public (string FullKey, string Prefix, string Hash) Create(string environment = "live")
    {
        // 32 random bytes -> 64 hex chars of entropy.
        var secret = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();
        var fullKey = $"ak_{environment}_{secret}";
        var prefix = $"ak_{environment}_{secret[..6]}";
        return (fullKey, prefix, Hash(fullKey));
    }

    public string Hash(string fullKey)
    {
        var bytes = SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(fullKey));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
