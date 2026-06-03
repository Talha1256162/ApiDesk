using ApiForge.Application.Abstractions.Persistence;
using ApiForge.Application.Abstractions.Services;

namespace ApiForge.Application.Services;

public sealed class PermissionService(IRbacRepository rbacRepository) : IPermissionService
{
    public Task<bool> HasPermissionAsync(Guid userId, Guid organizationId, Guid? workspaceId, string permissionKey, CancellationToken cancellationToken)
    {
        return rbacRepository.HasPermissionAsync(userId, organizationId, workspaceId, permissionKey, cancellationToken);
    }

    public Task<bool> IsOrganizationMemberAsync(Guid userId, Guid organizationId, CancellationToken cancellationToken)
    {
        return rbacRepository.IsOrganizationMemberAsync(userId, organizationId, cancellationToken);
    }

    public Task<bool> IsWorkspaceMemberAsync(Guid userId, Guid organizationId, Guid workspaceId, CancellationToken cancellationToken)
    {
        return rbacRepository.IsWorkspaceMemberAsync(userId, organizationId, workspaceId, cancellationToken);
    }
}
