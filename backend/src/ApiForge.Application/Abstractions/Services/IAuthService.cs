using ApiForge.Application.DTOs.Auth;
using ApiForge.Shared.Responses;

namespace ApiForge.Application.Abstractions.Services;

public interface IAuthService
{
    Task<Result<AuthResponse>> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken);
    Task<Result<AuthResponse>> LoginAsync(LoginRequest request, CancellationToken cancellationToken);
    Task<Result<AuthResponse>> RefreshAsync(RefreshTokenRequest request, CancellationToken cancellationToken);
    Task<Result> LogoutAsync(LogoutRequest request, CancellationToken cancellationToken);
}
