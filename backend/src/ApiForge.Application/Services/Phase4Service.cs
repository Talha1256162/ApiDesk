using System.Security.Cryptography;
using System.Text;
using ApiForge.Application.Abstractions.Auth;
using ApiForge.Application.Abstractions.Persistence;
using ApiForge.Application.Abstractions.Services;
using ApiForge.Application.DTOs.Phase4;
using ApiForge.Domain.Constants;
using ApiForge.Shared.Responses;

namespace ApiForge.Application.Services;

public sealed class Phase4Service(
    IPhase4Repository phase4Repository,
    ICollectionRepository collectionRepository,
    IPermissionService permissionService,
    IAiProviderService aiProviderService,
    ICurrentUserContext currentUserContext,
    IActivityRepository activityRepository) : ServiceBase(currentUserContext, activityRepository), IPhase4Service
{
    public async Task<Result<OrganizationSaasSettingsDto>> GetOrganizationSettingsAsync(Guid organizationId, CancellationToken cancellationToken)
    {
        if (!await HasOrgPermissionAsync(organizationId, PermissionKeys.ManageBillingSettings, cancellationToken))
            return CurrentUser is null ? Unauthorized<OrganizationSaasSettingsDto>() : Forbidden<OrganizationSaasSettingsDto>(PermissionKeys.ManageBillingSettings);
        var settings = await phase4Repository.GetOrganizationSettingsAsync(organizationId, cancellationToken);
        return settings is null ? Result<OrganizationSaasSettingsDto>.Failure("Organization was not found.", new ErrorDetail("organization.not_found", "Organization was not found.")) : Result<OrganizationSaasSettingsDto>.Success(settings);
    }

    public async Task<Result<OrganizationSaasSettingsDto>> SaveOrganizationSettingsAsync(Guid organizationId, SaveOrganizationSaasSettingsRequest request, CancellationToken cancellationToken)
    {
        if (CurrentUser is null) return Unauthorized<OrganizationSaasSettingsDto>();
        if (!await HasOrgPermissionAsync(organizationId, PermissionKeys.ManageBillingSettings, cancellationToken))
            return Forbidden<OrganizationSaasSettingsDto>(PermissionKeys.ManageBillingSettings);
        if (string.IsNullOrWhiteSpace(request.ProductName))
            return Result<OrganizationSaasSettingsDto>.Failure("Product name is required.", new ErrorDetail("settings.product_required", "Product name is required."));
        var settings = await phase4Repository.SaveOrganizationSettingsAsync(organizationId, request, CurrentUser.UserId, cancellationToken);
        await RecordActivityAsync(organizationId, null, "SaasSettingsChanged", "Organization", organizationId, settings.ProductName, "Update", "Success", "Info", "SaaS settings changed.", null, cancellationToken);
        return Result<OrganizationSaasSettingsDto>.Success(settings, "Settings saved.");
    }

    public async Task<Result<AiAssistantConfigDto>> GetAiConfigAsync(Guid organizationId, CancellationToken cancellationToken)
    {
        if (!await HasOrgPermissionAsync(organizationId, PermissionKeys.ManageBillingSettings, cancellationToken))
            return CurrentUser is null ? Unauthorized<AiAssistantConfigDto>() : Forbidden<AiAssistantConfigDto>(PermissionKeys.ManageBillingSettings);
        var config = await phase4Repository.GetAiConfigAsync(organizationId, cancellationToken)
            ?? new AiAssistantConfigDto(Guid.Empty, organizationId, "OpenAI", null, null, null, false, DateTime.UtcNow, null);
        return Result<AiAssistantConfigDto>.Success(config);
    }

    public async Task<Result<AiAssistantConfigDto>> SaveAiConfigAsync(Guid organizationId, SaveAiAssistantConfigRequest request, CancellationToken cancellationToken)
    {
        if (CurrentUser is null) return Unauthorized<AiAssistantConfigDto>();
        if (!await HasOrgPermissionAsync(organizationId, PermissionKeys.ManageBillingSettings, cancellationToken))
            return Forbidden<AiAssistantConfigDto>(PermissionKeys.ManageBillingSettings);
        var allowedProviders = new[] { "OpenAI", "Azure OpenAI", "Local LLM" };
        if (!allowedProviders.Contains(request.Provider))
            return Result<AiAssistantConfigDto>.Failure("AI provider is not supported.", new ErrorDetail("ai.provider_invalid", "AI provider is not supported."));
        var config = await phase4Repository.SaveAiConfigAsync(organizationId, request, CurrentUser.UserId, cancellationToken);
        await RecordActivityAsync(organizationId, null, "AiConfigChanged", "AiAssistantConfig", config.Id, config.Provider, "Update", "Success", "Info", "AI assistant provider configuration changed. API keys are not stored here.", null, cancellationToken);
        return Result<AiAssistantConfigDto>.Success(config, "AI configuration saved.");
    }

    public async Task<Result<AiProviderStatusDto>> GetAiProviderStatusAsync(Guid organizationId, CancellationToken cancellationToken)
    {
        if (CurrentUser is null) return Unauthorized<AiProviderStatusDto>();
        if (!await permissionService.IsOrganizationMemberAsync(CurrentUser.UserId, organizationId, cancellationToken))
            return Forbidden<AiProviderStatusDto>("organization.membership_required");

        return Result<AiProviderStatusDto>.Success(aiProviderService.GetStatus());
    }

    public async Task<Result<AiAssistantActionDto>> RunAiActionAsync(Guid workspaceId, AiAssistantActionRequest request, CancellationToken cancellationToken)
    {
        if (CurrentUser is null) return Unauthorized<AiAssistantActionDto>();
        var orgId = await collectionRepository.GetWorkspaceOrganizationIdAsync(workspaceId, cancellationToken);
        if (orgId is null) return Result<AiAssistantActionDto>.Failure("Workspace was not found.", new ErrorDetail("workspace.not_found", "Workspace was not found."));
        if (!await HasWorkspacePermissionAsync(CurrentUser.UserId, orgId.Value, workspaceId, PermissionKeys.RunRequests, cancellationToken))
            return Forbidden<AiAssistantActionDto>(PermissionKeys.RunRequests);
        var provider = aiProviderService.GetStatus();
        var providerStatus = provider.Configured
            ? $"{provider.ProviderName} real AI mode ({provider.ModelName})"
            : "Fallback mode - AI provider not configured";
        var context = request.Input ?? string.Empty;
        if (request.RequestId is Guid requestId)
        {
            var scope = await collectionRepository.GetRequestScopeAsync(requestId, cancellationToken);
            if (scope is null || scope.Value.OrganizationId != orgId.Value || scope.Value.WorkspaceId != workspaceId)
                return Result<AiAssistantActionDto>.Failure("Request was not found in this workspace.", new ErrorDetail("request.scope_mismatch", "Request was not found in this workspace."));
            var apiRequest = await collectionRepository.GetRequestAsync(requestId, cancellationToken);
            if (apiRequest is not null) context = $"{apiRequest.Method} {apiRequest.Url} {apiRequest.Description}";
        }
        var suggestions = await aiProviderService.GenerateSuggestionsAsync(request.Action, context, cancellationToken)
            ?? BuildAssistantSuggestions(request.Action, context);
        await RecordActivityAsync(orgId.Value, workspaceId, "AiAssistantActionRequested", "AiAssistant", null, request.Action, "Suggest", "Success", "Info", providerStatus, null, cancellationToken);
        return Result<AiAssistantActionDto>.Success(new AiAssistantActionDto(request.Action, providerStatus, suggestions, DateTime.UtcNow));
    }

    public async Task<Result<AdvancedAnalyticsDto>> GetAdvancedAnalyticsAsync(Guid workspaceId, CancellationToken cancellationToken)
    {
        var orgId = await collectionRepository.GetWorkspaceOrganizationIdAsync(workspaceId, cancellationToken);
        if (orgId is null) return Result<AdvancedAnalyticsDto>.Failure("Workspace was not found.", new ErrorDetail("workspace.not_found", "Workspace was not found."));
        if (!await HasWorkspacePermissionAsync(CurrentUser?.UserId, orgId.Value, workspaceId, PermissionKeys.ViewTeamActivity, cancellationToken))
            return CurrentUser is null ? Unauthorized<AdvancedAnalyticsDto>() : Forbidden<AdvancedAnalyticsDto>(PermissionKeys.ViewTeamActivity);
        return Result<AdvancedAnalyticsDto>.Success(await phase4Repository.GetAdvancedAnalyticsAsync(workspaceId, cancellationToken));
    }

    public async Task<Result<BillingOverviewDto>> GetBillingOverviewAsync(Guid organizationId, CancellationToken cancellationToken)
    {
        if (!await HasOrgPermissionAsync(organizationId, PermissionKeys.ManageBillingSettings, cancellationToken))
            return CurrentUser is null ? Unauthorized<BillingOverviewDto>() : Forbidden<BillingOverviewDto>(PermissionKeys.ManageBillingSettings);
        return Result<BillingOverviewDto>.Success(await phase4Repository.GetBillingOverviewAsync(organizationId, cancellationToken));
    }

    public async Task<Result<IReadOnlyList<ApiKeyDto>>> GetApiKeysAsync(Guid organizationId, CancellationToken cancellationToken)
    {
        if (!await HasOrgPermissionAsync(organizationId, PermissionKeys.ManageBillingSettings, cancellationToken))
            return CurrentUser is null ? Unauthorized<IReadOnlyList<ApiKeyDto>>() : Forbidden<IReadOnlyList<ApiKeyDto>>(PermissionKeys.ManageBillingSettings);
        return Result<IReadOnlyList<ApiKeyDto>>.Success(await phase4Repository.GetApiKeysAsync(organizationId, cancellationToken));
    }

    public async Task<Result<CreatedApiKeyDto>> CreateApiKeyAsync(Guid organizationId, CreateApiKeyRequest request, CancellationToken cancellationToken)
    {
        if (CurrentUser is null) return Unauthorized<CreatedApiKeyDto>();
        if (!await HasOrgPermissionAsync(organizationId, PermissionKeys.ManageBillingSettings, cancellationToken))
            return Forbidden<CreatedApiKeyDto>(PermissionKeys.ManageBillingSettings);
        if (string.IsNullOrWhiteSpace(request.Name))
            return Result<CreatedApiKeyDto>.Failure("API key name is required.", new ErrorDetail("api_key.name_required", "API key name is required."));
        var plain = $"adk_{Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).Replace("+", "").Replace("/", "").Replace("=", "")}";
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(plain)));
        var key = await phase4Repository.CreateApiKeyAsync(organizationId, request, hash, CurrentUser.UserId, cancellationToken);
        await RecordActivityAsync(organizationId, request.WorkspaceId, "ApiKeyCreated", "ApiKey", key.Id, key.Name, "Create", "Success", "Info", "API key created. Plain key was returned once only.", null, cancellationToken);
        return Result<CreatedApiKeyDto>.Success(new CreatedApiKeyDto(key, plain), "API key created. Copy it now; it will not be shown again.");
    }

    private async Task<bool> HasOrgPermissionAsync(Guid organizationId, string permission, CancellationToken cancellationToken)
        => CurrentUser is not null && await permissionService.HasPermissionAsync(CurrentUser.UserId, organizationId, null, permission, cancellationToken);

    private async Task<bool> HasWorkspacePermissionAsync(Guid? userId, Guid organizationId, Guid workspaceId, string permission, CancellationToken cancellationToken)
        => userId is not null && await permissionService.HasPermissionAsync(userId.Value, organizationId, workspaceId, permission, cancellationToken);

    private static IReadOnlyList<string> BuildAssistantSuggestions(string action, string context)
    {
        var target = string.IsNullOrWhiteSpace(context) ? "the selected API" : context.Trim();
        return action switch
        {
            "GenerateTests" => [
                $"Assert status code is successful for {target}.",
                "Assert response time is less than the workspace threshold.",
                "Assert required headers and JSON fields exist."
            ],
            "ExplainResponse" => [
                $"Summarize the response contract for {target}.",
                "Highlight status, headers, body shape, and possible failure conditions."
            ],
            "GenerateDocs" => [
                $"Generate concise endpoint documentation for {target}.",
                "Include auth, parameters, examples, and error responses."
            ],
            "SuggestMocks" => [
                $"Create success, validation error, unauthorized, and server error examples for {target}."
            ],
            "FindQualityGaps" => [
                "Check missing auth, examples, descriptions, and standard error responses.",
                $"Review naming consistency for {target}."
            ],
            _ => [$"Assistant action queued for {target}."]
        };
    }
}
