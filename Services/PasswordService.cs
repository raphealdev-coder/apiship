using System.Security.Cryptography;

namespace apiship.Services;

/// <summary>
/// Salted PBKDF2 (SHA-256) password hashing. Stored format is
/// "iterations.saltBase64.hashBase64" so parameters travel with the hash.
/// </summary>
public interface IPasswordService
{
    string Hash(string password);
    bool Verify(string password, string? stored);
}

public class PasswordService : IPasswordService
{
    private const int Iterations = 100_000;
    private const int SaltSize = 16;   // 128-bit
    private const int KeySize = 32;    // 256-bit

    public string Hash(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithmName.SHA256, KeySize);
        return $"{Iterations}.{Convert.ToBase64String(salt)}.{Convert.ToBase64String(hash)}";
    }

    public bool Verify(string password, string? stored)
    {
        if (string.IsNullOrEmpty(stored))
        {
            return false;
        }

        var parts = stored.Split('.', 3);
        if (parts.Length != 3 || !int.TryParse(parts[0], out var iterations))
        {
            return false;
        }

        var salt = Convert.FromBase64String(parts[1]);
        var expected = Convert.FromBase64String(parts[2]);
        var actual = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA256, expected.Length);
        return CryptographicOperations.FixedTimeEquals(actual, expected);
    }
}
