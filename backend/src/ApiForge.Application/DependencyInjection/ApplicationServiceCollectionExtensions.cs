using ApiForge.Application.Abstractions.Services;
using ApiForge.Application.Services;
using Microsoft.Extensions.DependencyInjection;

namespace ApiForge.Application.DependencyInjection;

public static class ApplicationServiceCollectionExtensions
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IPermissionService, PermissionService>();
        services.AddScoped<IOrganizationService, OrganizationService>();
        services.AddScoped<IWorkspaceService, WorkspaceService>();
        services.AddScoped<ICollectionService, CollectionService>();
        services.AddScoped<IEnvironmentService, EnvironmentService>();
        services.AddScoped<IRequestRunnerService, RequestRunnerService>();
        services.AddScoped<IActivityService, ActivityService>();
        services.AddScoped<IProductOpsService, ProductOpsService>();
        services.AddScoped<IPhase4Service, Phase4Service>();
        return services;
    }
}
