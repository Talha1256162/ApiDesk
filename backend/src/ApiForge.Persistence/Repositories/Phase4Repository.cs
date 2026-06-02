using ApiForge.Application.Abstractions.Persistence;
using ApiForge.Application.DTOs.Phase4;
using ApiForge.Persistence.Connection;
using Dapper;

namespace ApiForge.Persistence.Repositories;

public sealed class Phase4Repository(ISqlConnectionFactory connectionFactory) : IPhase4Repository
{
    public async Task<OrganizationSaasSettingsDto?> GetOrganizationSettingsAsync(Guid organizationId, CancellationToken cancellationToken)
    {
        using var connection = connectionFactory.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<OrganizationSaasSettingsDto>(new CommandDefinition("""
            select id as organizationId, productName, retentionDays
            from organizations
            where id = @OrganizationId and isDeleted = 0;
            """, new { OrganizationId = organizationId }, cancellationToken: cancellationToken));
    }

    public async Task<OrganizationSaasSettingsDto> SaveOrganizationSettingsAsync(Guid organizationId, SaveOrganizationSaasSettingsRequest request, Guid userId, CancellationToken cancellationToken)
    {
        using var connection = connectionFactory.CreateConnection();
        return await connection.QuerySingleAsync<OrganizationSaasSettingsDto>(new CommandDefinition("""
            update organizations
            set productName = @ProductName,
                retentionDays = @RetentionDays,
                modifiedOn = sysutcdatetime(),
                modifiedBy = @UserId,
                versionNumber = versionNumber + 1
            where id = @OrganizationId and isDeleted = 0;
            select id as organizationId, productName, retentionDays from organizations where id = @OrganizationId;
            """, new { OrganizationId = organizationId, ProductName = request.ProductName.Trim(), RetentionDays = Math.Clamp(request.RetentionDays, 30, 3650), UserId = userId }, cancellationToken: cancellationToken));
    }

    public async Task<AiAssistantConfigDto?> GetAiConfigAsync(Guid organizationId, CancellationToken cancellationToken)
    {
        using var connection = connectionFactory.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<AiAssistantConfigDto>(new CommandDefinition("""
            select top 1 id, organizationId, provider, modelName, endpointUrl, deploymentName, isEnabled, createdOn, modifiedOn
            from aiAssistantConfigs
            where organizationId = @OrganizationId and isDeleted = 0
            order by modifiedOn desc, createdOn desc;
            """, new { OrganizationId = organizationId }, cancellationToken: cancellationToken));
    }

    public async Task<AiAssistantConfigDto> SaveAiConfigAsync(Guid organizationId, SaveAiAssistantConfigRequest request, Guid userId, CancellationToken cancellationToken)
    {
        using var connection = connectionFactory.CreateConnection();
        var existingId = await connection.ExecuteScalarAsync<Guid?>(new CommandDefinition("""
            select top 1 id from aiAssistantConfigs where organizationId = @OrganizationId and isDeleted = 0 order by createdOn desc;
            """, new { OrganizationId = organizationId }, cancellationToken: cancellationToken));
        var id = existingId ?? Guid.NewGuid();
        await connection.ExecuteAsync(new CommandDefinition(existingId is null ? """
            insert into aiAssistantConfigs (id, organizationId, provider, modelName, endpointUrl, deploymentName, isEnabled, createdOn, createdBy, isDeleted, versionNumber)
            values (@Id, @OrganizationId, @Provider, @ModelName, @EndpointUrl, @DeploymentName, @IsEnabled, sysutcdatetime(), @UserId, 0, 1);
            """ : """
            update aiAssistantConfigs
            set provider = @Provider, modelName = @ModelName, endpointUrl = @EndpointUrl, deploymentName = @DeploymentName, isEnabled = @IsEnabled,
                modifiedOn = sysutcdatetime(), modifiedBy = @UserId, versionNumber = versionNumber + 1
            where id = @Id;
            """, new { Id = id, OrganizationId = organizationId, request.Provider, request.ModelName, request.EndpointUrl, request.DeploymentName, request.IsEnabled, UserId = userId }, cancellationToken: cancellationToken));
        return (await GetAiConfigAsync(organizationId, cancellationToken))!;
    }

