using System.Globalization;
using System.Security.Cryptography;

namespace TechHelpSolutions.Services;

public sealed class PasswordHashService
{
    public const int Iterations = 600_000;
    private const string Prefix = "THS$1$PBKDF2-SHA256$";

    public string Hash(string password)
    {
        ArgumentNullException.ThrowIfNull(password);
        var salt = RandomNumberGenerator.GetBytes(16);
        var derived = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithmName.SHA256, 32);
        return Prefix + Iterations.ToString(CultureInfo.InvariantCulture) + "$" + Convert.ToBase64String(salt) + "$" + Convert.ToBase64String(derived);
    }

    public string IdentifyFormat(string? stored) => TryParse(stored, out _, out _, out _) ? "PBKDF2-SHA256-v1"
        : string.IsNullOrEmpty(stored) || stored.StartsWith("THS$", StringComparison.Ordinal) ? "Invalid" : "Legacy";

    public bool NeedsRehash(string? stored) => !TryParse(stored, out var cost, out var salt, out _) || cost < Iterations || salt.Length < 16;

    public bool Verify(string? password, string? stored)
    {
        if (password is null || !TryParse(stored, out var cost, out var salt, out var expected)) return false;
        var actual = Rfc2898DeriveBytes.Pbkdf2(password, salt, cost, HashAlgorithmName.SHA256, 32);
        try { return CryptographicOperations.FixedTimeEquals(actual, expected); }
        finally { CryptographicOperations.ZeroMemory(actual); }
    }

    private static bool TryParse(string? stored, out int cost, out byte[] salt, out byte[] hash)
    {
        cost = 0; salt = []; hash = [];
        if (stored is null || stored.Length > 512 || !stored.StartsWith(Prefix, StringComparison.Ordinal)) return false;
        var parts = stored.Split('$');
        if (parts.Length != 6 || !int.TryParse(parts[3], NumberStyles.None, CultureInfo.InvariantCulture, out cost)
            || cost < 100_000 || cost > 2_000_000) return false;
        try { salt = Convert.FromBase64String(parts[4]); hash = Convert.FromBase64String(parts[5]); }
        catch (FormatException) { return false; }
        return salt.Length is >= 16 and <= 64 && hash.Length == 32;
    }
}
