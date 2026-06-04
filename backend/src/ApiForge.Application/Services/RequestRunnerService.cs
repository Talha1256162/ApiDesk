using ApiForge.Application.Abstractions.Auth;
using ApiForge.Application.Abstractions.Persistence;
using ApiForge.Application.Abstractions.Services;
using ApiForge.Application.DTOs.Collections;
using ApiForge.Application.DTOs.Requests;
using ApiForge.Domain.Constants;
using ApiForge.Shared.Responses;
using System.Text.RegularExpressions;

namespace ApiForge.Application.Services;

public sealed partial class RequestRunnerService(
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
        var missingVariables = FindMissingVariables(apiRequest, variables);
        if (missingVariables.Count > 0)
        {
            var message = $"Missing variables: {string.Join(", ", missingVariables)}.";
            await RecordActivityAsync(scope.Value.OrganizationId, apiRequest.WorkspaceId, "RequestFailed", "Request", requestId, apiRequest.Name, "Run", "Failure", "Warning", message, null, cancellationToken);
            return Result<ApiResponseDto>.Failure("Request has unresolved variables.", new ErrorDetail("request.variables_missing", message));
        }

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

    public async Task<Result<IReadOnlyList<RequestRunDto>>> GetHistoryAsync(Guid requestId, int count, CancellationToken cancellationToken)
    {
        if (CurrentUser is null)
        {
            return Unauthorized<IReadOnlyList<RequestRunDto>>();
        }

        var scope = await collectionRepository.GetRequestScopeAsync(requestId, cancellationToken);
        if (scope is null)
        {
            return Result<IReadOnlyList<RequestRunDto>>.Failure("Request was not found.", new ErrorDetail("request.not_found", "Request was not found."));
        }

        var allowed = await permissionService.HasPermissionAsync(CurrentUser.UserId, scope.Value.OrganizationId, scope.Value.WorkspaceId, PermissionKeys.ViewRequestHistory, cancellationToken);
        if (!allowed)
        {
            return Forbidden<IReadOnlyList<RequestRunDto>>(PermissionKeys.ViewRequestHistory);
        }

        var history = await requestRunRepository.GetHistoryAsync(requestId, Math.Clamp(count, 1, 100), cancellationToken);
        return Result<IReadOnlyList<RequestRunDto>>.Success(history);
    }

    public async Task<Result<CollectionRunResultDto>> RunCollectionAsync(Guid collectionId, RunCollectionRequest request, CancellationToken cancellationToken)
    {
        if (CurrentUser is null)
        {
            return Unauthorized<CollectionRunResultDto>();
        }

        var scope = await collectionRepository.GetCollectionScopeAsync(collectionId, cancellationToken);
        if (scope is null)
        {
            return Result<CollectionRunResultDto>.Failure("Collection was not found.", new ErrorDetail("collection.not_found", "Collection was not found."));
        }

        var allowed = await permissionService.HasPermissionAsync(CurrentUser.UserId, scope.Value.OrganizationId, scope.Value.WorkspaceId, PermissionKeys.RunRequests, cancellationToken);
        if (!allowed)
        {
            return Forbidden<CollectionRunResultDto>(PermissionKeys.RunRequests);
        }

        var requests = await collectionRepository.GetCollectionRequestsAsync(collectionId, cancellationToken);
        var selectedIds = request.RequestIds?.Count > 0 ? request.RequestIds.ToHashSet() : null;
        var runList = selectedIds is null ? requests : requests.Where(item => selectedIds.Contains(item.Id)).ToList();
        var results = new List<CollectionRunItemDto>(runList.Count);

        foreach (var item in runList)
        {
            if (request.DelayMs > 0 && results.Count > 0)
            {
                await Task.Delay(Math.Clamp(request.DelayMs, 0, 30000), cancellationToken);
            }

            var result = await SendAsync(item.Id, new SendApiRequestRequest(request.EnvironmentId), cancellationToken);
            if (result.Succeeded && result.Data is not null)
            {
                results.Add(new CollectionRunItemDto(item.Id, item.Name, item.Method, item.Url, result.Data.Succeeded, result.Data.StatusCode, result.Data.ElapsedMs, null));
            }
            else
            {
                results.Add(new CollectionRunItemDto(item.Id, item.Name, item.Method, item.Url, false, null, null, result.Message));
            }
        }

        var summary = new CollectionRunResultDto(collectionId, results.Count, results.Count(item => item.Succeeded), results.Count(item => !item.Succeeded), results);
        await RecordActivityAsync(scope.Value.OrganizationId, scope.Value.WorkspaceId, "CollectionRunCompleted", "Collection", collectionId, "Collection run", "Run", summary.Failed == 0 ? "Success" : "Failure", summary.Failed == 0 ? "Info" : "Warning", $"{summary.Passed}/{summary.TotalRequests} requests passed.", null, cancellationToken);
        return Result<CollectionRunResultDto>.Success(summary, "Collection run completed.");
    }

    private static IReadOnlyList<string> FindMissingVariables(ApiRequestDetailDto request, IReadOnlyDictionary<string, string> variables)
    {
        var values = new List<string?>
        {
            request.Url,
            request.BodyContent,
            request.AuthConfigJson
        };

        values.AddRange(request.Headers.SelectMany(item => new[] { item.Key, item.Value }));
        values.AddRange(request.QueryParams.SelectMany(item => new[] { item.Key, item.Value }));
        values.AddRange(request.PathParams.SelectMany(item => new[] { item.Key, item.Value }));

        return values
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .SelectMany(value => VariableTokenRegex().Matches(value!).Select(match => match.Groups["key"].Value.Trim()))
            .Where(key => !string.IsNullOrWhiteSpace(key) && !variables.ContainsKey(key))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(key => key, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    [GeneratedRegex("\\{\\{(?<key>[^}]+)\\}\\}")]
    private static partial Regex VariableTokenRegex();
}
