using System.Text.RegularExpressions;
using ApiForge.Application.Abstractions.Persistence;
using ApiForge.Application.DTOs.ProductOps;
using ApiForge.Persistence.Connection;
using ApiForge.Shared.Pagination;
using Dapper;

namespace ApiForge.Persistence.Repositories;

public sealed class ProductOpsRepository(ISqlConnectionFactory connectionFactory) : IProductOpsRepository
{
    public async Task<(Guid OrganizationId, Guid WorkspaceId, string CollectionName)?> GetCollectionScopeAsync(Guid collectionId, CancellationToken cancellationToken)
    {
        using var connection = connectionFactory.CreateConnection();
        var row = await connection.QuerySingleOrDefaultAsync<(Guid OrganizationId, Guid WorkspaceId, string CollectionName)>(new CommandDefinition("""
            select organizationId, workspaceId, name as collectionName
            from collections
            where id = @CollectionId and isDeleted = 0;
            """, new { CollectionId = collectionId }, cancellationToken: cancellationToken));
        return row.OrganizationId == Guid.Empty ? null : row;
    }

    public async Task<IReadOnlyList<MockServerDto>> GetMockServersAsync(Guid workspaceId, CancellationToken cancellationToken)
    {
        using var connection = connectionFactory.CreateConnection();
        var rows = await connection.QueryAsync<MockServerDto>(new CommandDefinition("""
            select ms.id, ms.workspaceId, ms.collectionId, c.name as collectionName, ms.name, ms.slug, ms.isPublic, ms.apiKeyRequired,
                   ms.delayMs, count(distinct mr.id) as routeCount, count(distinct ml.id) as logCount, ms.createdOn
            from mockServers ms
            join collections c on c.id = ms.collectionId and c.isDeleted = 0
            left join mockRoutes mr on mr.mockServerId = ms.id and mr.isDeleted = 0
            left join mockLogs ml on ml.mockServerId = ms.id and ml.isDeleted = 0
            where ms.workspaceId = @WorkspaceId and ms.isDeleted = 0
            group by ms.id, ms.workspaceId, ms.collectionId, c.name, ms.name, ms.slug, ms.isPublic, ms.apiKeyRequired, ms.delayMs, ms.createdOn
            order by ms.createdOn desc;
            """, new { WorkspaceId = workspaceId }, cancellationToken: cancellationToken));
        return rows.AsList();
    }

    public async Task<MockServerDto> CreateMockServerAsync(Guid workspaceId, CreateMockServerRequest request, Guid userId, CancellationToken cancellationToken)
    {
        using var connection = connectionFactory.CreateConnection();
        connection.Open();
        using var transaction = connection.BeginTransaction();
        var scope = await connection.QuerySingleAsync<(Guid OrganizationId, string CollectionName)>(new CommandDefinition("""
            select organizationId, name as collectionName from collections where id = @CollectionId and workspaceId = @WorkspaceId and isDeleted = 0;
            """, new { request.CollectionId, WorkspaceId = workspaceId }, transaction, cancellationToken: cancellationToken));
        var mockServerId = Guid.NewGuid();
        var slug = await UniqueSlugAsync(connection, transaction, request.Name, cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition("""
            insert into mockServers (id, organizationId, workspaceId, collectionId, name, slug, isPublic, apiKeyRequired, delayMs, createdOn, createdBy, isDeleted, versionNumber)
            values (@Id, @OrganizationId, @WorkspaceId, @CollectionId, @Name, @Slug, @IsPublic, @ApiKeyRequired, @DelayMs, sysutcdatetime(), @UserId, 0, 1);
            """, new { Id = mockServerId, scope.OrganizationId, WorkspaceId = workspaceId, request.CollectionId, Name = request.Name.Trim(), Slug = slug, request.IsPublic, request.ApiKeyRequired, DelayMs = Math.Clamp(request.DelayMs, 0, 30000), UserId = userId }, transaction, cancellationToken: cancellationToken));
        var routes = await connection.QueryAsync<(string Method, string Url, Guid? ExampleId)>(new CommandDefinition("""
            select r.method, r.url, (select top 1 re.id from requestExamples re where re.requestId = r.id and re.isDeleted = 0 order by re.createdOn desc) as exampleId
            from requests r
            where r.collectionId = @CollectionId and r.isDeleted = 0;
            """, new { request.CollectionId }, transaction, cancellationToken: cancellationToken));
        var routeRows = routes.Select(route => new { Id = Guid.NewGuid(), MockServerId = mockServerId, route.Method, Path = NormalizePath(route.Url), RequestExampleId = route.ExampleId, UserId = userId });
        await connection.ExecuteAsync(new CommandDefinition("""
            insert into mockRoutes (id, mockServerId, method, path, requestExampleId, createdOn, createdBy, isDeleted, versionNumber)
            values (@Id, @MockServerId, @Method, @Path, @RequestExampleId, sysutcdatetime(), @UserId, 0, 1);
            """, routeRows, transaction, cancellationToken: cancellationToken));
        transaction.Commit();
        return new MockServerDto(mockServerId, workspaceId, request.CollectionId, scope.CollectionName, request.Name.Trim(), slug, request.IsPublic, request.ApiKeyRequired, Math.Clamp(request.DelayMs, 0, 30000), routes.Count(), 0, DateTime.UtcNow);
    }

