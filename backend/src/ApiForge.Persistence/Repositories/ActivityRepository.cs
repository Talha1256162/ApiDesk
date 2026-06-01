using ApiForge.Application.Abstractions.Persistence;
using ApiForge.Application.DTOs.Activity;
using ApiForge.Shared.Pagination;
using ApiForge.Shared.Security;
using Dapper;
using ApiForge.Persistence.Connection;
using ApiForge.Persistence.Queries;

namespace ApiForge.Persistence.Repositories;

public sealed class ActivityRepository(ISqlConnectionFactory connectionFactory) : IActivityRepository
{
    public async Task RecordAsync(ActivityWriteModel activity, CancellationToken cancellationToken)
    {
        using var connection = connectionFactory.CreateConnection();
        await connection.ExecuteAsync(new CommandDefinition(
            ActivityQueries.InsertActivity,
            activity with { MetadataJson = SensitiveDataMasker.MaskJson(activity.MetadataJson) },
            cancellationToken: cancellationToken));
    }

    public async Task<PagedResult<ActivityEventDto>> GetActivityAsync(ActivityFilterRequest request, CancellationToken cancellationToken)
    {
        using var connection = connectionFactory.CreateConnection();
        using var grid = await connection.QueryMultipleAsync(new CommandDefinition("""
            select count(1)
            from activityEvents
            where organizationId = @OrganizationId
                and (@WorkspaceId is null or workspaceId = @WorkspaceId)
                and (@UserId is null or actorUserId = @UserId)
                and (@CollectionId is null or entityId = @CollectionId)
                and (@EventType is null or eventType = @EventType)
                and (@Status is null or status = @Status)
                and (@FromUtc is null or createdOn >= @FromUtc)
                and (@ToUtc is null or createdOn < @ToUtc)
                and isDeleted = 0;

            select id, organizationId, workspaceId, actorUserId, actorName, actorEmail, eventType, entityType,
                   entityId, entityName, status, severity, summary, correlationId, createdOn
            from activityEvents
            where organizationId = @OrganizationId
                and (@WorkspaceId is null or workspaceId = @WorkspaceId)
                and (@UserId is null or actorUserId = @UserId)
                and (@CollectionId is null or entityId = @CollectionId)
                and (@EventType is null or eventType = @EventType)
                and (@Status is null or status = @Status)
                and (@FromUtc is null or createdOn >= @FromUtc)
                and (@ToUtc is null or createdOn < @ToUtc)
                and isDeleted = 0
            order by createdOn desc
            offset @Offset rows fetch next @Count rows only;
            """,
            new
            {
                request.OrganizationId,
                request.WorkspaceId,
                request.UserId,
                request.CollectionId,
                request.EventType,
                request.Status,
                request.FromUtc,
                request.ToUtc,
                Offset = Math.Max(0, request.Offset),
                Count = request.Count is < 1 or > 200 ? 25 : request.Count
            },
            cancellationToken: cancellationToken));

        var total = await grid.ReadSingleAsync<int>();
        var items = (await grid.ReadAsync<ActivityEventDto>()).AsList();
        return new PagedResult<ActivityEventDto>(items, total, Math.Max(0, request.Offset), request.Count);
    }

    public async Task<ManagerSummaryDto> GetManagerSummaryAsync(Guid workspaceId, CancellationToken cancellationToken)
    {
        using var connection = connectionFactory.CreateConnection();
        using var grid = await connection.QueryMultipleAsync(new CommandDefinition("""
            select count(distinct actorUserId)
            from activityEvents
            where workspaceId = @WorkspaceId and cast(createdOn as date) = cast(sysutcdatetime() as date) and isDeleted = 0;

            select count(1)
            from activityEvents
            where workspaceId = @WorkspaceId and eventType = 'RequestSent' and cast(createdOn as date) = cast(sysutcdatetime() as date) and isDeleted = 0;

            select count(1)
            from activityEvents
            where workspaceId = @WorkspaceId and eventType = 'RequestFailed' and cast(createdOn as date) = cast(sysutcdatetime() as date) and isDeleted = 0;

            select count(1)
            from activityEvents
            where workspaceId = @WorkspaceId and eventType in ('CollectionCreated','CollectionUpdated','CollectionDeleted') and cast(createdOn as date) = cast(sysutcdatetime() as date) and isDeleted = 0;

            select count(1)
            from activityEvents
            where workspaceId = @WorkspaceId and eventType in ('EnvironmentCreated','EnvironmentVariablesChanged') and cast(createdOn as date) = cast(sysutcdatetime() as date) and isDeleted = 0;

            select count(1)
            from apiReviewItems
            where workspaceId = @WorkspaceId and status = 'In Review' and isDeleted = 0;

            select cast(createdOn as date) as [date], count(1) as [value]
            from activityEvents
            where workspaceId = @WorkspaceId and eventType = 'RequestSent' and createdOn >= dateadd(day, -14, sysutcdatetime()) and isDeleted = 0
            group by cast(createdOn as date)
            order by [date];

            select cast(createdOn as date) as [date], count(1) as [value]
            from activityEvents
            where workspaceId = @WorkspaceId and eventType = 'RequestFailed' and createdOn >= dateadd(day, -14, sysutcdatetime()) and isDeleted = 0
            group by cast(createdOn as date)
            order by [date];

            select top 5 actorUserId as userId, actorName as name, count(1) as [count]
            from activityEvents
            where workspaceId = @WorkspaceId and createdOn >= dateadd(day, -14, sysutcdatetime()) and isDeleted = 0
            group by actorUserId, actorName
            order by [count] desc;

            select top 5 requestId, endpoint, failureCount
            from (
                select rr.requestId, concat(r.method, ' ', r.url) as endpoint, count(1) as failureCount
                from requestRuns rr
                left join requests r on r.id = rr.requestId
                where rr.workspaceId = @WorkspaceId and rr.succeeded = 0 and rr.isDeleted = 0
                group by rr.requestId, r.method, r.url
            ) x
            order by failureCount desc;

            select top 5 requestId, endpoint, averageMs
            from (
                select rr.requestId, concat(r.method, ' ', r.url) as endpoint, cast(avg(cast(rr.elapsedMs as decimal(18,2))) as decimal(18,2)) as averageMs
                from requestRuns rr
                left join requests r on r.id = rr.requestId
                where rr.workspaceId = @WorkspaceId and rr.isDeleted = 0
                group by rr.requestId, r.method, r.url
            ) x
            order by averageMs desc;
            """,
            new { WorkspaceId = workspaceId },
            cancellationToken: cancellationToken));

        var activeUsers = await grid.ReadSingleAsync<int>();
        var sent = await grid.ReadSingleAsync<int>();
        var failed = await grid.ReadSingleAsync<int>();
        var collectionsChanged = await grid.ReadSingleAsync<int>();
        var envChanged = await grid.ReadSingleAsync<int>();
        var pending = await grid.ReadSingleAsync<int>();
        var sentTrend = (await grid.ReadAsync<ChartPointDto>()).AsList();
        var failedTrend = (await grid.ReadAsync<ChartPointDto>()).AsList();
        var users = (await grid.ReadAsync<UserActivityPointDto>()).AsList();
        var topFailed = (await grid.ReadAsync<EndpointMetricDto>()).AsList();
        var latency = (await grid.ReadAsync<EndpointLatencyDto>()).AsList();

        return new ManagerSummaryDto(activeUsers, sent, failed, collectionsChanged, envChanged, pending, sentTrend, failedTrend, users, topFailed, latency);
    }
}
