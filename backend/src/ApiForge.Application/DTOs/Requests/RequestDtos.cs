namespace ApiForge.Application.DTOs.Requests;

public sealed record KeyValueItemDto(string Key, string? Value, bool Enabled = true, bool IsSecret = false);

public sealed record SendApiRequestRequest(Guid? EnvironmentId, bool SaveHistory = true);

public sealed record ApiResponseDto(
    Guid RunId,
    int StatusCode,
    bool Succeeded,
    long ElapsedMs,
    long SizeBytes,
    IReadOnlyDictionary<string, string[]> Headers,
    IReadOnlyDictionary<string, string[]> Cookies,
    string ContentType,
    string Body,
    string BodyPreview,
    DateTime StartedOnUtc,
    DateTime CompletedOnUtc);

public sealed record RequestRunDto(
    Guid Id,
    Guid RequestId,
    Guid UserId,
    int? StatusCode,
    bool Succeeded,
    long ElapsedMs,
    long SizeBytes,
    DateTime CreatedOn);
