using ApiForge.Application.DTOs.Workspaces;
using ApiForge.Shared.Pagination;

namespace ApiForge.Application.Abstractions.Persistence;

public interface IWorkspaceRepository
{
    Task<Guid?> GetOrganizationIdAsync(Guid workspaceId, CancellationToken cancellationToken);
    Task<PagedResult<WorkspaceDto>> GetByOrganizationAsync(Guid organizationId, PagedRequest request, CancellationToken cancellationToken);
    Task<WorkspaceDto> CreateAsync(CreateWorkspaceRequest request, Guid createdBy, CancellationToken cancellationToken);
    Task<WorkspaceDto?> UpdateAsync(Guid workspaceId, UpdateWorkspaceRequest request, Guid modifiedBy, CancellationToken cancellationToken);
    Task<bool> DeleteAsync(Guid workspaceId, Guid modifiedBy, CancellationToken cancellationToken);
    Task<WorkspaceDashboardDto> GetDashboardAsync(Guid workspaceId, CancellationToken cancellationToken);
}
