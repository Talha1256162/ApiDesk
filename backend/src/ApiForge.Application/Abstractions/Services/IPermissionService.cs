namespace ApiForge.Application.Abstractions.Services;

public interface IPermissionService
{
    Task<bool> HasPermissionAsync(Guid userId, Guid organizationId, Guid? workspaceId, string permissionKey, CancellationToken cancellationToken);
    Task<bool> IsOrganizationMemberAsync(Guid userId, Guid organizationId, CancellationToken cancellationToken);
    Task<bool> IsWorkspaceMemberAsync(Guid userId, Guid organizationId, Guid workspaceId, CancellationToken cancellationToken);
}
