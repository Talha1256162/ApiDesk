using ApiForge.Application.Abstractions.Auth;
using ApiForge.Application.Abstractions.Persistence;
using ApiForge.Application.Abstractions.Services;
using ApiForge.Application.DTOs.Environments;
using ApiForge.Domain.Constants;
using ApiForge.Shared.Pagination;
using ApiForge.Shared.Responses;

namespace ApiForge.Application.Services;

public sealed class EnvironmentService(
    IEnvironmentRepository environmentRepository,
    ICollectionRepository collectionRepository,
    IPermissionService permissionService,
    ICurrentUserContext currentUserContext,
    IActivityRepository activityRepository) : ServiceBase(currentUserContext, activityRepository), IEnvironmentService
{
    public async Task<Result<PagedResult<EnvironmentDto>>> GetEnvironmentsAsync(Guid workspaceId, PagedRequest request, CancellationToken cancellationToken)
    {
        if (CurrentUser is null)
        {
            return Unauthorized<PagedResult<EnvironmentDto>>();
        }

        var organizationId = await collectionRepository.GetWorkspaceOrganizationIdAsync(workspaceId, cancellationToken);
        if (organizationId is null)
        {
            return Result<PagedResult<EnvironmentDto>>.Failure("Workspace was not found.", new ErrorDetail("workspace.not_found", "Workspace was not found."));
        }

        var isMember = await permissionService.IsWorkspaceMemberAsync(CurrentUser.UserId, organizationId.Value, workspaceId, cancellationToken);
        var canManage = await permissionService.HasPermissionAsync(CurrentUser.UserId, organizationId.Value, workspaceId, PermissionKeys.ManageEnvironments, cancellationToken);
        if (!isMember && !canManage)
        {
            return Forbidden<PagedResult<EnvironmentDto>>("workspace.member");
        }

        var environments = await environmentRepository.GetEnvironmentsAsync(workspaceId, request, cancellationToken);
        return Result<PagedResult<EnvironmentDto>>.Success(environments);
    }

    public async Task<Result<EnvironmentDto>> CreateAsync(CreateEnvironmentRequest request, CancellationToken cancellationToken)
    {
        if (CurrentUser is null)
        {
            return Unauthorized<EnvironmentDto>();
        }

        var organizationId = await collectionRepository.GetWorkspaceOrganizationIdAsync(request.WorkspaceId, cancellationToken);
        if (organizationId is null)
        {
            return Result<EnvironmentDto>.Failure("Workspace was not found.", new ErrorDetail("workspace.not_found", "Workspace was not found."));
        }

        var allowed = await permissionService.HasPermissionAsync(CurrentUser.UserId, organizationId.Value, request.WorkspaceId, PermissionKeys.ManageEnvironments, cancellationToken);
        if (!allowed)
        {
            return Forbidden<EnvironmentDto>(PermissionKeys.ManageEnvironments);
        }

        var environment = await environmentRepository.CreateAsync(request, CurrentUser.UserId, cancellationToken);
        await RecordActivityAsync(organizationId.Value, request.WorkspaceId, "EnvironmentCreated", "Environment", environment.Id, environment.Name, "Create", "Success", "Info", "Environment created.", null, cancellationToken);
        return Result<EnvironmentDto>.Success(environment, "Environment created.");
    }

    public async Task<Result<IReadOnlyList<EnvironmentVariableDto>>> UpsertVariablesAsync(Guid environmentId, UpsertEnvironmentVariablesRequest request, CancellationToken cancellationToken)
    {
        if (CurrentUser is null)
        {
            return Unauthorized<IReadOnlyList<EnvironmentVariableDto>>();
        }

        var scope = await environmentRepository.GetEnvironmentScopeAsync(environmentId, cancellationToken);
        if (scope is null)
        {
            return Result<IReadOnlyList<EnvironmentVariableDto>>.Failure("Environment was not found.", new ErrorDetail("environment.not_found", "Environment was not found."));
        }

        var allowed = await permissionService.HasPermissionAsync(CurrentUser.UserId, scope.Value.OrganizationId, scope.Value.WorkspaceId, PermissionKeys.ManageEnvironments, cancellationToken);
        if (!allowed)
        {
            return Forbidden<IReadOnlyList<EnvironmentVariableDto>>(PermissionKeys.ManageEnvironments);
        }

        var variables = await environmentRepository.UpsertVariablesAsync(environmentId, request, CurrentUser.UserId, cancellationToken);
        await RecordActivityAsync(scope.Value.OrganizationId, scope.Value.WorkspaceId, "EnvironmentVariablesChanged", "Environment", environmentId, "Environment", "Update", "Success", "Info", "Environment variables changed.", null, cancellationToken);
        return Result<IReadOnlyList<EnvironmentVariableDto>>.Success(variables, "Variables saved.");
    }
}
