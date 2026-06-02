using ApiForge.Application.Abstractions.Services;
using ApiForge.Application.DTOs.Phase4;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ApiForge.Api.Controllers;

[Authorize]
[Route("api")]
public sealed class Phase4Controller(IPhase4Service phase4Service) : ApiControllerBase
{
    [HttpGet("organizations/{organizationId:guid}/saas-settings")]
    public async Task<IActionResult> GetSettings(Guid organizationId, CancellationToken cancellationToken) =>
        FromResult(await phase4Service.GetOrganizationSettingsAsync(organizationId, cancellationToken));

    [HttpPut("organizations/{organizationId:guid}/saas-settings")]
    public async Task<IActionResult> SaveSettings(Guid organizationId, SaveOrganizationSaasSettingsRequest request, CancellationToken cancellationToken) =>
        FromResult(await phase4Service.SaveOrganizationSettingsAsync(organizationId, request, cancellationToken));

    [HttpGet("organizations/{organizationId:guid}/ai-config")]
    public async Task<IActionResult> GetAiConfig(Guid organizationId, CancellationToken cancellationToken) =>
        FromResult(await phase4Service.GetAiConfigAsync(organizationId, cancellationToken));

    [HttpPut("organizations/{organizationId:guid}/ai-config")]
    public async Task<IActionResult> SaveAiConfig(Guid organizationId, SaveAiAssistantConfigRequest request, CancellationToken cancellationToken) =>
        FromResult(await phase4Service.SaveAiConfigAsync(organizationId, request, cancellationToken));

    [HttpPost("workspaces/{workspaceId:guid}/ai-assistant/actions")]
    public async Task<IActionResult> RunAiAction(Guid workspaceId, AiAssistantActionRequest request, CancellationToken cancellationToken) =>
        FromResult(await phase4Service.RunAiActionAsync(workspaceId, request, cancellationToken));

    [HttpGet("workspaces/{workspaceId:guid}/analytics/advanced")]
    public async Task<IActionResult> GetAdvancedAnalytics(Guid workspaceId, CancellationToken cancellationToken) =>
        FromResult(await phase4Service.GetAdvancedAnalyticsAsync(workspaceId, cancellationToken));

    [HttpGet("organizations/{organizationId:guid}/billing")]
    public async Task<IActionResult> GetBilling(Guid organizationId, CancellationToken cancellationToken) =>
        FromResult(await phase4Service.GetBillingOverviewAsync(organizationId, cancellationToken));

    [HttpGet("organizations/{organizationId:guid}/api-keys")]
    public async Task<IActionResult> GetApiKeys(Guid organizationId, CancellationToken cancellationToken) =>
        FromResult(await phase4Service.GetApiKeysAsync(organizationId, cancellationToken));

    [HttpPost("organizations/{organizationId:guid}/api-keys")]
    public async Task<IActionResult> CreateApiKey(Guid organizationId, CreateApiKeyRequest request, CancellationToken cancellationToken) =>
        FromResult(await phase4Service.CreateApiKeyAsync(organizationId, request, cancellationToken));
}
