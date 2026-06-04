using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using ApiForge.Application.Abstractions.Services;
using ApiForge.Application.DTOs.Phase4;
using Microsoft.Extensions.Configuration;

namespace ApiForge.Application.Services;

public sealed class AiProviderService(IConfiguration configuration, IHttpClientFactory httpClientFactory) : IAiProviderService
{
    private const int DefaultTimeoutSeconds = 20;

    public AiProviderStatusDto GetStatus()
    {
        var provider = GetProviderName();
        var model = configuration["AI_MODEL"] ?? string.Empty;
        var timeout = GetTimeoutSeconds();
        var configured = IsConfigured(provider, configuration["AI_API_KEY"], configuration["AI_BASE_URL"], model);
        return new AiProviderStatusDto(configured, provider, string.IsNullOrWhiteSpace(model) ? "not configured" : model, true, timeout);
    }

    public async Task<IReadOnlyList<string>?> GenerateSuggestionsAsync(string action, string context, CancellationToken cancellationToken)
    {
        var status = GetStatus();
        if (!status.Configured)
        {
            return null;
        }

        var baseUrl = NormalizeBaseUrl(configuration["AI_BASE_URL"] ?? "https://api.openai.com/v1");
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(status.TimeoutSeconds));

        var client = httpClientFactory.CreateClient(nameof(AiProviderService));
        using var message = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl}/chat/completions");
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", configuration["AI_API_KEY"]);
        message.Content = new StringContent(JsonSerializer.Serialize(new
        {
            model = configuration["AI_MODEL"],
            temperature = 0.2,
            messages = new[]
            {
                new { role = "system", content = "You generate concise API Desk suggestions. Return only a JSON array of 3 to 6 short strings. Do not include markdown." },
                new { role = "user", content = $"Action: {action}\nContext: {context}" }
            }
        }), Encoding.UTF8, "application/json");

        try
        {
            using var response = await client.SendAsync(message, timeoutCts.Token);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            await using var stream = await response.Content.ReadAsStreamAsync(timeoutCts.Token);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: timeoutCts.Token);
            var content = document.RootElement
                .GetProperty("choices")[0]
                .GetProperty("message")
                .GetProperty("content")
                .GetString();

            return ParseSuggestionArray(content);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return null;
        }
        catch
        {
            return null;
        }
    }

    private static IReadOnlyList<string>? ParseSuggestionArray(string? content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return null;
        }

        try
        {
            var parsed = JsonSerializer.Deserialize<string[]>(content);
            return parsed?.Where(item => !string.IsNullOrWhiteSpace(item)).Select(item => item.Trim()).Take(6).ToArray();
        }
        catch
        {
            var lines = content.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(line => line.Trim('-', '*', ' ', '"'))
                .Where(line => line.Length > 0)
                .Take(6)
                .ToArray();
            return lines.Length == 0 ? null : lines;
        }
    }

    private string GetProviderName() => configuration["AI_PROVIDER"] switch
    {
        { Length: > 0 } provider => provider,
        _ => "Fallback"
    };

    private int GetTimeoutSeconds()
    {
        var raw = configuration["AI_TIMEOUT_SECONDS"];
        return int.TryParse(raw, out var seconds) && seconds is >= 3 and <= 120 ? seconds : DefaultTimeoutSeconds;
    }

    private static bool IsConfigured(string provider, string? apiKey, string? baseUrl, string? model)
    {
        if (string.Equals(provider, "Fallback", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (provider.Contains("OpenAI", StringComparison.OrdinalIgnoreCase))
        {
            return !string.IsNullOrWhiteSpace(apiKey)
                && !string.IsNullOrWhiteSpace(model)
                && Uri.TryCreate(baseUrl ?? "https://api.openai.com/v1", UriKind.Absolute, out var uri)
                && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp);
        }

        return false;
    }

    private static string NormalizeBaseUrl(string baseUrl) => baseUrl.Trim().TrimEnd('/');
}
