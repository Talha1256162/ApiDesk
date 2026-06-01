using System.Text.Json;
using System.Text.RegularExpressions;

namespace ApiForge.Shared.Security;

public static partial class SensitiveDataMasker
{
    private static readonly HashSet<string> SensitiveKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "authorization",
        "password",
        "passwd",
        "token",
        "accessToken",
        "refreshToken",
        "apiKey",
        "apikey",
        "secret",
        "clientSecret",
        "cookie",
        "set-cookie",
        "x-api-key"
    };

    public static string MaskValue(string? key, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        if (!string.IsNullOrWhiteSpace(key) && SensitiveKeys.Contains(key))
        {
            return "********";
        }

        if (BearerTokenRegex().IsMatch(value) || LongSecretRegex().IsMatch(value))
        {
            return "********";
        }

        return value;
    }

    public static string MaskJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return "{}";
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            var masked = MaskElement(document.RootElement, null);
            return JsonSerializer.Serialize(masked);
        }
        catch
        {
            return "{}";
        }
    }

    private static object? MaskElement(JsonElement element, string? currentKey)
    {
        return element.ValueKind switch
        {
            JsonValueKind.Object => element.EnumerateObject()
                .ToDictionary(prop => prop.Name, prop => MaskElement(prop.Value, prop.Name)),
            JsonValueKind.Array => element.EnumerateArray()
                .Select(item => MaskElement(item, currentKey))
                .ToArray(),
            JsonValueKind.String => MaskValue(currentKey, element.GetString()),
            JsonValueKind.Number => element.GetRawText(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            _ => null
        };
    }

    [GeneratedRegex("^Bearer\\s+[A-Za-z0-9._~+/=-]{12,}$", RegexOptions.IgnoreCase)]
    private static partial Regex BearerTokenRegex();

    [GeneratedRegex("^[A-Za-z0-9._~+/=-]{32,}$")]
    private static partial Regex LongSecretRegex();
}
