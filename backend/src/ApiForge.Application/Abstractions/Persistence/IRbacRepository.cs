namespace ApiForge.Application.Abstractions.Persistence;

public interface IRbacRepository
{
    Task<bool> HasPermissionAsync(Guid userId, Guid organizationId, Guid? workspaceId, string permissionKey, CancellationToken cancellationToken);
}
