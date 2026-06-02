namespace ApiForge.Application.DTOs.Phase4;

public sealed record AiAssistantConfigDto(Guid Id, Guid OrganizationId, string Provider, string? ModelName, string? EndpointUrl, string? DeploymentName, bool IsEnabled, DateTime CreatedOn, DateTime? ModifiedOn);
public sealed record SaveAiAssistantConfigRequest(string Provider, string? ModelName, string? EndpointUrl, string? DeploymentName, bool IsEnabled);
public sealed record AiAssistantActionRequest(string Action, Guid? CollectionId, Guid? RequestId, string? Input);
public sealed record AiAssistantActionDto(string Action, string ProviderStatus, IReadOnlyList<string> Suggestions, DateTime CreatedOnUtc);

public sealed record AdvancedAnalyticsDto(
    int TotalRuns,
    int SuccessfulRuns,
    int FailedRuns,
    double SuccessRate,
    long AverageLatencyMs,
    IReadOnlyList<AnalyticsPointDto> RequestsPerDay,
    IReadOnlyList<AnalyticsPointDto> FailuresPerDay,
    IReadOnlyList<AnalyticsRankDto> TopUsers,
    IReadOnlyList<AnalyticsRankDto> TopEndpoints,
    IReadOnlyList<AnalyticsRankDto> SlowEndpoints);

public sealed record AnalyticsPointDto(string Label, int Value);
public sealed record AnalyticsRankDto(string Label, int Value, long? Metric);

public sealed record BillingPlanDto(Guid Id, string Code, string Name, decimal MonthlyPrice, int IncludedRequests, int? IncludedMembers, string FeaturesJson);
public sealed record OrganizationSubscriptionDto(Guid Id, Guid OrganizationId, Guid BillingPlanId, string PlanName, string Status, DateTime CurrentPeriodStart, DateTime CurrentPeriodEnd);
public sealed record BillingOverviewDto(IReadOnlyList<BillingPlanDto> Plans, OrganizationSubscriptionDto? Subscription, int RequestsThisPeriod, int Members, int Workspaces);

public sealed record OrganizationSaasSettingsDto(Guid OrganizationId, string ProductName, int RetentionDays);
public sealed record SaveOrganizationSaasSettingsRequest(string ProductName, int RetentionDays);

public sealed record ApiKeyDto(Guid Id, Guid OrganizationId, Guid? WorkspaceId, string Name, DateTime? ExpiresOn, DateTime? LastUsedOn, DateTime CreatedOn);
public sealed record CreateApiKeyRequest(Guid? WorkspaceId, string Name, DateTime? ExpiresOn);
public sealed record CreatedApiKeyDto(ApiKeyDto ApiKey, string PlainTextKey);
