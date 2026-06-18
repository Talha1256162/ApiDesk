using ApiForge.Application.Abstractions.Persistence;
using ApiForge.Persistence.Connection;
using ApiForge.Persistence.Repositories;
using Microsoft.Extensions.DependencyInjection;

namespace ApiForge.Persistence.DependencyInjection;

public static class PersistenceServiceCollectionExtensions
{
    public static IServiceCollection AddPersistenceServices(this IServiceCollection services)
    {
        services.AddSingleton<ISqlConnectionFactory, SqlConnectionFactory>();
        services.AddScoped<IAuthRepository, AuthRepository>();
        services.AddScoped<IRbacRepository, RbacRepository>();
        services.AddScoped<IOrganizationRepository, OrganizationRepository>();
        services.AddScoped<IWorkspaceRepository, WorkspaceRepository>();
        services.AddScoped<ICollectionRepository, CollectionRepository>();
        services.AddScoped<IEnvironmentRepository, EnvironmentRepository>();
        services.AddScoped<IActivityRepository, ActivityRepository>();
        services.AddScoped<IEmailDeliveryRepository, EmailDeliveryRepository>();
        services.AddScoped<IRequestRunRepository, RequestRunRepository>();
        services.AddScoped<IProductOpsRepository, ProductOpsRepository>();
        services.AddScoped<IPhase4Repository, Phase4Repository>();
        services.AddScoped<IBetaFeedbackRepository, BetaFeedbackRepository>();
        return services;
    }
}
