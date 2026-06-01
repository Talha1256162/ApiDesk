using ApiForge.Application.Abstractions.Auth;
using ApiForge.Application.Abstractions.Persistence;
using ApiForge.Application.Abstractions.Security;
using ApiForge.Application.Abstractions.Services;
using ApiForge.Application.DTOs.Auth;
using ApiForge.Shared.Responses;

namespace ApiForge.Application.Services;

public sealed class AuthService(
    IAuthRepository authRepository,
    IPasswordHasher passwordHasher,
    IJwtTokenService jwtTokenService,
    ICurrentUserContext currentUserContext) : IAuthService
{
    public async Task<Result<AuthResponse>> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password) || string.IsNullOrWhiteSpace(request.FullName) || string.IsNullOrWhiteSpace(request.OrganizationName))
        {
            return Result<AuthResponse>.Failure("Registration data is invalid.", new ErrorDetail("validation.invalid", "Email, password, full name, and organization name are required."));
        }

        var existing = await authRepository.GetUserByEmailAsync(request.Email.Trim(), cancellationToken);
        if (existing is not null)
        {
            return Result<AuthResponse>.Failure("Email is already registered.", new ErrorDetail("auth.email_exists", "A user with this email already exists.", nameof(request.Email)));
        }

        var passwordHash = passwordHasher.Hash(request.Password);
        var registered = await authRepository.RegisterWithOrganizationAsync(request, passwordHash, cancellationToken);
        var tokenPair = jwtTokenService.CreateTokenPair(registered.User, registered.OrganizationId, registered.WorkspaceId);

        await authRepository.SaveRefreshTokenAsync(registered.User.Id, tokenPair.RefreshToken, tokenPair.RefreshTokenExpiresOnUtc, currentUserContext.IpAddress, currentUserContext.UserAgent, cancellationToken);

        return Result<AuthResponse>.Success(new AuthResponse(
            registered.User,
            registered.OrganizationId,
            registered.WorkspaceId,
            tokenPair.AccessToken,
            tokenPair.RefreshToken,
            tokenPair.AccessTokenExpiresOnUtc,
            tokenPair.RefreshTokenExpiresOnUtc), "Registration completed.");
    }

    public async Task<Result<AuthResponse>> LoginAsync(LoginRequest request, CancellationToken cancellationToken)
    {
        var user = await authRepository.GetUserByEmailAsync(request.Email.Trim(), cancellationToken);
        if (user is null)
        {
            return Result<AuthResponse>.Failure("Invalid email or password.", new ErrorDetail("auth.invalid_credentials", "Invalid email or password."));
        }

        var passwordHash = await authRepository.GetPasswordHashAsync(user.Id, cancellationToken);
        if (passwordHash is null || !passwordHasher.Verify(request.Password, passwordHash))
        {
            return Result<AuthResponse>.Failure("Invalid email or password.", new ErrorDetail("auth.invalid_credentials", "Invalid email or password."));
        }

        var tenant = await authRepository.GetDefaultTenantAsync(user.Id, request.OrganizationId, request.WorkspaceId, cancellationToken);
        if (tenant is null)
        {
            return Result<AuthResponse>.Failure("No active organization membership found.", new ErrorDetail("auth.no_tenant", "No active organization membership found."));
        }

        var tokenPair = jwtTokenService.CreateTokenPair(user, tenant.Value.OrganizationId, tenant.Value.WorkspaceId);
        await authRepository.SaveRefreshTokenAsync(user.Id, tokenPair.RefreshToken, tokenPair.RefreshTokenExpiresOnUtc, currentUserContext.IpAddress, currentUserContext.UserAgent, cancellationToken);
        await authRepository.UpdateLastActiveAsync(user.Id, cancellationToken);

        return Result<AuthResponse>.Success(new AuthResponse(
            user,
            tenant.Value.OrganizationId,
            tenant.Value.WorkspaceId,
            tokenPair.AccessToken,
            tokenPair.RefreshToken,
            tokenPair.AccessTokenExpiresOnUtc,
            tokenPair.RefreshTokenExpiresOnUtc));
    }

    public async Task<Result<AuthResponse>> RefreshAsync(RefreshTokenRequest request, CancellationToken cancellationToken)
    {
        var context = await authRepository.GetRefreshTokenContextAsync(request.RefreshToken, cancellationToken);
        if (context is null)
        {
            return Result<AuthResponse>.Failure("Refresh token is invalid or expired.", new ErrorDetail("auth.refresh_invalid", "Refresh token is invalid or expired."));
        }

        var user = await authRepository.GetUserByIdAsync(context.Value.UserId, cancellationToken);
        if (user is null)
        {
            return Result<AuthResponse>.Failure("User was not found.", new ErrorDetail("auth.user_not_found", "User was not found."));
        }

        await authRepository.RevokeRefreshTokenAsync(request.RefreshToken, cancellationToken);
        var tokenPair = jwtTokenService.CreateTokenPair(user, context.Value.OrganizationId, context.Value.WorkspaceId);
        await authRepository.SaveRefreshTokenAsync(user.Id, tokenPair.RefreshToken, tokenPair.RefreshTokenExpiresOnUtc, currentUserContext.IpAddress, currentUserContext.UserAgent, cancellationToken);

        return Result<AuthResponse>.Success(new AuthResponse(
            user,
            context.Value.OrganizationId,
            context.Value.WorkspaceId,
            tokenPair.AccessToken,
            tokenPair.RefreshToken,
            tokenPair.AccessTokenExpiresOnUtc,
            tokenPair.RefreshTokenExpiresOnUtc));
    }

    public async Task<Result> LogoutAsync(LogoutRequest request, CancellationToken cancellationToken)
    {
        await authRepository.RevokeRefreshTokenAsync(request.RefreshToken, cancellationToken);
        return Result.Success("Logged out.");
    }
}
