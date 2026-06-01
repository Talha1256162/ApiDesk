using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace ApiForge.Api.SignalR;

[Authorize]
public sealed class CollaborationHub : Hub
{
    public Task JoinWorkspace(Guid workspaceId)
    {
        return Groups.AddToGroupAsync(Context.ConnectionId, WorkspaceGroup(workspaceId));
    }

    public Task LeaveWorkspace(Guid workspaceId)
    {
        return Groups.RemoveFromGroupAsync(Context.ConnectionId, WorkspaceGroup(workspaceId));
    }

    public Task NotifyEditing(Guid workspaceId, Guid requestId, string displayName)
    {
        return Clients.OthersInGroup(WorkspaceGroup(workspaceId))
            .SendAsync("requestEditing", new { workspaceId, requestId, displayName });
    }

    private static string WorkspaceGroup(Guid workspaceId) => $"workspace:{workspaceId}";
}
