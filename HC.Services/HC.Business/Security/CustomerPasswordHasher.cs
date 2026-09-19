using System.Security.Cryptography;

namespace HC.Business.Security;

/// <summary>
/// PBKDF2 (SHA-256) password hashing for storefront customers.
/// Stored format: <c>pbkdf2$sha256$&lt;iterations&gt;$&lt;saltBase64&gt;$&lt;hashBase64&gt;</c> (~90 chars).
/// </summary>
public static class CustomerPasswordHasher
{
    private const int SaltSize = 16;
    private const int HashSize = 32;
    private const int Iterations = 100_000;
    private const string Prefix = "pbkdf2$sha256$";
    private const char Separator = '$';

    /// <summary>True when the stored value is already a PBKDF2 hash (as opposed to a legacy plain-text password).</summary>
    public static bool IsHashed(string? value)
        => !string.IsNullOrEmpty(value) && value.StartsWith(Prefix, StringComparison.Ordinal);

    public static string Hash(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithmName.SHA256, HashSize);
        return $"{Prefix}{Iterations}{Separator}{Convert.ToBase64String(salt)}{Separator}{Convert.ToBase64String(hash)}";
    }

    /// <summary>
    /// Verifies a password against the stored value.
    /// Legacy rows hold the password in clear text - if such a value matches, <paramref name="needsUpgrade"/>
    /// is set so the caller can replace it with a real hash (hash-on-first-successful-login).
    /// </summary>
    public static bool Verify(string? stored, string password, out bool needsUpgrade)
    {
        needsUpgrade = false;

        if (string.IsNullOrEmpty(stored))
            return false;

        if (!IsHashed(stored))
        {
            if (!string.Equals(stored, password, StringComparison.Ordinal))
                return false;

            needsUpgrade = true;
            return true;
        }

        var parts = stored.Split(Separator);
        if (parts.Length != 5)
            return false;

        if (!int.TryParse(parts[2], out var iterations) || iterations <= 0)
            return false;

        byte[] salt;
        byte[] expected;
        try
        {
            salt = Convert.FromBase64String(parts[3]);
            expected = Convert.FromBase64String(parts[4]);
        }
        catch (FormatException)
        {
            return false;
        }

        var actual = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA256, expected.Length);
        return CryptographicOperations.FixedTimeEquals(actual, expected);
    }
}
