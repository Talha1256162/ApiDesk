using ApiForge.Application.DTOs.Phase4;
using ApiForge.Shared.Responses;

namespace ApiForge.Application.Abstractions.Services;

public interface IPhase4Service
{
    Task<Result<OrganizationSaasSettingsDto>> GetOrganizationSettingsAsync(Guid organizationId, CancellationToken cancellationToken);
    Task<Result<OrganizationSaasSettingsDto>> SaveOrganizationSettingsAsync(Guid organizationId, SaveOrganizationSaasSettingsRequest request, CancellationToken cancellationToken);
    Task<Result<AiAssistantConfigDto>> GetAiConfigAsync(Guid organizationId, CancellationToken cancellationToken);
    Task<Result<AiAssistantConfigDto>> SaveAiConfigAsync(Guid organizationId, SaveAiAssistantConfigRequest request, CancellationToken cancellationToken);
    Task<Result<AiProviderStatusDto>> GetAiProviderStatusAsync(Guid organizationId, CancellationToken cancellationToken);
    Task<Result<AiAssistantActionDto>> RunAiActionAsync(Guid workspaceId, AiAssistantActionRequest request, CancellationToken cancellationToken);
    Task<Result<AdvancedAnalyticsDto>> GetAdvancedAnalyticsAsync(Guid workspaceId, CancellationToken cancellationToken);
    Task<Result<BillingOverviewDto>> GetBillingOverviewAsync(Guid organizationId, CancellationToken cancellationToken);
    Task<Result<IReadOnlyList<ApiKeyDto>>> GetApiKeysAsync(Guid organizationId, CancellationToken cancellationToken);
    Task<Result<CreatedApiKeyDto>> CreateApiKeyAsync(Guid organizationId, CreateApiKeyRequest request, CancellationToken cancellationToken);
}
