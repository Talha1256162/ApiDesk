using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace ApiForge.Persistence.Repositories;

internal static partial class RepositoryUtility
{
    public static string Slugify(string value)
    {
        var normalized = value.Trim().ToLowerInvariant();
        normalized = InvalidSlugChars().Replace(normalized, "-");
        normalized = DuplicateDash().Replace(normalized, "-").Trim('-');
        return string.IsNullOrWhiteSpace(normalized) ? Guid.NewGuid().ToString("N")[..8] : normalized;
    }

    public static string Sha256(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(bytes);
    }

    [GeneratedRegex("[^a-z0-9]+")]
    private static partial Regex InvalidSlugChars();

    [GeneratedRegex("-+")]
    private static partial Regex DuplicateDash();
}