    public async Task<IReadOnlyList<MockRouteDto>> GetMockRoutesAsync(Guid mockServerId, CancellationToken cancellationToken)
    {
        using var connection = connectionFactory.CreateConnection();
        var rows = await connection.QueryAsync<MockRouteDto>(new CommandDefinition("""
            select mr.id, mr.mockServerId, mr.method, mr.path, mr.requestExampleId, re.name as exampleName
            from mockRoutes mr
            left join requestExamples re on re.id = mr.requestExampleId and re.isDeleted = 0
            where mr.mockServerId = @MockServerId and mr.isDeleted = 0
            order by mr.path;
            """, new { MockServerId = mockServerId }, cancellationToken: cancellationToken));
        return rows.AsList();
    }

    public async Task<IReadOnlyList<MockLogDto>> GetMockLogsAsync(Guid mockServerId, int count, CancellationToken cancellationToken)
    {
        using var connection = connectionFactory.CreateConnection();
        var rows = await connection.QueryAsync<MockLogDto>(new CommandDefinition("""
            select top (@Count) id, mockServerId, mockRouteId, method, path, statusCode, createdOn
            from mockLogs
            where mockServerId = @MockServerId and isDeleted = 0
            order by createdOn desc;
            """, new { MockServerId = mockServerId, Count = Math.Clamp(count, 1, 200) }, cancellationToken: cancellationToken));
        return rows.AsList();
    }

    public async Task<MockResponseDto?> MatchMockResponseAsync(string slug, string method, string path, CancellationToken cancellationToken)
    {
        using var connection = connectionFactory.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<MockResponseDto>(new CommandDefinition("""
            select coalesce(re.statusCode, 501) as statusCode,
                   'application/json' as contentType,
                   coalesce(re.body, '{"message":"No saved response example is attached to this mock route."}') as body,
                   re.headersJson as headersJson
            from mockServers ms
            join mockRoutes mr on mr.mockServerId = ms.id and mr.isDeleted = 0
            left join requestExamples re on re.id = mr.requestExampleId and re.isDeleted = 0
            where ms.slug = @Slug and ms.isDeleted = 0 and upper(mr.method) = upper(@Method) and mr.path = @Path;
            """, new { Slug = slug, Method = method, Path = NormalizePath(path) }, cancellationToken: cancellationToken));
    }

