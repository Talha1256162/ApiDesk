using ApiForge.Application.DTOs.Requests;
using ApiForge.Shared.Responses;

namespace ApiForge.Application.Abstractions.Services;

public interface IRequestRunnerService
{
    Task<Result<ApiResponseDto>> SendAsync(Guid requestId, SendApiRequestRequest request, CancellationToken cancellationToken);
}
