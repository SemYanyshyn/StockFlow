using System.Globalization;
using System.Security.Cryptography;

namespace StockFlow.BusinessLogic.Users;

public sealed class Pbkdf2PasswordHasher : IPasswordHasher
{
    public const int Iterations = 600_000;
    public const int SaltBytes = 16;
    public const int HashBytes = 32;
    private const string Algorithm = "pbkdf2-sha256";
    private const string Version = "v1";
    // Bounds prevent corrupt hashes from causing unbounded CPU or memory use.
    private const int MaxVerificationIterations = 2_000_000;

    public string Hash(string password)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(password);
        var salt = RandomNumberGenerator.GetBytes(SaltBytes);
        var hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithmName.SHA256, HashBytes);
        return $"{Algorithm}${Version}${Iterations.ToString(CultureInfo.InvariantCulture)}${Convert.ToBase64String(salt)}${Convert.ToBase64String(hash)}";
    }

    public bool Verify(string password, string encodedHash)
    {
        if (string.IsNullOrEmpty(password) || string.IsNullOrEmpty(encodedHash) || encodedHash.Length > 255) return false;
        var parts = encodedHash.Split('$');
        if (parts.Length != 5 || parts[0] != Algorithm || parts[1] != Version ||
            !int.TryParse(parts[2], NumberStyles.None, CultureInfo.InvariantCulture, out var iterations) ||
            iterations < 10_000 || iterations > MaxVerificationIterations) return false;
        try
        {
            var salt = Convert.FromBase64String(parts[3]);
            var expected = Convert.FromBase64String(parts[4]);
            if (salt.Length != SaltBytes || expected.Length != HashBytes) return false;
            var actual = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA256, HashBytes);
            return CryptographicOperations.FixedTimeEquals(actual, expected);
        }
        catch (FormatException) { return false; }
    }
}