    public async Task RecordMockLogAsync(string slug, Guid? routeId, string method, string path, int statusCode, CancellationToken cancellationToken)
    {
        using var connection = connectionFactory.CreateConnection();
        await connection.ExecuteAsync(new CommandDefinition("""
            insert into mockLogs (id, mockServerId, mockRouteId, method, path, statusCode, createdOn, createdBy, isDeleted, versionNumber)
            select newid(), ms.id, @RouteId, @Method, @Path, @StatusCode, sysutcdatetime(), null, 0, 1
            from mockServers ms
            where ms.slug = @Slug and ms.isDeleted = 0;
            """, new { Slug = slug, RouteId = routeId, Method = method, Path = NormalizePath(path), StatusCode = statusCode }, cancellationToken: cancellationToken));
    }

    public async Task<IReadOnlyList<MonitorDto>> GetMonitorsAsync(Guid workspaceId, CancellationToken cancellationToken)
    {
        using var connection = connectionFactory.CreateConnection();
        var rows = await connection.QueryAsync<MonitorDto>(new CommandDefinition("""
            select m.id, m.workspaceId, m.collectionId, m.environmentId, c.name as collectionName, e.name as environmentName,
                   m.name, m.scheduleExpression, m.isEnabled, lr.status as lastStatus, lr.passedCount as lastPassedCount,
                   lr.failedCount as lastFailedCount, lr.latencyMs as lastLatencyMs, lr.createdOn as lastRunOn, m.createdOn
            from monitors m
            join collections c on c.id = m.collectionId and c.isDeleted = 0
            left join environments e on e.id = m.environmentId and e.isDeleted = 0
            outer apply (select top 1 status, passedCount, failedCount, latencyMs, createdOn from monitorRuns where monitorId = m.id and isDeleted = 0 order by createdOn desc) lr
            where m.workspaceId = @WorkspaceId and m.isDeleted = 0
            order by m.createdOn desc;
            """, new { WorkspaceId = workspaceId }, cancellationToken: cancellationToken));
        return rows.AsList();
    }

    public async Task<MonitorDto> CreateMonitorAsync(Guid workspaceId, CreateMonitorRequest request, Guid userId, CancellationToken cancellationToken)
    {
        using var connection = connectionFactory.CreateConnection();
        var monitorId = Guid.NewGuid();
        return await connection.QuerySingleAsync<MonitorDto>(new CommandDefinition("""
            declare @OrganizationId uniqueidentifier = (select organizationId from collections where id = @CollectionId and workspaceId = @WorkspaceId and isDeleted = 0);
            insert into monitors (id, organizationId, workspaceId, collectionId, environmentId, name, scheduleExpression, isEnabled, createdOn, createdBy, isDeleted, versionNumber)
            values (@MonitorId, @OrganizationId, @WorkspaceId, @CollectionId, @EnvironmentId, @Name, @ScheduleExpression, @IsEnabled, sysutcdatetime(), @UserId, 0, 1);
            select m.id, m.workspaceId, m.collectionId, m.environmentId, c.name as collectionName, e.name as environmentName, m.name, m.scheduleExpression, m.isEnabled,
                   cast(null as nvarchar(40)) as lastStatus, cast(null as int) as lastPassedCount, cast(null as int) as lastFailedCount, cast(null as bigint) as lastLatencyMs,
                   cast(null as datetime2) as lastRunOn, m.createdOn
            from monitors m
            join collections c on c.id = m.collectionId
            left join environments e on e.id = m.environmentId
            where m.id = @MonitorId;
            """, new { MonitorId = monitorId, WorkspaceId = workspaceId, request.CollectionId, request.EnvironmentId, Name = request.Name.Trim(), request.ScheduleExpression, request.IsEnabled, UserId = userId }, cancellationToken: cancellationToken));
    }

    public async Task<MonitorDto?> GetMonitorAsync(Guid monitorId, CancellationToken cancellationToken)
    {
        using var connection = connectionFactory.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<MonitorDto>(new CommandDefinition("""
            select m.id, m.workspaceId, m.collectionId, m.environmentId, c.name as collectionName, e.name as environmentName,
                   m.name, m.scheduleExpression, m.isEnabled, lr.status as lastStatus, lr.passedCount as lastPassedCount,
                   lr.failedCount as lastFailedCount, lr.latencyMs as lastLatencyMs, lr.createdOn as lastRunOn, m.createdOn
            from monitors m
            join collections c on c.id = m.collectionId and c.isDeleted = 0
            left join environments e on e.id = m.environmentId and e.isDeleted = 0
            outer apply (select top 1 status, passedCount, failedCount, latencyMs, createdOn from monitorRuns where monitorId = m.id and isDeleted = 0 order by createdOn desc) lr
            where m.id = @MonitorId and m.isDeleted = 0;
            """, new { MonitorId = monitorId }, cancellationToken: cancellationToken));
    }

