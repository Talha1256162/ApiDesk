namespace ApiForge.Application.Abstractions.Services;

public interface IPermissionService
{
    Task<bool> HasPermissionAsync(Guid userId, Guid organizationId, Guid? workspaceId, string permissionKey, CancellationToken cancellationToken);
}
