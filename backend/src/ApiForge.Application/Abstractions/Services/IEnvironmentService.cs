using ApiForge.Application.DTOs.Environments;
using ApiForge.Shared.Pagination;
using ApiForge.Shared.Responses;

namespace ApiForge.Application.Abstractions.Services;

public interface IEnvironmentService
{
    Task<Result<PagedResult<EnvironmentDto>>> GetEnvironmentsAsync(Guid workspaceId, PagedRequest request, CancellationToken cancellationToken);
    Task<Result<EnvironmentDto>> CreateAsync(CreateEnvironmentRequest request, CancellationToken cancellationToken);
    Task<Result<IReadOnlyList<EnvironmentVariableDto>>> UpsertVariablesAsync(Guid environmentId, UpsertEnvironmentVariablesRequest request, CancellationToken cancellationToken);
}