    public async Task<IReadOnlyList<ScheduledMonitorDto>> GetEnabledMonitorsAsync(CancellationToken cancellationToken)
    {
        using var connection = connectionFactory.CreateConnection();
        var rows = await connection.QueryAsync<ScheduledMonitorDto>(new CommandDefinition("""
            select id, organizationId, workspaceId, collectionId, environmentId, name, scheduleExpression, createdBy
            from monitors
            where isEnabled = 1 and isDeleted = 0;
            """, cancellationToken: cancellationToken));
        return rows.AsList();
    }

    public async Task AddMonitorRunAsync(Guid monitorId, string status, int passedCount, int failedCount, long? latencyMs, Guid userId, CancellationToken cancellationToken)
    {
        using var connection = connectionFactory.CreateConnection();
        await connection.ExecuteAsync(new CommandDefinition("""
            insert into monitorRuns (id, monitorId, status, passedCount, failedCount, latencyMs, createdOn, createdBy, isDeleted, versionNumber)
            values (newid(), @MonitorId, @Status, @PassedCount, @FailedCount, @LatencyMs, sysutcdatetime(), @UserId, 0, 1);
            """, new { MonitorId = monitorId, Status = status, PassedCount = passedCount, FailedCount = failedCount, LatencyMs = latencyMs, UserId = userId }, cancellationToken: cancellationToken));
    }

    public async Task<IReadOnlyList<MonitorRunDto>> GetMonitorRunsAsync(Guid monitorId, int count, CancellationToken cancellationToken)
    {
        using var connection = connectionFactory.CreateConnection();
        var rows = await connection.QueryAsync<MonitorRunDto>(new CommandDefinition("""
            select top (@Count) id, monitorId, status, passedCount, failedCount, latencyMs, createdOn
            from monitorRuns
            where monitorId = @MonitorId and isDeleted = 0
            order by createdOn desc;
            """, new { MonitorId = monitorId, Count = Math.Clamp(count, 1, 100) }, cancellationToken: cancellationToken));
        return rows.AsList();
    }

    public async Task<IReadOnlyList<PublishedDocDto>> GetPublishedDocsAsync(Guid workspaceId, CancellationToken cancellationToken)
    {
        using var connection = connectionFactory.CreateConnection();
        var rows = await connection.QueryAsync<PublishedDocDto>(new CommandDefinition("""
            select pd.id, pd.workspaceId, pd.collectionId, c.name as collectionName, pd.slug, pd.isPublic, pd.brandJson, pd.publishedOn, pd.createdOn
            from publishedDocs pd
            join collections c on c.id = pd.collectionId and c.isDeleted = 0
            where pd.workspaceId = @WorkspaceId and pd.isDeleted = 0
            order by pd.createdOn desc;
            """, new { WorkspaceId = workspaceId }, cancellationToken: cancellationToken));
        return rows.AsList();
    }

