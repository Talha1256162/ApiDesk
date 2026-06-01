using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using ApiForge.Application.Abstractions.Security;
using ApiForge.Application.DTOs.Auth;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace ApiForge.Infrastructure.Auth;

public sealed class JwtTokenService(IOptions<JwtOptions> options) : IJwtTokenService
{
    private readonly JwtOptions _options = options.Value;

    public TokenPair CreateTokenPair(AuthUserDto user, Guid organizationId, Guid? workspaceId)
    {
        var now = DateTime.UtcNow;
        var expiresOn = now.AddMinutes(_options.AccessTokenMinutes);
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.SigningKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email),
            new("name", user.FullName),
            new("organizationId", organizationId.ToString())
        };

        if (workspaceId.HasValue)
        {
            claims.Add(new Claim("workspaceId", workspaceId.Value.ToString()));
        }

        var token = new JwtSecurityToken(
            issuer: _options.Issuer,
            audience: _options.Audience,
            claims: claims,
            notBefore: now,
            expires: expiresOn,
            signingCredentials: credentials);

        return new TokenPair(
            new JwtSecurityTokenHandler().WriteToken(token),
            CreateRefreshToken(),
            expiresOn,
            now.AddDays(_options.RefreshTokenDays));
    }

    public string CreateRefreshToken()
    {
        return Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
    }
}
