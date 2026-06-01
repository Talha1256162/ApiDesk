using ApiForge.Application.Abstractions.Persistence;
using ApiForge.Application.DTOs.Workspaces;
using ApiForge.Shared.Pagination;
using Dapper;
using ApiForge.Persistence.Connection;

namespace ApiForge.Persistence.Repositories;

public sealed class WorkspaceRepository(ISqlConnectionFactory connectionFactory) : IWorkspaceRepository
{
    public async Task<Guid?> GetOrganizationIdAsync(Guid workspaceId, CancellationToken cancellationToken)
    {
        using var connection = connectionFactory.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<Guid?>(new CommandDefinition(
            "select organizationId from workspaces where id = @WorkspaceId and isDeleted = 0;",
            new { WorkspaceId = workspaceId },
            cancellationToken: cancellationToken));
    }

    public async Task<PagedResult<WorkspaceDto>> GetByOrganizationAsync(Guid organizationId, PagedRequest request, CancellationToken cancellationToken)
    {
        using var connection = connectionFactory.CreateConnection();
        using var grid = await connection.QueryMultipleAsync(new CommandDefinition("""
            select count(1)
            from workspaces
            where organizationId = @OrganizationId and isDeleted = 0
                and (@Search is null or name like '%' + @Search + '%');

            select id, organizationId, name, slug, type, description, createdOn
            from workspaces
            where organizationId = @OrganizationId and isDeleted = 0
                and (@Search is null or name like '%' + @Search + '%')
            order by name
            offset @Offset rows fetch next @Count rows only;
            """,
            new { OrganizationId = organizationId, Search = request.SearchString, Offset = request.SafeOffset, Count = request.SafeCount },
            cancellationToken: cancellationToken));

        var total = await grid.ReadSingleAsync<int>();
        var items = (await grid.ReadAsync<WorkspaceDto>()).AsList();
        return new PagedResult<WorkspaceDto>(items, total, request.SafeOffset, request.SafeCount);
    }

    public async Task<WorkspaceDto> CreateAsync(CreateWorkspaceRequest request, Guid createdBy, CancellationToken cancellationToken)
    {
        using var connection = connectionFactory.CreateConnection();
        connection.Open();
        using var transaction = connection.BeginTransaction();

        var workspaceId = Guid.NewGuid();
        var slug = $"{RepositoryUtility.Slugify(request.Name)}-{workspaceId.ToString("N")[..6]}";

        await connection.ExecuteAsync(new CommandDefinition("""
            insert into workspaces (id, organizationId, name, slug, type, description, createdOn, createdBy, isDeleted, versionNumber)
            values (@WorkspaceId, @OrganizationId, @Name, @Slug, @Type, @Description, sysutcdatetime(), @CreatedBy, 0, 1);

            insert into workspaceMembers (id, organizationId, workspaceId, userId, roleId, status, createdOn, createdBy, isDeleted, versionNumber)
            select newid(), @OrganizationId, @WorkspaceId, @CreatedBy, om.roleId, 'Active', sysutcdatetime(), @CreatedBy, 0, 1
            from organizationMembers om
            where om.organizationId = @OrganizationId and om.userId = @CreatedBy and om.status = 'Active' and om.isDeleted = 0;

            insert into environments (id, organizationId, workspaceId, name, isDefault, createdOn, createdBy, isDeleted, versionNumber)
            values (newid(), @OrganizationId, @WorkspaceId, 'Local', 1, sysutcdatetime(), @CreatedBy, 0, 1);
            """,
            new { WorkspaceId = workspaceId, request.OrganizationId, request.Name, Slug = slug, request.Type, request.Description, CreatedBy = createdBy },
            transaction,
            cancellationToken: cancellationToken));

        transaction.Commit();
        return new WorkspaceDto(workspaceId, request.OrganizationId, request.Name, slug, request.Type, request.Description, DateTime.UtcNow);
    }

    public async Task<WorkspaceDto?> UpdateAsync(Guid workspaceId, UpdateWorkspaceRequest request, Guid modifiedBy, CancellationToken cancellationToken)
    {
        using var connection = connectionFactory.CreateConnection();
        var slug = $"{RepositoryUtility.Slugify(request.Name)}-{workspaceId.ToString("N")[..6]}";
        var updated = await connection.QuerySingleOrDefaultAsync<WorkspaceDto>(new CommandDefinition("""
            update workspaces
            set name = @Name,
                slug = @Slug,
                type = @Type,
                description = @Description,
                modifiedOn = sysutcdatetime(),
                modifiedBy = @ModifiedBy,
                versionNumber = versionNumber + 1
            output inserted.id, inserted.organizationId, inserted.name, inserted.slug, inserted.type, inserted.description, inserted.createdOn
            where id = @WorkspaceId and isDeleted = 0 and versionNumber = @VersionNumber;
            """,
            new { WorkspaceId = workspaceId, request.Name, Slug = slug, request.Type, request.Description, request.VersionNumber, ModifiedBy = modifiedBy },
            cancellationToken: cancellationToken));
        return updated;
    }

