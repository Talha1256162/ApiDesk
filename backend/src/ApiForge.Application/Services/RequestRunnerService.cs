using ApiForge.Application.Abstractions.Auth;
using ApiForge.Application.Abstractions.Persistence;
using ApiForge.Application.Abstractions.Services;
using ApiForge.Application.DTOs.Requests;
using ApiForge.Domain.Constants;
using ApiForge.Shared.Responses;

namespace ApiForge.Application.Services;

public sealed class RequestRunnerService(
    ICollectionRepository collectionRepository,
    IEnvironmentRepository environmentRepository,
    IRequestRunRepository requestRunRepository,
    IHttpRequestExecutor httpRequestExecutor,
    IPermissionService permissionService,
    ICurrentUserContext currentUserContext,
    IActivityRepository activityRepository) : ServiceBase(currentUserContext, activityRepository), IRequestRunnerService
{
    public async Task<Result<ApiResponseDto>> SendAsync(Guid requestId, SendApiRequestRequest request, CancellationToken cancellationToken)
    {
        if (CurrentUser is null)
        {
            return Unauthorized<ApiResponseDto>();
        }

        var apiRequest = await collectionRepository.GetRequestAsync(requestId, cancellationToken);
        if (apiRequest is null)
        {
            return Result<ApiResponseDto>.Failure("Request was not found.", new ErrorDetail("request.not_found", "Request was not found."));
        }

        var scope = await collectionRepository.GetRequestScopeAsync(requestId, cancellationToken);
        if (scope is null)
        {
            return Result<ApiResponseDto>.Failure("Request was not found.", new ErrorDetail("request.not_found", "Request was not found."));
        }

        var allowed = await permissionService.HasPermissionAsync(CurrentUser.UserId, scope.Value.OrganizationId, scope.Value.WorkspaceId, PermissionKeys.RunRequests, cancellationToken);
        if (!allowed)
        {
            return Forbidden<ApiResponseDto>(PermissionKeys.RunRequests);
        }

        var variables = await environmentRepository.ResolveVariablesAsync(apiRequest.WorkspaceId, apiRequest.CollectionId, request.EnvironmentId, CurrentUser.UserId, cancellationToken);
        var runId = await requestRunRepository.CreateRunAsync(requestId, apiRequest.WorkspaceId, request.EnvironmentId, CurrentUser.UserId, DateTime.UtcNow, cancellationToken);

        try
        {
            var response = await httpRequestExecutor.ExecuteAsync(runId, apiRequest, variables, cancellationToken);
            if (request.SaveHistory)
            {
                await requestRunRepository.CompleteRunAsync(runId, response, cancellationToken);
            }

            await RecordActivityAsync(scope.Value.OrganizationId, apiRequest.WorkspaceId, response.Succeeded ? "RequestSent" : "RequestFailed", "Request", requestId, apiRequest.Name, "Run", response.Succeeded ? "Success" : "Failure", response.Succeeded ? "Info" : "Error", $"{apiRequest.Method} {apiRequest.Url} returned {response.StatusCode}.", null, cancellationToken);
            return Result<ApiResponseDto>.Success(response, "Request executed.");
        }
        catch (Exception ex)
        {
            await requestRunRepository.FailRunAsync(runId, ex.Message, 0, DateTime.UtcNow, cancellationToken);
            await RecordActivityAsync(scope.Value.OrganizationId, apiRequest.WorkspaceId, "RequestFailed", "Request", requestId, apiRequest.Name, "Run", "Failure", "Error", ex.Message, null, cancellationToken);
            return Result<ApiResponseDto>.Failure("Request execution failed.", new ErrorDetail("request.execution_failed", ex.Message));
        }
    }
}
