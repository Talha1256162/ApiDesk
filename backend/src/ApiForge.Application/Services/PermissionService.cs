using ApiForge.Application.Abstractions.Persistence;
using ApiForge.Application.Abstractions.Services;

namespace ApiForge.Application.Services;

public sealed class PermissionService(IRbacRepository rbacRepository) : IPermissionService
{
    public Task<bool> HasPermissionAsync(Guid userId, Guid organizationId, Guid? workspaceId, string permissionKey, CancellationToken cancellationToken)
    {
        return rbacRepository.HasPermissionAsync(userId, organizationId, workspaceId, permissionKey, cancellationToken);
    }
}
