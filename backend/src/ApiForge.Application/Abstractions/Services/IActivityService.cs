using ApiForge.Application.DTOs.Activity;
using ApiForge.Shared.Pagination;
using ApiForge.Shared.Responses;

namespace ApiForge.Application.Abstractions.Services;

public interface IActivityService
{
    Task<Result<PagedResult<ActivityEventDto>>> GetActivityAsync(ActivityFilterRequest request, CancellationToken cancellationToken);
    Task<Result<ManagerSummaryDto>> GetManagerSummaryAsync(Guid workspaceId, CancellationToken cancellationToken);
    Task<Result<PagedResult<AuditLogDto>>> GetAuditLogsAsync(ActivityFilterRequest request, CancellationToken cancellationToken);
    Task<Result<string>> ExportActivityCsvAsync(ActivityFilterRequest request, CancellationToken cancellationToken);
    Task<Result<string>> ExportAuditCsvAsync(ActivityFilterRequest request, CancellationToken cancellationToken);
}
