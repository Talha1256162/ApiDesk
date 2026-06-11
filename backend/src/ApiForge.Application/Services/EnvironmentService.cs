using ApiForge.Application.Abstractions.Auth;
using ApiForge.Application.Abstractions.Persistence;
using ApiForge.Application.Abstractions.Services;
using ApiForge.Application.DTOs.Environments;
using ApiForge.Domain.Constants;
using ApiForge.Shared.Pagination;
using ApiForge.Shared.Responses;
using System.Text.RegularExpressions;

namespace ApiForge.Application.Services;

public sealed class EnvironmentService(
    IEnvironmentRepository environmentRepository,
    ICollectionRepository collectionRepository,
    IPermissionService permissionService,
    ICurrentUserContext currentUserContext,
    IActivityRepository activityRepository) : ServiceBase(currentUserContext, activityRepository), IEnvironmentService
{
    private static readonly Regex VariableKeyRegex = new("^[A-Za-z_][A-Za-z0-9_.-]*$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

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

    public async Task<Result<EnvironmentDto>> UpdateAsync(Guid environmentId, UpdateEnvironmentRequest request, CancellationToken cancellationToken)
    {
        if (CurrentUser is null)
        {
            return Unauthorized<EnvironmentDto>();
        }

        var scope = await environmentRepository.GetEnvironmentScopeAsync(environmentId, cancellationToken);
        if (scope is null)
        {
            return Result<EnvironmentDto>.Failure("Environment was not found.", new ErrorDetail("environment.not_found", "Environment was not found."));
        }

        var allowed = await permissionService.HasPermissionAsync(CurrentUser.UserId, scope.Value.OrganizationId, scope.Value.WorkspaceId, PermissionKeys.ManageEnvironments, cancellationToken);
        if (!allowed)
        {
            return Forbidden<EnvironmentDto>(PermissionKeys.ManageEnvironments);
        }

        var name = request.Name.Trim();
        if (string.IsNullOrWhiteSpace(name) || name.Length > 160)
        {
            return Result<EnvironmentDto>.Failure("Environment name is invalid.", new ErrorDetail("environment.name_invalid", "Environment name is required and must be 160 characters or fewer.", "name"));
        }

        var updated = await environmentRepository.UpdateAsync(environmentId, request with { Name = name }, CurrentUser.UserId, cancellationToken);
        if (updated is null)
        {
            return Result<EnvironmentDto>.Failure("Environment was not found.", new ErrorDetail("environment.not_found", "Environment was not found."));
        }

        await RecordActivityAsync(scope.Value.OrganizationId, scope.Value.WorkspaceId, "EnvironmentUpdated", "Environment", environmentId, updated.Name, "Update", "Success", "Info", "Environment updated.", null, cancellationToken);
        return Result<EnvironmentDto>.Success(updated, "Environment updated.");
    }

    public async Task<Result<EnvironmentDto>> DuplicateAsync(Guid environmentId, DuplicateEnvironmentRequest request, CancellationToken cancellationToken)
    {
        if (CurrentUser is null)
        {
            return Unauthorized<EnvironmentDto>();
        }

        var scope = await environmentRepository.GetEnvironmentScopeAsync(environmentId, cancellationToken);
        if (scope is null)
        {
            return Result<EnvironmentDto>.Failure("Environment was not found.", new ErrorDetail("environment.not_found", "Environment was not found."));
        }

        var allowed = await permissionService.HasPermissionAsync(CurrentUser.UserId, scope.Value.OrganizationId, scope.Value.WorkspaceId, PermissionKeys.ManageEnvironments, cancellationToken);
        if (!allowed)
        {
            return Forbidden<EnvironmentDto>(PermissionKeys.ManageEnvironments);
        }

        if (!string.IsNullOrWhiteSpace(request.Name) && request.Name.Trim().Length > 160)
        {
            return Result<EnvironmentDto>.Failure("Environment name is invalid.", new ErrorDetail("environment.name_invalid", "Environment name must be 160 characters or fewer.", "name"));
        }

        var duplicated = await environmentRepository.DuplicateAsync(environmentId, request with { Name = request.Name?.Trim() }, CurrentUser.UserId, cancellationToken);
        if (duplicated is null)
        {
            return Result<EnvironmentDto>.Failure("Environment was not found.", new ErrorDetail("environment.not_found", "Environment was not found."));
        }

        await RecordActivityAsync(scope.Value.OrganizationId, scope.Value.WorkspaceId, "EnvironmentDuplicated", "Environment", duplicated.Id, duplicated.Name, "Create", "Success", "Info", "Environment duplicated.", null, cancellationToken);
        return Result<EnvironmentDto>.Success(duplicated, "Environment duplicated.");
    }

    public async Task<Result> DeleteAsync(Guid environmentId, CancellationToken cancellationToken)
    {
        if (CurrentUser is null)
        {
            return Unauthorized();
        }

        var scope = await environmentRepository.GetEnvironmentScopeAsync(environmentId, cancellationToken);
        if (scope is null)
        {
            return Result.Failure("Environment was not found.", new ErrorDetail("environment.not_found", "Environment was not found."));
        }

        var allowed = await permissionService.HasPermissionAsync(CurrentUser.UserId, scope.Value.OrganizationId, scope.Value.WorkspaceId, PermissionKeys.ManageEnvironments, cancellationToken);
        if (!allowed)
        {
            return Forbidden(PermissionKeys.ManageEnvironments);
        }

        var deleted = await environmentRepository.DeleteAsync(environmentId, CurrentUser.UserId, cancellationToken);
        if (!deleted)
        {
            return Result.Failure("Environment was not found.", new ErrorDetail("environment.not_found", "Environment was not found."));
        }

        await RecordActivityAsync(scope.Value.OrganizationId, scope.Value.WorkspaceId, "EnvironmentDeleted", "Environment", environmentId, "Environment", "Delete", "Success", "Info", "Environment deleted.", null, cancellationToken);
        return Result.Success("Environment deleted.");
    }

    public async Task<Result<IReadOnlyList<EnvironmentVariableDto>>> GetVariablesAsync(Guid environmentId, CancellationToken cancellationToken)
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

        var isMember = await permissionService.IsWorkspaceMemberAsync(CurrentUser.UserId, scope.Value.OrganizationId, scope.Value.WorkspaceId, cancellationToken);
        var canManage = await permissionService.HasPermissionAsync(CurrentUser.UserId, scope.Value.OrganizationId, scope.Value.WorkspaceId, PermissionKeys.ManageEnvironments, cancellationToken);
        if (!isMember && !canManage)
        {
            return Forbidden<IReadOnlyList<EnvironmentVariableDto>>("workspace.member");
        }

        var variables = await environmentRepository.GetVariablesAsync(environmentId, cancellationToken);
        return Result<IReadOnlyList<EnvironmentVariableDto>>.Success(variables);
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

        var validationError = ValidateVariables(request);
        if (validationError is not null)
        {
            return Result<IReadOnlyList<EnvironmentVariableDto>>.Failure(validationError.Message, validationError);
        }

        var variables = await environmentRepository.UpsertVariablesAsync(environmentId, request, CurrentUser.UserId, cancellationToken);
        await RecordActivityAsync(scope.Value.OrganizationId, scope.Value.WorkspaceId, "EnvironmentVariablesChanged", "Environment", environmentId, "Environment", "Update", "Success", "Info", "Environment variables changed.", null, cancellationToken);
        return Result<IReadOnlyList<EnvironmentVariableDto>>.Success(variables, "Variables saved.");
    }

    private static ErrorDetail? ValidateVariables(UpsertEnvironmentVariablesRequest request)
    {
        var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var variable in request.Variables)
        {
            var key = variable.Key.Trim();
            if (string.IsNullOrWhiteSpace(key) || key.Length > 300 || !VariableKeyRegex.IsMatch(key))
            {
                return new ErrorDetail("environment.variable_key_invalid", "Variable names must start with a letter or underscore and contain only letters, numbers, underscore, dot, or dash.", "variables.key");
            }

            if (!keys.Add(key))
            {
                return new ErrorDetail("environment.variable_key_duplicate", $"Duplicate variable key: {key}.", "variables.key");
            }

            if (!string.Equals(variable.Scope, "Environment", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(variable.Scope, "LocalPrivate", StringComparison.OrdinalIgnoreCase))
            {
                return new ErrorDetail("environment.variable_scope_invalid", "Environment variables can only use Environment or LocalPrivate scope from this editor.", "variables.scope");
            }
        }

        return null;
    }
}