    public async Task<PublishedDocDto> PublishDocsAsync(Guid workspaceId, PublishDocsRequest request, string? passwordHash, Guid userId, CancellationToken cancellationToken)
    {
        using var connection = connectionFactory.CreateConnection();
        var docId = Guid.NewGuid();
        return await connection.QuerySingleAsync<PublishedDocDto>(new CommandDefinition("""
            declare @OrganizationId uniqueidentifier = (select organizationId from collections where id = @CollectionId and workspaceId = @WorkspaceId and isDeleted = 0);
            insert into publishedDocs (id, organizationId, workspaceId, collectionId, slug, isPublic, passwordHash, brandJson, publishedOn, createdOn, createdBy, isDeleted, versionNumber)
            values (@DocId, @OrganizationId, @WorkspaceId, @CollectionId, @Slug, @IsPublic, @PasswordHash, @BrandJson, sysutcdatetime(), sysutcdatetime(), @UserId, 0, 1);
            select pd.id, pd.workspaceId, pd.collectionId, c.name as collectionName, pd.slug, pd.isPublic, pd.brandJson, pd.publishedOn, pd.createdOn
            from publishedDocs pd
            join collections c on c.id = pd.collectionId
            where pd.id = @DocId;
            """, new { DocId = docId, WorkspaceId = workspaceId, request.CollectionId, Slug = Slugify(request.Slug), request.IsPublic, PasswordHash = passwordHash, request.BrandJson, UserId = userId }, cancellationToken: cancellationToken));
    }

    public async Task<bool> UnpublishDocsAsync(Guid docId, Guid userId, CancellationToken cancellationToken)
    {
        using var connection = connectionFactory.CreateConnection();
        var rows = await connection.ExecuteAsync(new CommandDefinition("""
            update publishedDocs
            set isDeleted = 1, modifiedOn = sysutcdatetime(), modifiedBy = @UserId, versionNumber = versionNumber + 1
            where id = @DocId and isDeleted = 0;
            """, new { DocId = docId, UserId = userId }, cancellationToken: cancellationToken));
        return rows > 0;
    }

    public async Task<DocumentationDto?> GetDocumentationAsync(string slug, CancellationToken cancellationToken)
    {
        using var connection = connectionFactory.CreateConnection();
        var doc = await connection.QuerySingleOrDefaultAsync<(Guid Id, Guid CollectionId, string Slug, string CollectionName, string? BrandJson)>(new CommandDefinition("""
            select pd.id, pd.collectionId, pd.slug, c.name as collectionName, pd.brandJson
            from publishedDocs pd
            join collections c on c.id = pd.collectionId and c.isDeleted = 0
            where pd.slug = @Slug and pd.isDeleted = 0 and pd.publishedOn is not null;
            """, new { Slug = slug }, cancellationToken: cancellationToken));
        if (doc.Id == Guid.Empty)
        {
            return null;
        }

        var rows = await connection.QueryAsync<DocumentationRequestRow>(new CommandDefinition("""
            select r.name, r.method, r.url, r.description, r.authType,
                   (select re.name from requestExamples re where re.requestId = r.id and re.isDeleted = 0 for json path) as examplesJson
            from requests r
            where r.collectionId = @CollectionId and r.isDeleted = 0
            order by r.name;
            """, new { doc.CollectionId }, cancellationToken: cancellationToken));
        return new DocumentationDto(doc.Id, doc.Slug, doc.CollectionName, doc.BrandJson, rows.Select(row => new DocumentationRequestDto(row.Name, row.Method, row.Url, row.Description, row.AuthType, ParseExampleNames(row.ExamplesJson))).ToList());
    }

    public async Task<PagedResult<ApiSpecDto>> GetApiSpecsAsync(Guid workspaceId, PagedRequest request, CancellationToken cancellationToken)
    {
        using var connection = connectionFactory.CreateConnection();
        var total = await connection.ExecuteScalarAsync<int>(new CommandDefinition("""
            select count(1) from apiSpecs where workspaceId = @WorkspaceId and isDeleted = 0 and (@Search is null or name like '%' + @Search + '%');
            """, new { WorkspaceId = workspaceId, Search = request.SearchString }, cancellationToken: cancellationToken));
        var rows = await connection.QueryAsync<ApiSpecDto>(new CommandDefinition("""
            select id, workspaceId, collectionId, name, format, validationStatus, createdOn
            from apiSpecs
            where workspaceId = @WorkspaceId and isDeleted = 0 and (@Search is null or name like '%' + @Search + '%')
            order by createdOn desc
            offset @Offset rows fetch next @Count rows only;
            """, new { WorkspaceId = workspaceId, Search = request.SearchString, request.Offset, Count = Math.Clamp(request.Count, 1, 100) }, cancellationToken: cancellationToken));
        return new PagedResult<ApiSpecDto>(rows.AsList(), total, request.Offset, Math.Clamp(request.Count, 1, 100));
    }

