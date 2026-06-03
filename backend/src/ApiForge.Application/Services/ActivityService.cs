using ApiForge.Application.Abstractions.Auth;
using ApiForge.Application.Abstractions.Persistence;
using ApiForge.Application.Abstractions.Services;
using ApiForge.Application.DTOs.Activity;
using ApiForge.Domain.Constants;
using ApiForge.Shared.Pagination;
using ApiForge.Shared.Responses;

namespace ApiForge.Application.Services;

public sealed class ActivityService(
    IActivityRepository activityRepository,
    IPermissionService permissionService,
    IWorkspaceRepository workspaceRepository,
    ICurrentUserContext currentUserContext) : IActivityService
{
    public async Task<Result<PagedResult<ActivityEventDto>>> GetActivityAsync(ActivityFilterRequest request, CancellationToken cancellationToken)
    {
        if (currentUserContext.User is null)
        {
            return Result<PagedResult<ActivityEventDto>>.Failure("Authentication is required.", new ErrorDetail("auth.required", "Authentication is required."));
        }

        var allowed = await permissionService.HasPermissionAsync(currentUserContext.User.UserId, request.OrganizationId, request.WorkspaceId, PermissionKeys.ViewTeamActivity, cancellationToken);
        if (!allowed)
        {
            return Result<PagedResult<ActivityEventDto>>.Failure("You do not have permission to view team activity.", new ErrorDetail("permission.denied", $"Missing permission: {PermissionKeys.ViewTeamActivity}."));
        }

        var activity = await activityRepository.GetActivityAsync(request, cancellationToken);
        return Result<PagedResult<ActivityEventDto>>.Success(activity);
    }

    public async Task<Result<ManagerSummaryDto>> GetManagerSummaryAsync(Guid workspaceId, CancellationToken cancellationToken)
    {
        if (currentUserContext.User is null)
        {
            return Result<ManagerSummaryDto>.Failure("Authentication is required.", new ErrorDetail("auth.required", "Authentication is required."));
        }

        var organizationId = await workspaceRepository.GetOrganizationIdAsync(workspaceId, cancellationToken);
        if (organizationId is null)
        {
            return Result<ManagerSummaryDto>.Failure("Workspace was not found.", new ErrorDetail("workspace.not_found", "Workspace was not found."));
        }

        var allowed = await permissionService.IsWorkspaceMemberAsync(currentUserContext.User.UserId, organizationId.Value, workspaceId, cancellationToken);
        if (!allowed)
        {
            return Result<ManagerSummaryDto>.Failure("You do not have access to this workspace.", new ErrorDetail("workspace.access_denied", "You do not have access to this workspace."));
        }

        var summary = await activityRepository.GetManagerSummaryAsync(workspaceId, cancellationToken);
        return Result<ManagerSummaryDto>.Success(summary);
    }

    public async Task<Result<PagedResult<AuditLogDto>>> GetAuditLogsAsync(ActivityFilterRequest request, CancellationToken cancellationToken)
    {
        if (currentUserContext.User is null)
        {
            return Result<PagedResult<AuditLogDto>>.Failure("Authentication is required.", new ErrorDetail("auth.required", "Authentication is required."));
        }

        var allowed = await permissionService.HasPermissionAsync(currentUserContext.User.UserId, request.OrganizationId, request.WorkspaceId, PermissionKeys.ViewAuditLogs, cancellationToken);
        if (!allowed)
        {
            return Result<PagedResult<AuditLogDto>>.Failure("You do not have permission to view audit logs.", new ErrorDetail("permission.denied", $"Missing permission: {PermissionKeys.ViewAuditLogs}."));
        }

        return Result<PagedResult<AuditLogDto>>.Success(await activityRepository.GetAuditLogsAsync(request, cancellationToken));
    }

    public async Task<Result<string>> ExportActivityCsvAsync(ActivityFilterRequest request, CancellationToken cancellationToken)
    {
        var result = await GetActivityAsync(request with { Offset = 0, Count = 500 }, cancellationToken);
        if (!result.Succeeded || result.Data is null)
        {
            return Result<string>.Failure(result.Message, result.Errors.ToArray());
        }

        var rows = new List<string> { "CreatedOn,Actor,Email,EventType,EntityType,EntityName,Status,Severity,Summary" };
        rows.AddRange(result.Data.Items.Select(item => string.Join(',', Csv(item.CreatedOn.ToString("O")), Csv(item.ActorName), Csv(item.ActorEmail), Csv(item.EventType), Csv(item.EntityType), Csv(item.EntityName), Csv(item.Status), Csv(item.Severity), Csv(item.Summary))));
        return Result<string>.Success(string.Join(Environment.NewLine, rows), "Activity CSV exported.");
    }

    public async Task<Result<string>> ExportAuditCsvAsync(ActivityFilterRequest request, CancellationToken cancellationToken)
    {
        var result = await GetAuditLogsAsync(request with { Offset = 0, Count = 500 }, cancellationToken);
        if (!result.Succeeded || result.Data is null)
        {
            return Result<string>.Failure(result.Message, result.Errors.ToArray());
        }

        var rows = new List<string> { "CreatedOn,Actor,Email,EventType,EntityType,EntityName,Action,Severity,CorrelationId" };
        rows.AddRange(result.Data.Items.Select(item => string.Join(',', Csv(item.CreatedOn.ToString("O")), Csv(item.ActorName), Csv(item.ActorEmail), Csv(item.EventType), Csv(item.EntityType), Csv(item.EntityName), Csv(item.Action), Csv(item.Severity), Csv(item.CorrelationId))));
        return Result<string>.Success(string.Join(Environment.NewLine, rows), "Audit CSV exported.");
    }

    private static string Csv(string? value)
    {
        var safe = (value ?? string.Empty).Replace("\"", "\"\"");
        return $"\"{safe}\"";
    }
}
