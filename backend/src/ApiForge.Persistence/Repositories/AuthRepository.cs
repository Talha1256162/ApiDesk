using ApiForge.Application.Abstractions.Persistence;
using ApiForge.Application.DTOs.Auth;
using ApiForge.Domain.Constants;
using ApiForge.Persistence.Connection;
using ApiForge.Persistence.Queries;
using Dapper;

namespace ApiForge.Persistence.Repositories;

public sealed class AuthRepository(ISqlConnectionFactory connectionFactory) : IAuthRepository
{
    public async Task<AuthUserDto?> GetUserByEmailAsync(string email, CancellationToken cancellationToken)
    {
        using var connection = connectionFactory.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<AuthUserDto>(
            new CommandDefinition(AuthQueries.GetUserByEmail, new { Email = email }, cancellationToken: cancellationToken));
    }

    public async Task<AuthUserDto?> GetUserByIdAsync(Guid userId, CancellationToken cancellationToken)
    {
        using var connection = connectionFactory.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<AuthUserDto>(
            new CommandDefinition(AuthQueries.GetUserById, new { UserId = userId }, cancellationToken: cancellationToken));
    }

    public async Task<string?> GetPasswordHashAsync(Guid userId, CancellationToken cancellationToken)
    {
        using var connection = connectionFactory.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<string>(
            new CommandDefinition(AuthQueries.GetPasswordHash, new { UserId = userId }, cancellationToken: cancellationToken));
    }

    public async Task<(Guid OrganizationId, Guid? WorkspaceId)?> GetDefaultTenantAsync(Guid userId, Guid? requestedOrganizationId, Guid? requestedWorkspaceId, CancellationToken cancellationToken)
    {
        using var connection = connectionFactory.CreateConnection();
        var row = await connection.QuerySingleOrDefaultAsync<(Guid OrganizationId, Guid? WorkspaceId)>(
            new CommandDefinition(AuthQueries.GetDefaultTenant, new { UserId = userId, OrganizationId = requestedOrganizationId, WorkspaceId = requestedWorkspaceId }, cancellationToken: cancellationToken));
        return row.OrganizationId == Guid.Empty ? null : row;
    }

    public async Task<(AuthUserDto User, Guid OrganizationId, Guid WorkspaceId)> RegisterWithOrganizationAsync(RegisterRequest request, string passwordHash, CancellationToken cancellationToken)
    {
        using var connection = connectionFactory.CreateConnection();
        connection.Open();
        using var transaction = connection.BeginTransaction();

        var userId = Guid.NewGuid();
        var organizationId = Guid.NewGuid();
        var workspaceId = Guid.NewGuid();
        var environmentId = Guid.NewGuid();
        var organizationSlug = $"{RepositoryUtility.Slugify(request.OrganizationName)}-{organizationId.ToString("N")[..6]}";
        var workspaceName = string.IsNullOrWhiteSpace(request.WorkspaceName) ? "Engineering" : request.WorkspaceName.Trim();
        var workspaceSlug = $"{RepositoryUtility.Slugify(workspaceName)}-{workspaceId.ToString("N")[..6]}";

        var ownerRoleId = await connection.ExecuteScalarAsync<Guid>(new CommandDefinition(
            "select top 1 id from roles where name = @Name and isDeleted = 0;",
            new { Name = RoleNames.Owner },
            transaction,
            cancellationToken: cancellationToken));

        await connection.ExecuteAsync(new CommandDefinition("""
            insert into users (id, email, passwordHash, fullName, avatarUrl, timeZone, lastActiveOn, createdOn, createdBy, isDeleted, versionNumber)
            values (@UserId, @Email, @PasswordHash, @FullName, null, 'UTC', sysutcdatetime(), sysutcdatetime(), @UserId, 0, 1);

            insert into organizations (id, name, slug, productName, createdOn, createdBy, isDeleted, versionNumber)
            values (@OrganizationId, @OrganizationName, @OrganizationSlug, coalesce(@ProductName, 'Apeiron'), sysutcdatetime(), @UserId, 0, 1);

            insert into organizationMembers (id, organizationId, userId, roleId, status, invitedByUserId, joinedOn, createdOn, createdBy, isDeleted, versionNumber)
            values (newid(), @OrganizationId, @UserId, @OwnerRoleId, 'Active', @UserId, sysutcdatetime(), sysutcdatetime(), @UserId, 0, 1);

            insert into workspaces (id, organizationId, name, slug, type, description, createdOn, createdBy, isDeleted, versionNumber)
            values (@WorkspaceId, @OrganizationId, @WorkspaceName, @WorkspaceSlug, 'Team', 'Default engineering workspace', sysutcdatetime(), @UserId, 0, 1);

            insert into workspaceMembers (id, organizationId, workspaceId, userId, roleId, status, createdOn, createdBy, isDeleted, versionNumber)
            values (newid(), @OrganizationId, @WorkspaceId, @UserId, @OwnerRoleId, 'Active', sysutcdatetime(), @UserId, 0, 1);

            insert into environments (id, organizationId, workspaceId, name, isDefault, createdOn, createdBy, isDeleted, versionNumber)
            values (@EnvironmentId, @OrganizationId, @WorkspaceId, 'Local', 1, sysutcdatetime(), @UserId, 0, 1);
            """,
            new
            {
                UserId = userId,
                request.Email,
                PasswordHash = passwordHash,
                request.FullName,
                OrganizationId = organizationId,
                OrganizationName = request.OrganizationName,
                OrganizationSlug = organizationSlug,
                ProductName = (string?)null,
                OwnerRoleId = ownerRoleId,
                WorkspaceId = workspaceId,
                WorkspaceName = workspaceName,
                WorkspaceSlug = workspaceSlug,
                EnvironmentId = environmentId
            },
            transaction,
            cancellationToken: cancellationToken));

        transaction.Commit();

        return (new AuthUserDto(userId, request.Email, request.FullName, null, "UTC"), organizationId, workspaceId);
    }

