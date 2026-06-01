using ApiForge.Application.DTOs.Requests;

namespace ApiForge.Application.Abstractions.Persistence;

public interface IRequestRunRepository
{
    Task<Guid> CreateRunAsync(Guid requestId, Guid workspaceId, Guid? environmentId, Guid userId, DateTime startedOnUtc, CancellationToken cancellationToken);
    Task CompleteRunAsync(Guid runId, ApiResponseDto response, CancellationToken cancellationToken);
    Task FailRunAsync(Guid runId, string errorMessage, long elapsedMs, DateTime completedOnUtc, CancellationToken cancellationToken);
}
