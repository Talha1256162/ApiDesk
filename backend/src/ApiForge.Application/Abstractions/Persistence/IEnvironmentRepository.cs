using ApiForge.Application.DTOs.Environments;
using ApiForge.Shared.Pagination;

namespace ApiForge.Application.Abstractions.Persistence;

public interface IEnvironmentRepository
{
    Task<Guid?> GetWorkspaceOrganizationIdByEnvironmentAsync(Guid environmentId, CancellationToken cancellationToken);
    Task<(Guid OrganizationId, Guid WorkspaceId)?> GetEnvironmentScopeAsync(Guid environmentId, CancellationToken cancellationToken);
    Task<PagedResult<EnvironmentDto>> GetEnvironmentsAsync(Guid workspaceId, PagedRequest request, CancellationToken cancellationToken);
    Task<EnvironmentDto> CreateAsync(CreateEnvironmentRequest request, Guid userId, CancellationToken cancellationToken);
    Task<EnvironmentDto?> UpdateAsync(Guid environmentId, UpdateEnvironmentRequest request, Guid userId, CancellationToken cancellationToken);
    Task<EnvironmentDto?> DuplicateAsync(Guid environmentId, DuplicateEnvironmentRequest request, Guid userId, CancellationToken cancellationToken);
    Task<bool> DeleteAsync(Guid environmentId, Guid userId, CancellationToken cancellationToken);
    Task<IReadOnlyList<EnvironmentVariableDto>> GetVariablesAsync(Guid environmentId, CancellationToken cancellationToken);
    Task<IReadOnlyList<EnvironmentVariableDto>> UpsertVariablesAsync(Guid environmentId, UpsertEnvironmentVariablesRequest request, Guid userId, CancellationToken cancellationToken);
    Task<IReadOnlyDictionary<string, string>> ResolveVariablesAsync(Guid workspaceId, Guid? collectionId, Guid? environmentId, Guid userId, CancellationToken cancellationToken);
}
