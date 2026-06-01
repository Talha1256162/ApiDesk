using ApiForge.Application.DTOs.Auth;

namespace ApiForge.Application.Abstractions.Persistence;

public interface IAuthRepository
{
    Task<AuthUserDto?> GetUserByEmailAsync(string email, CancellationToken cancellationToken);
    Task<AuthUserDto?> GetUserByIdAsync(Guid userId, CancellationToken cancellationToken);
    Task<string?> GetPasswordHashAsync(Guid userId, CancellationToken cancellationToken);
    Task<(Guid OrganizationId, Guid? WorkspaceId)?> GetDefaultTenantAsync(Guid userId, Guid? requestedOrganizationId, Guid? requestedWorkspaceId, CancellationToken cancellationToken);
    Task<(AuthUserDto User, Guid OrganizationId, Guid WorkspaceId)> RegisterWithOrganizationAsync(RegisterRequest request, string passwordHash, CancellationToken cancellationToken);
    Task SaveRefreshTokenAsync(Guid userId, string refreshToken, DateTime expiresOnUtc, string? ipAddress, string? userAgent, CancellationToken cancellationToken);
    Task<(Guid UserId, Guid OrganizationId, Guid? WorkspaceId)?> GetRefreshTokenContextAsync(string refreshToken, CancellationToken cancellationToken);
    Task RevokeRefreshTokenAsync(string refreshToken, CancellationToken cancellationToken);
    Task UpdateLastActiveAsync(Guid userId, CancellationToken cancellationToken);
}
