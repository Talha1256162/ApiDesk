using ApiForge.Application.Abstractions.Persistence;
using ApiForge.Application.DTOs.BetaFeedback;
using ApiForge.Persistence.Connection;
using ApiForge.Shared.Pagination;
using Dapper;

namespace ApiForge.Persistence.Repositories;

public sealed class BetaFeedbackRepository(ISqlConnectionFactory connectionFactory) : IBetaFeedbackRepository
{
    public async Task<BetaFeedbackDto> CreateAsync(CreateBetaFeedbackRequest request, Guid actorUserId, string actorName, string actorEmail, CancellationToken cancellationToken)
    {
        using var connection = connectionFactory.CreateConnection();
        var id = Guid.NewGuid();
        return await connection.QuerySingleAsync<BetaFeedbackDto>(new CommandDefinition("""
            insert into betaFeedback
            (
                id, organizationId, workspaceId, actorUserId, actorName, actorEmail,
                category, sentiment, rating, title, message, route, browserInfo,
                status, createdOn, createdBy, isDeleted, versionNumber
            )
            output inserted.id, inserted.organizationId, inserted.workspaceId, inserted.actorUserId,
                   inserted.actorName, inserted.actorEmail, inserted.category, inserted.sentiment,
                   inserted.rating, inserted.title, inserted.message, inserted.route, inserted.browserInfo,
                   inserted.status, inserted.adminNotes, inserted.createdOn, inserted.modifiedOn, inserted.versionNumber
            values
            (
                @Id, @OrganizationId, @WorkspaceId, @ActorUserId, @ActorName, @ActorEmail,
                @Category, @Sentiment, @Rating, @Title, @Message, @Route, @BrowserInfo,
                'New', sysutcdatetime(), @ActorUserId, 0, 1
            );
            """,
            new
            {
                Id = id,
                request.OrganizationId,
                request.WorkspaceId,
                ActorUserId = actorUserId,
                ActorName = actorName,
                ActorEmail = actorEmail,
                request.Category,
                request.Sentiment,
                request.Rating,
                request.Title,
                request.Message,
                request.Route,
                request.BrowserInfo
            },
            cancellationToken: cancellationToken));
    }

    public async Task<Guid?> GetOrganizationIdAsync(Guid feedbackId, CancellationToken cancellationToken)
    {
        using var connection = connectionFactory.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<Guid?>(new CommandDefinition(
            "select organizationId from betaFeedback where id = @FeedbackId and isDeleted = 0;",
            new { FeedbackId = feedbackId },
            cancellationToken: cancellationToken));
    }

    public async Task<PagedResult<BetaFeedbackDto>> GetByOrganizationAsync(Guid organizationId, PagedRequest request, CancellationToken cancellationToken)
    {
        using var connection = connectionFactory.CreateConnection();
        using var grid = await connection.QueryMultipleAsync(new CommandDefinition("""
            select count(1)
            from betaFeedback
            where organizationId = @OrganizationId
              and isDeleted = 0
              and (@Search is null
                or title like '%' + @Search + '%'
                or message like '%' + @Search + '%'
                or actorName like '%' + @Search + '%'
                or actorEmail like '%' + @Search + '%'
                or category like '%' + @Search + '%'
                or status like '%' + @Search + '%');

            select id, organizationId, workspaceId, actorUserId, actorName, actorEmail,
                   category, sentiment, rating, title, message, route, browserInfo,
                   status, adminNotes, createdOn, modifiedOn, versionNumber
            from betaFeedback
            where organizationId = @OrganizationId
              and isDeleted = 0
              and (@Search is null
                or title like '%' + @Search + '%'
                or message like '%' + @Search + '%'
                or actorName like '%' + @Search + '%'
                or actorEmail like '%' + @Search + '%'
                or category like '%' + @Search + '%'
                or status like '%' + @Search + '%')
            order by createdOn desc
            offset @Offset rows fetch next @Count rows only;
            """,
            new
            {
                OrganizationId = organizationId,
                Search = string.IsNullOrWhiteSpace(request.SearchString) ? null : request.SearchString.Trim(),
                Offset = request.SafeOffset,
                Count = request.SafeCount
            },
            cancellationToken: cancellationToken));

        var total = await grid.ReadSingleAsync<int>();
        var items = (await grid.ReadAsync<BetaFeedbackDto>()).AsList();
        return new PagedResult<BetaFeedbackDto>(items, total, request.SafeOffset, request.SafeCount);
    }

