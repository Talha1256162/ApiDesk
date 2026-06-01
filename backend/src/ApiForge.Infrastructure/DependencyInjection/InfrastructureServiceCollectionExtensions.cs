using ApiForge.Application.Abstractions.Security;
using ApiForge.Application.Abstractions.Services;
using ApiForge.Infrastructure.Auth;
using ApiForge.Infrastructure.Http;
using ApiForge.Infrastructure.Security;
using ApiForge.Shared.Time;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ApiForge.Infrastructure.DependencyInjection;

public static class InfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructureServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<JwtOptions>(configuration.GetSection("Jwt"));
        services.AddSingleton<ISystemClock, SystemClock>();
        services.AddScoped<IPasswordHasher, BcryptPasswordHasher>();
        services.AddScoped<IJwtTokenService, JwtTokenService>();
        services.AddScoped<IHttpRequestExecutor, BackendHttpRequestExecutor>();
        return services;
    }
}
