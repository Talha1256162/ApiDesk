namespace ApiForge.Infrastructure.Auth;

public sealed class JwtOptions
{
    public string Issuer { get; init; } = "ApiForge";
    public string Audience { get; init; } = "ApiForge";
    public string SigningKey { get; init; } = "CHANGE_ME_TO_A_LONG_LOCAL_DEV_SECRET_32_CHARS";
    public int AccessTokenMinutes { get; init; } = 60;
    public int RefreshTokenDays { get; init; } = 14;
}
