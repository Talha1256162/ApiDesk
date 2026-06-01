namespace ApiForge.Application.DTOs.Auth;

public sealed record RegisterRequest(string Email, string Password, string FullName, string OrganizationName, string? WorkspaceName);
public sealed record LoginRequest(string Email, string Password, Guid? OrganizationId = null, Guid? WorkspaceId = null);
public sealed record RefreshTokenRequest(string RefreshToken);
public sealed record LogoutRequest(string RefreshToken);

public sealed record AuthUserDto(Guid Id, string Email, string FullName, string? AvatarUrl, string? TimeZone);

public sealed record TokenPair(string AccessToken, string RefreshToken, DateTime AccessTokenExpiresOnUtc, DateTime RefreshTokenExpiresOnUtc);

public sealed record AuthResponse(
    AuthUserDto User,
    Guid OrganizationId,
    Guid? WorkspaceId,
    string AccessToken,
    string RefreshToken,
    DateTime AccessTokenExpiresOnUtc,
    DateTime RefreshTokenExpiresOnUtc);
