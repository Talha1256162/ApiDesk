using ApiForge.Application.DTOs.Phase4;

namespace ApiForge.Application.Abstractions.Persistence;

public interface IPhase4Repository
{
    Task<OrganizationSaasSettingsDto?> GetOrganizationSettingsAsync(Guid organizationId, CancellationToken cancellationToken);
    Task<OrganizationSaasSettingsDto> SaveOrganizationSettingsAsync(Guid organizationId, SaveOrganizationSaasSettingsRequest request, Guid userId, CancellationToken cancellationToken);
    Task<AiAssistantConfigDto?> GetAiConfigAsync(Guid organizationId, CancellationToken cancellationToken);
    Task<AiAssistantConfigDto> SaveAiConfigAsync(Guid organizationId, SaveAiAssistantConfigRequest request, Guid userId, CancellationToken cancellationToken);
    Task<AdvancedAnalyticsDto> GetAdvancedAnalyticsAsync(Guid workspaceId, CancellationToken cancellationToken);
    Task<BillingOverviewDto> GetBillingOverviewAsync(Guid organizationId, CancellationToken cancellationToken);
    Task<IReadOnlyList<ApiKeyDto>> GetApiKeysAsync(Guid organizationId, CancellationToken cancellationToken);
    Task<ApiKeyDto> CreateApiKeyAsync(Guid organizationId, CreateApiKeyRequest request, string keyHash, Guid userId, CancellationToken cancellationToken);
}
