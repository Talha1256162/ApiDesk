using ApiForge.Application.Abstractions.Auth;
using ApiForge.Application.Abstractions.Persistence;
using ApiForge.Application.Abstractions.Services;
using ApiForge.Application.DTOs.Workspaces;
using ApiForge.Domain.Constants;
using ApiForge.Shared.Pagination;
using ApiForge.Shared.Responses;

namespace ApiForge.Application.Services;

public sealed class WorkspaceService(
    IWorkspaceRepository workspaceRepository,
    IPermissionService permissionService,
    ICurrentUserContext currentUserContext,
    IActivityRepository activityRepository) : ServiceBase(currentUserContext, activityRepository), IWorkspaceService
{
    public async Task<Result<PagedResult<WorkspaceDto>>> GetWorkspacesAsync(Guid organizationId, PagedRequest request, CancellationToken cancellationToken)
    {
        if (CurrentUser is null)
        {
            return Unauthorized<PagedResult<WorkspaceDto>>();
        }

        if (!await permissionService.IsOrganizationMemberAsync(CurrentUser.UserId, organizationId, cancellationToken))
        {
            return Forbidden<PagedResult<WorkspaceDto>>("organization.member");
        }

        var workspaces = await workspaceRepository.GetByOrganizationAsync(organizationId, request, cancellationToken);
        return Result<PagedResult<WorkspaceDto>>.Success(workspaces);
    }

    public async Task<Result<WorkspaceDto>> CreateAsync(CreateWorkspaceRequest request, CancellationToken cancellationToken)
    {
        if (CurrentUser is null)
        {
            return Unauthorized<WorkspaceDto>();
        }

        var allowed = await permissionService.HasPermissionAsync(CurrentUser.UserId, request.OrganizationId, null, PermissionKeys.CreateWorkspace, cancellationToken);
        if (!allowed)
        {
            return Forbidden<WorkspaceDto>(PermissionKeys.CreateWorkspace);
        }

        var workspace = await workspaceRepository.CreateAsync(request, CurrentUser.UserId, cancellationToken);
        await RecordActivityAsync(request.OrganizationId, workspace.Id, "WorkspaceCreated", "Workspace", workspace.Id, workspace.Name, "Create", "Success", "Info", "Workspace created.", null, cancellationToken);
        return Result<WorkspaceDto>.Success(workspace, "Workspace created.");
    }

    public async Task<Result<WorkspaceDto>> UpdateAsync(Guid workspaceId, UpdateWorkspaceRequest request, CancellationToken cancellationToken)
    {
        if (CurrentUser is null)
        {
            return Unauthorized<WorkspaceDto>();
        }

        var organizationId = await workspaceRepository.GetOrganizationIdAsync(workspaceId, cancellationToken);
        if (organizationId is null)
        {
            return Result<WorkspaceDto>.Failure("Workspace was not found.", new ErrorDetail("workspace.not_found", "Workspace was not found."));
        }

        var allowed = await permissionService.HasPermissionAsync(CurrentUser.UserId, organizationId.Value, workspaceId, PermissionKeys.EditWorkspace, cancellationToken);
        if (!allowed)
        {
            return Forbidden<WorkspaceDto>(PermissionKeys.EditWorkspace);
        }

        var workspace = await workspaceRepository.UpdateAsync(workspaceId, request, CurrentUser.UserId, cancellationToken);
        if (workspace is null)
        {
            return Result<WorkspaceDto>.Failure("Workspace update failed. It may have changed since you loaded it.", new ErrorDetail("workspace.version_conflict", "Version conflict or missing workspace."));
        }

        await RecordActivityAsync(organizationId.Value, workspaceId, "WorkspaceUpdated", "Workspace", workspaceId, workspace.Name, "Update", "Success", "Info", "Workspace updated.", null, cancellationToken);
        return Result<WorkspaceDto>.Success(workspace, "Workspace updated.");
    }

    public async Task<Result> DeleteAsync(Guid workspaceId, CancellationToken cancellationToken)
    {
        if (CurrentUser is null)
        {
            return Unauthorized();
        }

        var organizationId = await workspaceRepository.GetOrganizationIdAsync(workspaceId, cancellationToken);
        if (organizationId is null)
        {
            return Result.Failure("Workspace was not found.", new ErrorDetail("workspace.not_found", "Workspace was not found."));
        }

        var allowed = await permissionService.HasPermissionAsync(CurrentUser.UserId, organizationId.Value, workspaceId, PermissionKeys.DeleteWorkspace, cancellationToken);
        if (!allowed)
        {
            return Forbidden(PermissionKeys.DeleteWorkspace);
        }

        var deleted = await workspaceRepository.DeleteAsync(workspaceId, CurrentUser.UserId, cancellationToken);
        if (!deleted)
        {
            return Result.Failure("Workspace was not found.", new ErrorDetail("workspace.not_found", "Workspace was not found."));
        }

        await RecordActivityAsync(organizationId.Value, workspaceId, "WorkspaceDeleted", "Workspace", workspaceId, "Workspace", "Delete", "Success", "Warning", "Workspace deleted.", null, cancellationToken);
        return Result.Success("Workspace deleted.");
    }

    public async Task<Result<WorkspaceDashboardDto>> GetDashboardAsync(Guid workspaceId, CancellationToken cancellationToken)
    {
        if (CurrentUser is null)
        {
            return Unauthorized<WorkspaceDashboardDto>();
        }

        var organizationId = await workspaceRepository.GetOrganizationIdAsync(workspaceId, cancellationToken);
        if (organizationId is null)
        {
            return Result<WorkspaceDashboardDto>.Failure("Workspace was not found.", new ErrorDetail("workspace.not_found", "Workspace was not found."));
        }

        if (!await permissionService.IsWorkspaceMemberAsync(CurrentUser.UserId, organizationId.Value, workspaceId, cancellationToken))
        {
            return Forbidden<WorkspaceDashboardDto>("workspace.member");
        }

        var dashboard = await workspaceRepository.GetDashboardAsync(workspaceId, cancellationToken);
        return Result<WorkspaceDashboardDto>.Success(dashboard);
    }
}
