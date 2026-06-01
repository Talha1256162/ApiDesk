using ApiForge.Application.Abstractions.Persistence;
using ApiForge.Persistence.Connection;
using ApiForge.Persistence.Queries;
using Dapper;

namespace ApiForge.Persistence.Repositories;

public sealed class RbacRepository(ISqlConnectionFactory connectionFactory) : IRbacRepository
{
    public async Task<bool> HasPermissionAsync(Guid userId, Guid organizationId, Guid? workspaceId, string permissionKey, CancellationToken cancellationToken)
    {
        using var connection = connectionFactory.CreateConnection();
        return await connection.ExecuteScalarAsync<bool>(
            new CommandDefinition(RbacQueries.HasPermission, new { UserId = userId, OrganizationId = organizationId, WorkspaceId = workspaceId, PermissionKey = permissionKey }, cancellationToken: cancellationToken));
    }
}
