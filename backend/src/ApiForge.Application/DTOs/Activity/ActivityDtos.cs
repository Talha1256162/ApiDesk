namespace ApiForge.Application.DTOs.Activity;

public sealed record ActivityFilterRequest(
    Guid OrganizationId,
    Guid? WorkspaceId,
    Guid? UserId,
    Guid? CollectionId,
    string? EventType,
    string? Status,
    DateTime? FromUtc,
    DateTime? ToUtc,
    int Offset = 0,
    int Count = 25);

public sealed record ActivityEventDto(
    Guid Id,
    Guid OrganizationId,
    Guid? WorkspaceId,
    Guid ActorUserId,
    string ActorName,
    string ActorEmail,
    string EventType,
    string EntityType,
    Guid? EntityId,
    string? EntityName,
    string Status,
    string Severity,
    string? Summary,
    string CorrelationId,
    DateTime CreatedOn);

public sealed record ManagerSummaryDto(
    int ActiveUsersToday,
    int RequestsSentToday,
    int FailedApisToday,
    int CollectionsChangedToday,
    int EnvironmentsChangedToday,
    int PendingApprovals,
    IReadOnlyList<ChartPointDto> RequestsPerDay,
    IReadOnlyList<ChartPointDto> FailedRequestsPerDay,
    IReadOnlyList<UserActivityPointDto> MostActiveUsers,
    IReadOnlyList<EndpointMetricDto> TopFailedEndpoints,
    IReadOnlyList<EndpointLatencyDto> AverageResponseTimeByEndpoint);

public sealed record ChartPointDto(DateTime Date, int Value);
public sealed record UserActivityPointDto(Guid UserId, string Name, int Count);
public sealed record EndpointMetricDto(Guid? RequestId, string Endpoint, int FailureCount);
public sealed record EndpointLatencyDto(Guid? RequestId, string Endpoint, decimal AverageMs);