    public async Task SaveRefreshTokenAsync(Guid userId, string refreshToken, DateTime expiresOnUtc, string? ipAddress, string? userAgent, CancellationToken cancellationToken)
    {
        using var connection = connectionFactory.CreateConnection();
        await connection.ExecuteAsync(new CommandDefinition("""
            insert into refreshTokens (id, userId, tokenHash, expiresOn, ipAddress, userAgent, createdOn, createdBy, isDeleted, versionNumber)
            values (newid(), @UserId, @TokenHash, @ExpiresOn, @IpAddress, @UserAgent, sysutcdatetime(), @UserId, 0, 1);
            """,
            new { UserId = userId, TokenHash = RepositoryUtility.Sha256(refreshToken), ExpiresOn = expiresOnUtc, IpAddress = ipAddress, UserAgent = userAgent },
            cancellationToken: cancellationToken));
    }

    public async Task<(Guid UserId, Guid OrganizationId, Guid? WorkspaceId)?> GetRefreshTokenContextAsync(string refreshToken, CancellationToken cancellationToken)
    {
        using var connection = connectionFactory.CreateConnection();
        var row = await connection.QuerySingleOrDefaultAsync<(Guid UserId, Guid OrganizationId, Guid? WorkspaceId)>(
            new CommandDefinition(AuthQueries.GetRefreshTokenContext, new { TokenHash = RepositoryUtility.Sha256(refreshToken) }, cancellationToken: cancellationToken));
        return row.UserId == Guid.Empty ? null : row;
    }

    public async Task RevokeRefreshTokenAsync(string refreshToken, CancellationToken cancellationToken)
    {
        using var connection = connectionFactory.CreateConnection();
        await connection.ExecuteAsync(new CommandDefinition("""
            update refreshTokens
            set revokedOn = sysutcdatetime(), modifiedOn = sysutcdatetime()
            where tokenHash = @TokenHash and revokedOn is null;
            """,
            new { TokenHash = RepositoryUtility.Sha256(refreshToken) },
            cancellationToken: cancellationToken));
    }

    public async Task UpdateLastActiveAsync(Guid userId, CancellationToken cancellationToken)
    {
        using var connection = connectionFactory.CreateConnection();
        await connection.ExecuteAsync(new CommandDefinition("""
            update users set lastActiveOn = sysutcdatetime(), modifiedOn = sysutcdatetime(), modifiedBy = @UserId where id = @UserId;
            update organizationMembers set lastActiveOn = sysutcdatetime(), modifiedOn = sysutcdatetime(), modifiedBy = @UserId where userId = @UserId and status = 'Active';
            """,
            new { UserId = userId },
            cancellationToken: cancellationToken));
    }
}