    public async Task<ApiSpecDto> UploadApiSpecAsync(Guid workspaceId, UploadApiSpecRequest request, string validationStatus, Guid userId, CancellationToken cancellationToken)
    {
        using var connection = connectionFactory.CreateConnection();
        var specId = Guid.NewGuid();
        return await connection.QuerySingleAsync<ApiSpecDto>(new CommandDefinition("""
            declare @OrganizationId uniqueidentifier = (select organizationId from workspaces where id = @WorkspaceId and isDeleted = 0);
            insert into apiSpecs (id, organizationId, workspaceId, collectionId, name, format, content, validationStatus, createdOn, createdBy, isDeleted, versionNumber)
            values (@SpecId, @OrganizationId, @WorkspaceId, @CollectionId, @Name, @Format, @Content, @ValidationStatus, sysutcdatetime(), @UserId, 0, 1);
            select id, workspaceId, collectionId, name, format, validationStatus, createdOn from apiSpecs where id = @SpecId;
            """, new { SpecId = specId, WorkspaceId = workspaceId, request.CollectionId, Name = request.Name.Trim(), Format = request.Format.Trim(), request.Content, ValidationStatus = validationStatus, UserId = userId }, cancellationToken: cancellationToken));
    }

    public async Task<string?> GetApiSpecContentAsync(Guid specId, CancellationToken cancellationToken)
    {
        using var connection = connectionFactory.CreateConnection();
        return await connection.ExecuteScalarAsync<string?>(new CommandDefinition("""
            select content from apiSpecs where id = @SpecId and isDeleted = 0;
            """, new { SpecId = specId }, cancellationToken: cancellationToken));
    }

    private static async Task<string> UniqueSlugAsync(System.Data.IDbConnection connection, System.Data.IDbTransaction transaction, string name, CancellationToken cancellationToken)
    {
        var baseSlug = Slugify(name);
        var slug = baseSlug;
        var index = 1;
        while (await connection.ExecuteScalarAsync<int>(new CommandDefinition("select count(1) from mockServers where slug = @Slug and isDeleted = 0;", new { Slug = slug }, transaction, cancellationToken: cancellationToken)) > 0)
        {
            index++;
            slug = $"{baseSlug}-{index}";
        }
        return slug;
    }

    private static string Slugify(string value)
    {
        var slug = Regex.Replace(value.Trim().ToLowerInvariant(), "[^a-z0-9]+", "-").Trim('-');
        return string.IsNullOrWhiteSpace(slug) ? Guid.NewGuid().ToString("N")[..12] : slug;
    }

    private static string NormalizePath(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "/";
        }

        var candidate = value.Trim();
        if (candidate.Contains("://", StringComparison.Ordinal))
        {
            try
            {
                candidate = new Uri(candidate).AbsolutePath;
            }
            catch
            {
                candidate = "/";
            }
        }

        if (candidate.Contains('?'))
        {
            candidate = candidate[..candidate.IndexOf('?')];
        }

        return candidate.StartsWith('/') ? candidate : $"/{candidate}";
    }

    private sealed record DocumentationRequestRow(string Name, string Method, string Url, string? Description, string? AuthType, string? ExamplesJson);

    private static IReadOnlyList<string> ParseExampleNames(string? examplesJson)
    {
        if (string.IsNullOrWhiteSpace(examplesJson))
        {
            return [];
        }

        try
        {
            using var document = System.Text.Json.JsonDocument.Parse(examplesJson);
            return document.RootElement.EnumerateArray()
                .Select(item => item.TryGetProperty("name", out var name) ? name.GetString() : null)
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Select(name => name!)
                .ToList();
        }
        catch
        {
            return [];
        }
    }
}
