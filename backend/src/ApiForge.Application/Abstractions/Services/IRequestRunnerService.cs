using ApiForge.Application.DTOs.Requests;
using ApiForge.Shared.Responses;

namespace ApiForge.Application.Abstractions.Services;

public interface IRequestRunnerService
{
    Task<Result<ApiResponseDto>> SendAsync(Guid requestId, SendApiRequestRequest request, CancellationToken cancellationToken);
    Task<Result<IReadOnlyList<RequestRunDto>>> GetHistoryAsync(Guid requestId, int count, CancellationToken cancellationToken);
    Task<Result<CollectionRunResultDto>> RunCollectionAsync(Guid collectionId, RunCollectionRequest request, CancellationToken cancellationToken);
}
