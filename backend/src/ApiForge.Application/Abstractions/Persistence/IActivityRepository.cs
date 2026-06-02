using ApiForge.Application.DTOs.Activity;
using ApiForge.Shared.Pagination;

namespace ApiForge.Application.Abstractions.Persistence;

public interface IActivityRepository
{
    Task RecordAsync(ActivityWriteModel activity, CancellationToken cancellationToken);
    Task<PagedResult<ActivityEventDto>> GetActivityAsync(ActivityFilterRequest request, CancellationToken cancellationToken);
    Task<ManagerSummaryDto> GetManagerSummaryAsync(Guid workspaceId, CancellationToken cancellationToken);
    Task<PagedResult<AuditLogDto>> GetAuditLogsAsync(ActivityFilterRequest request, CancellationToken cancellationToken);
}

public sealed record ActivityWriteModel(
    Guid OrganizationId,
    Guid? WorkspaceId,
    Guid ActorUserId,
    string ActorName,
    string ActorEmail,
    string EventType,
    string EntityType,
    Guid? EntityId,
    string? EntityName,
    string Action,
    string Status,
    string Severity,
    string? Summary,
    string? MetadataJson,
    string? IpAddress,
    string? UserAgent,
    string CorrelationId);
