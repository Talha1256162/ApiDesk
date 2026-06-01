using ApiForge.Application.DTOs.Workspaces;
using ApiForge.Shared.Pagination;
using ApiForge.Shared.Responses;

namespace ApiForge.Application.Abstractions.Services;

public interface IWorkspaceService
{
    Task<Result<PagedResult<WorkspaceDto>>> GetWorkspacesAsync(Guid organizationId, PagedRequest request, CancellationToken cancellationToken);
    Task<Result<WorkspaceDto>> CreateAsync(CreateWorkspaceRequest request, CancellationToken cancellationToken);
    Task<Result<WorkspaceDto>> UpdateAsync(Guid workspaceId, UpdateWorkspaceRequest request, CancellationToken cancellationToken);
    Task<Result> DeleteAsync(Guid workspaceId, CancellationToken cancellationToken);
    Task<Result<WorkspaceDashboardDto>> GetDashboardAsync(Guid workspaceId, CancellationToken cancellationToken);
}
