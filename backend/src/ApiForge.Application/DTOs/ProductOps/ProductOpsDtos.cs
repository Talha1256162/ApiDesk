namespace ApiForge.Application.DTOs.ProductOps;

public sealed record MockServerDto(
    Guid Id,
    Guid WorkspaceId,
    Guid CollectionId,
    string CollectionName,
    string Name,
    string Slug,
    bool IsPublic,
    bool ApiKeyRequired,
    int DelayMs,
    int RouteCount,
    int LogCount,
    DateTime CreatedOn);

public sealed record CreateMockServerRequest(Guid CollectionId, string Name, bool IsPublic, bool ApiKeyRequired, int DelayMs);

public sealed record MockRouteDto(Guid Id, Guid MockServerId, string Method, string Path, Guid? RequestExampleId, string? ExampleName);

public sealed record MockLogDto(Guid Id, Guid MockServerId, Guid? MockRouteId, string Method, string Path, int StatusCode, DateTime CreatedOn);

public sealed record MockResponseDto(int StatusCode, string ContentType, string Body, string? HeadersJson);

public sealed record MockServerAccessDto(Guid Id, Guid OrganizationId, Guid WorkspaceId, bool IsPublic, bool ApiKeyRequired);

public sealed record MonitorDto(
    Guid Id,
    Guid WorkspaceId,
    Guid CollectionId,
    Guid? EnvironmentId,
    string CollectionName,
    string? EnvironmentName,
    string Name,
    string ScheduleExpression,
    bool IsEnabled,
    string? LastStatus,
    int? LastPassedCount,
    int? LastFailedCount,
    long? LastLatencyMs,
    DateTime? LastRunOn,
    DateTime CreatedOn);

public sealed record CreateMonitorRequest(Guid CollectionId, Guid? EnvironmentId, string Name, string ScheduleExpression, bool IsEnabled = true);

public sealed record MonitorRunDto(Guid Id, Guid MonitorId, string Status, int PassedCount, int FailedCount, long? LatencyMs, DateTime CreatedOn);

public sealed record ScheduledMonitorDto(Guid Id, Guid OrganizationId, Guid WorkspaceId, Guid CollectionId, Guid? EnvironmentId, string Name, string ScheduleExpression, Guid CreatedBy);

public sealed record PublishedDocDto(
    Guid Id,
    Guid WorkspaceId,
    Guid CollectionId,
    string CollectionName,
    string Slug,
    bool IsPublic,
    string? BrandJson,
    DateTime? PublishedOn,
    DateTime CreatedOn);

public sealed record PublishDocsRequest(Guid CollectionId, string Slug, bool IsPublic, string? Password, string? BrandJson);

public sealed record DocumentationRequestDto(string Name, string Method, string Url, string? Description, string? AuthType, IReadOnlyList<string> Examples);

public sealed record DocumentationDto(Guid Id, string Slug, string CollectionName, string? BrandJson, IReadOnlyList<DocumentationRequestDto> Requests);

public sealed record UnlockDocumentationRequest(string Password);

public sealed record ApiSpecDto(Guid Id, Guid WorkspaceId, Guid? CollectionId, string Name, string Format, string ValidationStatus, DateTime CreatedOn);

public sealed record UploadApiSpecRequest(Guid? CollectionId, string Name, string Format, string Content);

public sealed record GovernanceFindingDto(string Rule, string Severity, string Message, string? Location);

public sealed record ApiSpecValidationDto(ApiSpecDto Spec, IReadOnlyList<GovernanceFindingDto> Findings);
