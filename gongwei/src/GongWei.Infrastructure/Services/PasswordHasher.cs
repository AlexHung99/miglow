using System.Security.Cryptography;
using System.Text;
using GongWei.Application.Abstractions;

namespace GongWei.Infrastructure.Services;

/// <summary>
/// PBKDF2-HMAC-SHA256 password hashing.
///
/// PBKDF2 rather than Argon2id, which would be the stronger choice, because Argon2 needs a
/// third-party package and this is the one credential path in a system that otherwise has
/// none. The iteration count follows the current OWASP guidance for PBKDF2-SHA256, and the
/// parameters are stored alongside each hash so they can be raised later without
/// invalidating existing passwords.
/// </summary>
public sealed class Pbkdf2PasswordHasher : IPasswordHasher
{
    private const string Algorithm = "pbkdf2-sha256";

    /// <summary>OWASP's recommendation for PBKDF2-HMAC-SHA256. Costs a few hundred ms.</summary>
    private const int DefaultIterations = 600_000;

    private const int SaltBytes = 16;
    private const int HashBytes = 32;

    public string Hash(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltBytes);
        var hash = Derive(password, salt, DefaultIterations);

        return string.Join('$',
            Algorithm,
            DefaultIterations.ToString(),
            Convert.ToBase64String(salt),
            Convert.ToBase64String(hash));
    }

    public PasswordVerification Verify(string password, string encoded)
    {
        var parts = encoded.Split('$');

        if (parts.Length != 4
            || parts[0] != Algorithm
            || !int.TryParse(parts[1], out var iterations)
            || iterations <= 0)
        {
            // A malformed stored hash must fail closed rather than throw: a corrupted row
            // should lock one account out, not surface a 500 that confirms it exists.
            return PasswordVerification.Failed;
        }

        byte[] salt;
        byte[] expected;

        try
        {
            salt = Convert.FromBase64String(parts[2]);
            expected = Convert.FromBase64String(parts[3]);
        }
        catch (FormatException)
        {
            return PasswordVerification.Failed;
        }

        var actual = Derive(password, salt, iterations, expected.Length);

        // Constant time: a byte-by-byte comparison leaks how much of the hash matched.
        if (!CryptographicOperations.FixedTimeEquals(actual, expected))
        {
            return PasswordVerification.Failed;
        }

        return iterations < DefaultIterations
            ? PasswordVerification.SucceededNeedsRehash
            : PasswordVerification.Succeeded;
    }

    private static byte[] Derive(string password, byte[] salt, int iterations, int length = HashBytes) =>
        Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(password),
            salt,
            iterations,
            HashAlgorithmName.SHA256,
            length);
}