    public async Task<AdvancedAnalyticsDto> GetAdvancedAnalyticsAsync(Guid workspaceId, CancellationToken cancellationToken)
    {
        using var connection = connectionFactory.CreateConnection();
        var totals = await connection.QuerySingleAsync<(int TotalRuns, int SuccessfulRuns, int FailedRuns, long AverageLatencyMs)>(new CommandDefinition("""
            select count(1) as totalRuns,
                   sum(case when succeeded = 1 then 1 else 0 end) as successfulRuns,
                   sum(case when succeeded = 0 then 1 else 0 end) as failedRuns,
                   coalesce(avg(cast(elapsedMs as bigint)), 0) as averageLatencyMs
            from requestRuns
            where workspaceId = @WorkspaceId and isDeleted = 0;
            """, new { WorkspaceId = workspaceId }, cancellationToken: cancellationToken));
        var requestsPerDay = (await connection.QueryAsync<AnalyticsPointDto>(new CommandDefinition("""
            select convert(varchar(10), createdOn, 23) as label, count(1) as value
            from requestRuns
            where workspaceId = @WorkspaceId and isDeleted = 0 and createdOn >= dateadd(day, -14, sysutcdatetime())
            group by convert(varchar(10), createdOn, 23)
            order by label;
            """, new { WorkspaceId = workspaceId }, cancellationToken: cancellationToken))).AsList();
        var failuresPerDay = (await connection.QueryAsync<AnalyticsPointDto>(new CommandDefinition("""
            select convert(varchar(10), createdOn, 23) as label, count(1) as value
            from requestRuns
            where workspaceId = @WorkspaceId and isDeleted = 0 and succeeded = 0 and createdOn >= dateadd(day, -14, sysutcdatetime())
            group by convert(varchar(10), createdOn, 23)
            order by label;
            """, new { WorkspaceId = workspaceId }, cancellationToken: cancellationToken))).AsList();
        var topUsers = (await connection.QueryAsync<AnalyticsRankDto>(new CommandDefinition("""
            select top 8 u.fullName as label, count(1) as value, cast(null as bigint) as metric
            from requestRuns rr join users u on u.id = rr.userId
            where rr.workspaceId = @WorkspaceId and rr.isDeleted = 0
            group by u.fullName
            order by count(1) desc;
            """, new { WorkspaceId = workspaceId }, cancellationToken: cancellationToken))).AsList();
        var topEndpoints = (await connection.QueryAsync<AnalyticsRankDto>(new CommandDefinition("""
            select top 8 concat(r.method, ' ', r.url) as label, count(1) as value, cast(null as bigint) as metric
            from requestRuns rr join requests r on r.id = rr.requestId
            where rr.workspaceId = @WorkspaceId and rr.isDeleted = 0
            group by r.method, r.url
            order by count(1) desc;
            """, new { WorkspaceId = workspaceId }, cancellationToken: cancellationToken))).AsList();
        var slowEndpoints = (await connection.QueryAsync<AnalyticsRankDto>(new CommandDefinition("""
            select top 8 concat(r.method, ' ', r.url) as label, count(1) as value, avg(cast(rr.elapsedMs as bigint)) as metric
            from requestRuns rr join requests r on r.id = rr.requestId
            where rr.workspaceId = @WorkspaceId and rr.isDeleted = 0 and rr.elapsedMs is not null
            group by r.method, r.url
            order by avg(cast(rr.elapsedMs as bigint)) desc;
            """, new { WorkspaceId = workspaceId }, cancellationToken: cancellationToken))).AsList();
        var successRate = totals.TotalRuns == 0 ? 100 : Math.Round((double)totals.SuccessfulRuns / totals.TotalRuns * 100, 2);
        return new AdvancedAnalyticsDto(totals.TotalRuns, totals.SuccessfulRuns, totals.FailedRuns, successRate, totals.AverageLatencyMs, requestsPerDay, failuresPerDay, topUsers, topEndpoints, slowEndpoints);
    }

