namespace ApiForge.Application.DTOs.Workspaces;

public sealed record WorkspaceDto(
    Guid Id,
    Guid OrganizationId,
    string Name,
    string Slug,
    string Type,
    string? Description,
    DateTime CreatedOn);

public sealed record CreateWorkspaceRequest(Guid OrganizationId, string Name, string Type, string? Description);
public sealed record UpdateWorkspaceRequest(string Name, string Type, string? Description, int VersionNumber);

public sealed record WorkspaceDashboardDto(
    Guid WorkspaceId,
    int TotalCollections,
    int TotalApis,
    int RequestsSentToday,
    int FailedRequestsToday,
    int ActiveMembers,
    IReadOnlyList<RecentActivityDto> RecentActivity,
    IReadOnlyList<LatestTestRunDto> LatestTestRuns,
    IReadOnlyList<SlowApiDto> SlowestApis,
    IReadOnlyList<EnvironmentUsageDto> MostUsedEnvironments);

public sealed record RecentActivityDto(DateTime CreatedOn, string ActorName, string EventType, string EntityName, string Status);
public sealed record LatestTestRunDto(Guid Id, string Name, string Status, DateTime CreatedOn);
public sealed record SlowApiDto(Guid RequestId, string Name, string Method, string Url, decimal AverageMs);
public sealed record EnvironmentUsageDto(Guid EnvironmentId, string Name, int RunCount);
