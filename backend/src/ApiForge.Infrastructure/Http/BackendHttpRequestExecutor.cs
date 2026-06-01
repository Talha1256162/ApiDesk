using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using ApiForge.Application.Abstractions.Services;
using ApiForge.Application.DTOs.Collections;
using ApiForge.Application.DTOs.Requests;
using ApiForge.Shared.Security;

namespace ApiForge.Infrastructure.Http;

public sealed partial class BackendHttpRequestExecutor : IHttpRequestExecutor
{
    public async Task<ApiResponseDto> ExecuteAsync(Guid runId, ApiRequestDetailDto request, IReadOnlyDictionary<string, string> variables, CancellationToken cancellationToken)
    {
        var started = DateTime.UtcNow;
        var stopwatch = Stopwatch.StartNew();
        var url = ResolveVariables(request.Url, variables);

        using var handler = new HttpClientHandler
        {
            AllowAutoRedirect = request.FollowRedirects
        };

        if (!request.SslVerification)
        {
            handler.ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;
        }

        using var client = new HttpClient(handler)
        {
            Timeout = TimeSpan.FromMilliseconds(Math.Clamp(request.TimeoutMs, 1000, 120000))
        };

        using var message = new HttpRequestMessage(new HttpMethod(request.Method), BuildUrl(url, request.QueryParams, variables));
        ApplyHeaders(message, request.Headers, variables);
        ApplyAuth(message, request.AuthType, request.AuthConfigJson, variables);
        ApplyBody(message, request, variables);

        using var response = await client.SendAsync(message, HttpCompletionOption.ResponseContentRead, cancellationToken);
        var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);
        stopwatch.Stop();
        var body = Encoding.UTF8.GetString(bytes);
        var headers = response.Headers.Concat(response.Content.Headers)
            .GroupBy(h => h.Key, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.SelectMany(x => x.Value).Select(v => SensitiveDataMasker.MaskValue(g.Key, v)).ToArray(), StringComparer.OrdinalIgnoreCase);

        var cookies = headers.TryGetValue("Set-Cookie", out var setCookie)
            ? setCookie.Select((value, index) => new { Key = $"cookie{index + 1}", Value = SensitiveDataMasker.MaskValue("cookie", value) })
                .ToDictionary(x => x.Key, x => new[] { x.Value })
            : new Dictionary<string, string[]>();

        return new ApiResponseDto(
            runId,
            (int)response.StatusCode,
            response.IsSuccessStatusCode,
            stopwatch.ElapsedMilliseconds,
            bytes.LongLength,
            headers,
            cookies,
            response.Content.Headers.ContentType?.ToString() ?? "application/octet-stream",
            body,
            body.Length > 16000 ? body[..16000] : body,
            started,
            DateTime.UtcNow);
    }

    private static Uri BuildUrl(string url, IReadOnlyList<KeyValueItemDto> queryParams, IReadOnlyDictionary<string, string> variables)
    {
        var enabledParams = queryParams.Where(p => p.Enabled && !string.IsNullOrWhiteSpace(p.Key)).ToList();
        if (enabledParams.Count == 0)
        {
            return new Uri(url);
        }

        var separator = url.Contains('?') ? "&" : "?";
        var query = string.Join("&", enabledParams.Select(p => $"{WebUtility.UrlEncode(ResolveVariables(p.Key, variables))}={WebUtility.UrlEncode(ResolveVariables(p.Value ?? string.Empty, variables))}"));
        return new Uri(url + separator + query);
    }

    private static void ApplyHeaders(HttpRequestMessage message, IReadOnlyList<KeyValueItemDto> headers, IReadOnlyDictionary<string, string> variables)
    {
        foreach (var header in headers.Where(h => h.Enabled && !string.IsNullOrWhiteSpace(h.Key)))
        {
            var key = ResolveVariables(header.Key, variables);
            var value = ResolveVariables(header.Value ?? string.Empty, variables);
            if (!message.Headers.TryAddWithoutValidation(key, value))
            {
                message.Content ??= new StringContent(string.Empty);
                message.Content.Headers.TryAddWithoutValidation(key, value);
            }
        }
    }

    private static void ApplyAuth(HttpRequestMessage message, string? authType, string? authConfigJson, IReadOnlyDictionary<string, string> variables)
    {
        if (string.IsNullOrWhiteSpace(authType) || string.IsNullOrWhiteSpace(authConfigJson))
        {
            return;
        }

        using var json = JsonDocument.Parse(authConfigJson);
        var root = json.RootElement;
        if (authType.Equals("Bearer", StringComparison.OrdinalIgnoreCase) && root.TryGetProperty("token", out var token))
        {
            message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", ResolveVariables(token.GetString() ?? string.Empty, variables));
        }
        else if (authType.Equals("Basic", StringComparison.OrdinalIgnoreCase) && root.TryGetProperty("username", out var username) && root.TryGetProperty("password", out var password))
        {
            var raw = $"{ResolveVariables(username.GetString() ?? string.Empty, variables)}:{ResolveVariables(password.GetString() ?? string.Empty, variables)}";
            message.Headers.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes(raw)));
        }
    }

    private static void ApplyBody(HttpRequestMessage message, ApiRequestDetailDto request, IReadOnlyDictionary<string, string> variables)
    {
        if (request.Method.Equals("GET", StringComparison.OrdinalIgnoreCase) || string.Equals(request.BodyType, "none", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var content = ResolveVariables(request.BodyContent ?? string.Empty, variables);
        var contentType = request.BodyType.Equals("rawJson", StringComparison.OrdinalIgnoreCase) ? "application/json" : "text/plain";
        message.Content = new StringContent(content, Encoding.UTF8, contentType);
    }

    private static string ResolveVariables(string value, IReadOnlyDictionary<string, string> variables)
    {
        return VariableRegex().Replace(value, match =>
        {
            var key = match.Groups["key"].Value.Trim();
            return variables.TryGetValue(key, out var replacement) ? replacement : match.Value;
        });
    }

    [GeneratedRegex("\\{\\{(?<key>[^}]+)\\}\\}")]
    private static partial Regex VariableRegex();
}
