using ApiForge.Application.DTOs.Collections;
using ApiForge.Application.DTOs.Requests;

namespace ApiForge.Application.Abstractions.Services;

public interface IHttpRequestExecutor
{
    Task<ApiResponseDto> ExecuteAsync(Guid runId, ApiRequestDetailDto request, IReadOnlyDictionary<string, string> variables, CancellationToken cancellationToken);
}