    public async Task<BetaFeedbackDto?> UpdateStatusAsync(Guid feedbackId, UpdateBetaFeedbackStatusRequest request, Guid modifiedBy, CancellationToken cancellationToken)
    {
        using var connection = connectionFactory.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<BetaFeedbackDto>(new CommandDefinition("""
            update betaFeedback
            set status = @Status,
                adminNotes = @AdminNotes,
                modifiedOn = sysutcdatetime(),
                modifiedBy = @ModifiedBy,
                versionNumber = versionNumber + 1
            output inserted.id, inserted.organizationId, inserted.workspaceId, inserted.actorUserId,
                   inserted.actorName, inserted.actorEmail, inserted.category, inserted.sentiment,
                   inserted.rating, inserted.title, inserted.message, inserted.route, inserted.browserInfo,
                   inserted.status, inserted.adminNotes, inserted.createdOn, inserted.modifiedOn, inserted.versionNumber
            where id = @FeedbackId and isDeleted = 0;
            """,
            new { FeedbackId = feedbackId, request.Status, request.AdminNotes, ModifiedBy = modifiedBy },
            cancellationToken: cancellationToken));
    }

    public async Task<BetaChecklistSignals> GetChecklistSignalsAsync(Guid organizationId, Guid? workspaceId, Guid actorUserId, CancellationToken cancellationToken)
    {
        using var connection = connectionFactory.CreateConnection();
        using var grid = await connection.QueryMultipleAsync(new CommandDefinition("""
            select cast(case when exists (
                select 1
                from collections c
                join workspaces w on w.id = c.workspaceId
                where w.organizationId = @OrganizationId
                  and (@WorkspaceId is null or c.workspaceId = @WorkspaceId)
                  and c.isDeleted = 0 and w.isDeleted = 0
            ) then 1 else 0 end as bit);

            select cast(case when exists (
                select 1
                from environments e
                join workspaces w on w.id = e.workspaceId
                where w.organizationId = @OrganizationId
                  and (@WorkspaceId is null or e.workspaceId = @WorkspaceId)
                  and e.isDeleted = 0 and w.isDeleted = 0
            ) then 1 else 0 end as bit);

            select cast(case when exists (
                select 1
                from requestRuns rr
                join workspaces w on w.id = rr.workspaceId
                where w.organizationId = @OrganizationId
                  and (@WorkspaceId is null or rr.workspaceId = @WorkspaceId)
                  and rr.isDeleted = 0 and w.isDeleted = 0
            ) then 1 else 0 end as bit);

            select cast(case when
                (select count(1) from organizationMembers where organizationId = @OrganizationId and status = 'Active' and isDeleted = 0) > 1
                or exists (select 1 from invitations where organizationId = @OrganizationId and isDeleted = 0)
            then 1 else 0 end as bit);

            select cast(case when exists (
                select 1
                from betaFeedback
                where organizationId = @OrganizationId and actorUserId = @ActorUserId and isDeleted = 0
            ) then 1 else 0 end as bit);
            """,
            new { OrganizationId = organizationId, WorkspaceId = workspaceId, ActorUserId = actorUserId },
            cancellationToken: cancellationToken));

        return new BetaChecklistSignals(
            await grid.ReadSingleAsync<bool>(),
            await grid.ReadSingleAsync<bool>(),
            await grid.ReadSingleAsync<bool>(),
            await grid.ReadSingleAsync<bool>(),
            await grid.ReadSingleAsync<bool>());
    }
}
