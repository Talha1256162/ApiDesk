using System.Security.Claims;
using ApiForge.Application.Abstractions.Persistence;
using ApiForge.Application.Abstractions.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace ApiForge.Api.SignalR;

[Authorize]
public sealed class CollaborationHub(IWorkspaceRepository workspaceRepository, IPermissionService permissionService) : Hub
{
    public async Task JoinWorkspace(Guid workspaceId)
    {
        if (!await CanAccessWorkspaceAsync(workspaceId))
        {
            return;
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, WorkspaceGroup(workspaceId));
    }

    public Task LeaveWorkspace(Guid workspaceId)
    {
        return Groups.RemoveFromGroupAsync(Context.ConnectionId, WorkspaceGroup(workspaceId));
    }

    public async Task NotifyEditing(Guid workspaceId, Guid requestId, string displayName)
    {
        if (!await CanAccessWorkspaceAsync(workspaceId))
        {
            return;
        }

        await Clients.OthersInGroup(WorkspaceGroup(workspaceId))
            .SendAsync("requestEditing", new { workspaceId, requestId, displayName });
    }

    public static string WorkspaceGroup(Guid workspaceId) => $"workspace:{workspaceId}";

    private async Task<bool> CanAccessWorkspaceAsync(Guid workspaceId)
    {
        var rawUserId = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? Context.User?.FindFirstValue("sub");
        if (!Guid.TryParse(rawUserId, out var userId))
        {
            return false;
        }

        var organizationId = await workspaceRepository.GetOrganizationIdAsync(workspaceId, Context.ConnectionAborted);
        return organizationId is not null
            && await permissionService.IsWorkspaceMemberAsync(userId, organizationId.Value, workspaceId, Context.ConnectionAborted);
    }
}
