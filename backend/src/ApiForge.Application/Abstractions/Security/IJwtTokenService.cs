using ApiForge.Application.DTOs.Auth;

namespace ApiForge.Application.Abstractions.Security;

public interface IJwtTokenService
{
    TokenPair CreateTokenPair(AuthUserDto user, Guid organizationId, Guid? workspaceId);
    string CreateRefreshToken();
}
