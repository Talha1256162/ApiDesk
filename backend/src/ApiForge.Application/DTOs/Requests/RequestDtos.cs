namespace ApiForge.Application.DTOs.Requests;

public sealed record KeyValueItemDto(string Key, string? Value, bool Enabled = true, bool IsSecret = false);

public sealed record SendApiRequestRequest(Guid? EnvironmentId, bool SaveHistory = true);

public sealed record RunCollectionRequest(Guid? EnvironmentId, IReadOnlyList<Guid>? RequestIds = null, int DelayMs = 0);

public sealed record ApiResponseDto(
    Guid RunId,
    int StatusCode,
    string StatusText,
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
    string RequestName,
    string Method,
    string Url,
    string ActorName,
    string Status,
    Guid UserId,
    int? StatusCode,
    bool? Succeeded,
    long? ElapsedMs,
    long? SizeBytes,
    string? ErrorMessage,
    string? BodyPreview,
    DateTime StartedOn,
    DateTime? CompletedOn,
    DateTime CreatedOn);

public sealed record CollectionRunItemDto(Guid RequestId, string Name, string Method, string Url, bool Succeeded, int? StatusCode, long? ElapsedMs, string? ErrorMessage);
public sealed record CollectionRunResultDto(Guid CollectionId, int TotalRequests, int Passed, int Failed, IReadOnlyList<CollectionRunItemDto> Results);