    public async Task<BillingOverviewDto> GetBillingOverviewAsync(Guid organizationId, CancellationToken cancellationToken)
    {
        using var connection = connectionFactory.CreateConnection();
        var plans = (await connection.QueryAsync<BillingPlanDto>(new CommandDefinition("""
            select id, code, name, monthlyPrice, includedRequests, includedMembers, featuresJson
            from billingPlans
            where isActive = 1 and isDeleted = 0
            order by monthlyPrice;
            """, cancellationToken: cancellationToken))).AsList();
        var subscription = await connection.QuerySingleOrDefaultAsync<OrganizationSubscriptionDto>(new CommandDefinition("""
            select top 1 os.id, os.organizationId, os.billingPlanId, bp.name as planName, os.status, os.currentPeriodStart, os.currentPeriodEnd
            from organizationSubscriptions os
            join billingPlans bp on bp.id = os.billingPlanId
            where os.organizationId = @OrganizationId and os.isDeleted = 0
            order by os.createdOn desc;
            """, new { OrganizationId = organizationId }, cancellationToken: cancellationToken));
        var usage = await connection.QuerySingleAsync<(int Requests, int Members, int Workspaces)>(new CommandDefinition("""
            select
              (select count(1) from requestRuns where organizationId = @OrganizationId and createdOn >= dateadd(day, -30, sysutcdatetime()) and isDeleted = 0) as requests,
              (select count(1) from organizationMembers where organizationId = @OrganizationId and status = 'Active' and isDeleted = 0) as members,
              (select count(1) from workspaces where organizationId = @OrganizationId and isDeleted = 0) as workspaces;
            """, new { OrganizationId = organizationId }, cancellationToken: cancellationToken));
        return new BillingOverviewDto(plans, subscription, usage.Requests, usage.Members, usage.Workspaces);
    }

    public async Task<IReadOnlyList<ApiKeyDto>> GetApiKeysAsync(Guid organizationId, CancellationToken cancellationToken)
    {
        using var connection = connectionFactory.CreateConnection();
        return (await connection.QueryAsync<ApiKeyDto>(new CommandDefinition("""
            select id, organizationId, workspaceId, name, expiresOn, lastUsedOn, createdOn
            from apiKeys
            where organizationId = @OrganizationId and isDeleted = 0
            order by createdOn desc;
            """, new { OrganizationId = organizationId }, cancellationToken: cancellationToken))).AsList();
    }

    public async Task<ApiKeyDto> CreateApiKeyAsync(Guid organizationId, CreateApiKeyRequest request, string keyHash, Guid userId, CancellationToken cancellationToken)
    {
        using var connection = connectionFactory.CreateConnection();
        var id = Guid.NewGuid();
        return await connection.QuerySingleAsync<ApiKeyDto>(new CommandDefinition("""
            insert into apiKeys (id, organizationId, workspaceId, name, keyHash, expiresOn, createdOn, createdBy, isDeleted, versionNumber)
            values (@Id, @OrganizationId, @WorkspaceId, @Name, @KeyHash, @ExpiresOn, sysutcdatetime(), @UserId, 0, 1);
            select id, organizationId, workspaceId, name, expiresOn, lastUsedOn, createdOn from apiKeys where id = @Id;
            """, new { Id = id, OrganizationId = organizationId, request.WorkspaceId, Name = request.Name.Trim(), KeyHash = keyHash, request.ExpiresOn, UserId = userId }, cancellationToken: cancellationToken));
    }
}