    public async Task<bool> DeleteAsync(Guid workspaceId, Guid modifiedBy, CancellationToken cancellationToken)
    {
        using var connection = connectionFactory.CreateConnection();
        var rows = await connection.ExecuteAsync(new CommandDefinition("""
            update workspaces
            set isDeleted = 1, modifiedOn = sysutcdatetime(), modifiedBy = @ModifiedBy, versionNumber = versionNumber + 1
            where id = @WorkspaceId and isDeleted = 0;
            """,
            new { WorkspaceId = workspaceId, ModifiedBy = modifiedBy },
            cancellationToken: cancellationToken));
        return rows > 0;
    }

    public async Task<WorkspaceDashboardDto> GetDashboardAsync(Guid workspaceId, CancellationToken cancellationToken)
    {
        using var connection = connectionFactory.CreateConnection();
        using var grid = await connection.QueryMultipleAsync(new CommandDefinition("""
            select count(1)
            from workspaces
            where organizationId = (select organizationId from workspaces where id = @WorkspaceId)
                and isDeleted = 0;

            select count(1) from collections where workspaceId = @WorkspaceId and isDeleted = 0;
            select count(1) from requests where workspaceId = @WorkspaceId and isDeleted = 0;
            select count(1) from requestRuns where workspaceId = @WorkspaceId and cast(createdOn as date) = cast(sysutcdatetime() as date) and isDeleted = 0;
            select count(1) from requestRuns where workspaceId = @WorkspaceId and succeeded = 0 and cast(createdOn as date) = cast(sysutcdatetime() as date) and isDeleted = 0;
            select count(distinct userId) from workspaceMembers where workspaceId = @WorkspaceId and status = 'Active' and isDeleted = 0;

            select top 10 createdOn, actorName, eventType, coalesce(entityName, entityType) as entityName, status
            from activityEvents
            where workspaceId = @WorkspaceId and isDeleted = 0
            order by createdOn desc;

            select top 5 id, 'Collection run' as name, status, createdOn
            from collectionRuns
            where workspaceId = @WorkspaceId and isDeleted = 0
            order by createdOn desc;

            select top 5 r.id as requestId, r.name, r.method, r.url, cast(avg(cast(rr.elapsedMs as decimal(18,2))) as decimal(18,2)) as averageMs
            from requestRuns rr
            join requests r on r.id = rr.requestId
            where rr.workspaceId = @WorkspaceId and rr.isDeleted = 0 and rr.elapsedMs is not null
            group by r.id, r.name, r.method, r.url
            order by averageMs desc;

            select top 5 e.id as environmentId, e.name, count(rr.id) as runCount
            from environments e
            left join requestRuns rr on rr.environmentId = e.id and rr.isDeleted = 0
            where e.workspaceId = @WorkspaceId and e.isDeleted = 0
            group by e.id, e.name
            order by runCount desc;
            """,
            new { WorkspaceId = workspaceId },
            cancellationToken: cancellationToken));

        var totalWorkspaces = await grid.ReadSingleAsync<int>();
        var totalCollections = await grid.ReadSingleAsync<int>();
        var totalApis = await grid.ReadSingleAsync<int>();
        var sentToday = await grid.ReadSingleAsync<int>();
        var failedToday = await grid.ReadSingleAsync<int>();
        var activeMembers = await grid.ReadSingleAsync<int>();
        var recent = (await grid.ReadAsync<RecentActivityDto>()).AsList();
        var runs = (await grid.ReadAsync<LatestTestRunDto>()).AsList();
        var slowApis = (await grid.ReadAsync<SlowApiDto>()).AsList();
        var envUsage = (await grid.ReadAsync<EnvironmentUsageDto>()).AsList();

        return new WorkspaceDashboardDto(workspaceId, totalWorkspaces, totalCollections, totalApis, sentToday, failedToday, activeMembers, recent, runs, slowApis, envUsage);
    }
}
