using System.Text.Json;
using ApiForge.Application.Abstractions.Persistence;
using ApiForge.Application.DTOs.Collections;
using ApiForge.Application.DTOs.Requests;
using ApiForge.Shared.Pagination;
using Dapper;
using ApiForge.Persistence.Connection;

namespace ApiForge.Persistence.Repositories;

public sealed class CollectionRepository(ISqlConnectionFactory connectionFactory) : ICollectionRepository
{
    public async Task<Guid?> GetWorkspaceOrganizationIdAsync(Guid workspaceId, CancellationToken cancellationToken)
    {
        using var connection = connectionFactory.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<Guid?>(new CommandDefinition(
            "select organizationId from workspaces where id = @WorkspaceId and isDeleted = 0;",
            new { WorkspaceId = workspaceId },
            cancellationToken: cancellationToken));
    }

    public async Task<(Guid WorkspaceId, Guid OrganizationId)?> GetCollectionScopeAsync(Guid collectionId, CancellationToken cancellationToken)
    {
        using var connection = connectionFactory.CreateConnection();
        var row = await connection.QuerySingleOrDefaultAsync<(Guid WorkspaceId, Guid OrganizationId)>(new CommandDefinition("""
            select c.workspaceId, c.organizationId
            from collections c
            where c.id = @CollectionId and c.isDeleted = 0;
            """, new { CollectionId = collectionId }, cancellationToken: cancellationToken));
        return row.WorkspaceId == Guid.Empty ? null : row;
    }

    public async Task<(Guid WorkspaceId, Guid OrganizationId)?> GetFolderScopeAsync(Guid folderId, CancellationToken cancellationToken)
    {
        using var connection = connectionFactory.CreateConnection();
        var row = await connection.QuerySingleOrDefaultAsync<(Guid WorkspaceId, Guid OrganizationId)>(new CommandDefinition("""
            select f.workspaceId, f.organizationId
            from folders f
            where f.id = @FolderId and f.isDeleted = 0;
            """, new { FolderId = folderId }, cancellationToken: cancellationToken));
        return row.WorkspaceId == Guid.Empty ? null : row;
    }

    public async Task<(Guid WorkspaceId, Guid OrganizationId)?> GetRequestScopeAsync(Guid requestId, CancellationToken cancellationToken)
    {
        using var connection = connectionFactory.CreateConnection();
        var row = await connection.QuerySingleOrDefaultAsync<(Guid WorkspaceId, Guid OrganizationId)>(new CommandDefinition("""
            select r.workspaceId, r.organizationId
            from requests r
            where r.id = @RequestId and r.isDeleted = 0;
            """, new { RequestId = requestId }, cancellationToken: cancellationToken));
        return row.WorkspaceId == Guid.Empty ? null : row;
    }

    public async Task<PagedResult<CollectionDto>> GetCollectionsAsync(Guid workspaceId, PagedRequest request, CancellationToken cancellationToken)
    {
        using var connection = connectionFactory.CreateConnection();
        using var grid = await connection.QueryMultipleAsync(new CommandDefinition("""
            select count(1)
            from collections c
            where c.workspaceId = @WorkspaceId and c.isDeleted = 0
                and (@Search is null or c.name like '%' + @Search + '%');

            select c.id, c.workspaceId, c.name, c.description, c.ownerUserId, u.fullName as ownerName,
                   (select count(1) from requests r where r.collectionId = c.id and r.isDeleted = 0) as requestCount,
                   c.versionNumber, c.createdOn, c.modifiedOn
            from collections c
            join users u on u.id = c.ownerUserId
            where c.workspaceId = @WorkspaceId and c.isDeleted = 0
                and (@Search is null or c.name like '%' + @Search + '%')
            order by c.modifiedOn desc, c.createdOn desc
            offset @Offset rows fetch next @Count rows only;
            """,
            new { WorkspaceId = workspaceId, Search = request.SearchString, Offset = request.SafeOffset, Count = request.SafeCount },
            cancellationToken: cancellationToken));

        var total = await grid.ReadSingleAsync<int>();
        var items = (await grid.ReadAsync<CollectionDto>()).AsList();
        return new PagedResult<CollectionDto>(items, total, request.SafeOffset, request.SafeCount);
    }

    public async Task<IReadOnlyList<ApiRequestSummaryDto>> GetCollectionRequestsAsync(Guid collectionId, CancellationToken cancellationToken)
    {
        using var connection = connectionFactory.CreateConnection();
        var rows = await connection.QueryAsync<ApiRequestSummaryDto>(new CommandDefinition("""
            select id, collectionId, folderId, name, method, url, coalesce(modifiedOn, createdOn) as modifiedOn
            from requests
            where collectionId = @CollectionId and isDeleted = 0
            order by coalesce(modifiedOn, createdOn) desc;
            """,
            new { CollectionId = collectionId },
            cancellationToken: cancellationToken));
        return rows.AsList();
    }

    public async Task<CollectionDto> CreateCollectionAsync(CreateCollectionRequest request, Guid userId, CancellationToken cancellationToken)
    {
        using var connection = connectionFactory.CreateConnection();
        var organizationId = await GetWorkspaceOrganizationIdAsync(request.WorkspaceId, cancellationToken)
            ?? throw new InvalidOperationException("Workspace not found.");
        var collectionId = Guid.NewGuid();

        await connection.ExecuteAsync(new CommandDefinition("""
            insert into collections (id, organizationId, workspaceId, name, description, ownerUserId, createdOn, createdBy, isDeleted, versionNumber)
            values (@CollectionId, @OrganizationId, @WorkspaceId, @Name, @Description, @UserId, sysutcdatetime(), @UserId, 0, 1);
            """,
            new { CollectionId = collectionId, OrganizationId = organizationId, request.WorkspaceId, request.Name, request.Description, UserId = userId },
            cancellationToken: cancellationToken));

        return new CollectionDto(collectionId, request.WorkspaceId, request.Name, request.Description, userId, string.Empty, 0, 1, DateTime.UtcNow, null);
    }

    public async Task<CollectionDto?> UpdateCollectionAsync(Guid collectionId, UpdateCollectionRequest request, Guid userId, CancellationToken cancellationToken)
    {
        using var connection = connectionFactory.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<CollectionDto>(new CommandDefinition("""
            update collections
            set name = @Name,
                description = @Description,
                modifiedOn = sysutcdatetime(),
                modifiedBy = @UserId,
                versionNumber = versionNumber + 1
            output inserted.id, inserted.workspaceId, inserted.name, inserted.description, inserted.ownerUserId, '' as ownerName,
                   0 as requestCount, inserted.versionNumber, inserted.createdOn, inserted.modifiedOn
            where id = @CollectionId and isDeleted = 0 and versionNumber = @VersionNumber;
            """,
            new { CollectionId = collectionId, request.Name, request.Description, request.VersionNumber, UserId = userId },
            cancellationToken: cancellationToken));
    }

    public async Task<bool> DeleteCollectionAsync(Guid collectionId, Guid userId, CancellationToken cancellationToken)
    {
        using var connection = connectionFactory.CreateConnection();
        var rows = await connection.ExecuteAsync(new CommandDefinition("""
            update collections
            set isDeleted = 1, modifiedOn = sysutcdatetime(), modifiedBy = @UserId, versionNumber = versionNumber + 1
            where id = @CollectionId and isDeleted = 0;
            """,
            new { CollectionId = collectionId, UserId = userId },
            cancellationToken: cancellationToken));
        return rows > 0;
    }

    public async Task<FolderDto> CreateFolderAsync(Guid collectionId, CreateFolderRequest request, Guid userId, CancellationToken cancellationToken)
    {
        using var connection = connectionFactory.CreateConnection();
        var scope = await GetCollectionScopeAsync(collectionId, cancellationToken)
            ?? throw new InvalidOperationException("Collection not found.");
        var folderId = Guid.NewGuid();

        await connection.ExecuteAsync(new CommandDefinition("""
            insert into folders (id, organizationId, workspaceId, collectionId, parentFolderId, name, sortOrder, createdOn, createdBy, isDeleted, versionNumber)
            values (@FolderId, @OrganizationId, @WorkspaceId, @CollectionId, @ParentFolderId, @Name, @SortOrder, sysutcdatetime(), @UserId, 0, 1);
            """,
            new { FolderId = folderId, scope.OrganizationId, scope.WorkspaceId, CollectionId = collectionId, request.ParentFolderId, request.Name, request.SortOrder, UserId = userId },
            cancellationToken: cancellationToken));

        return new FolderDto(folderId, collectionId, request.ParentFolderId, request.Name, request.SortOrder, DateTime.UtcNow);
    }

    public async Task<ApiRequestDetailDto> CreateRequestAsync(Guid? folderId, SaveApiRequestRequest request, Guid userId, CancellationToken cancellationToken)
    {
        using var connection = connectionFactory.CreateConnection();
        connection.Open();
        using var transaction = connection.BeginTransaction();

        var organizationId = await connection.ExecuteScalarAsync<Guid>(new CommandDefinition(
            "select organizationId from workspaces where id = @WorkspaceId and isDeleted = 0;",
            new { request.WorkspaceId },
            transaction,
            cancellationToken: cancellationToken));
        var requestId = Guid.NewGuid();

        await connection.ExecuteAsync(new CommandDefinition("""
            insert into requests
            (id, organizationId, workspaceId, collectionId, folderId, name, description, method, url, authType, authConfigJson, bodyType,
             preRequestScript, testScript, timeoutMs, followRedirects, sslVerification, ownerUserId, lastModifiedByUserId, createdOn, createdBy, isDeleted, versionNumber)
            values
            (@RequestId, @OrganizationId, @WorkspaceId, @CollectionId, @FolderId, @Name, @Description, @Method, @Url, @AuthType, @AuthConfigJson, @BodyType,
             @PreRequestScript, @TestScript, @TimeoutMs, @FollowRedirects, @SslVerification, @UserId, @UserId, sysutcdatetime(), @UserId, 0, 1);
            """,
            new
            {
                RequestId = requestId,
                OrganizationId = organizationId,
                request.WorkspaceId,
                request.CollectionId,
                FolderId = folderId,
                request.Name,
                request.Description,
                request.Method,
                request.Url,
                request.AuthType,
                request.AuthConfigJson,
                request.BodyType,
                request.PreRequestScript,
                request.TestScript,
                request.TimeoutMs,
                request.FollowRedirects,
                request.SslVerification,
                UserId = userId
            },
            transaction,
            cancellationToken: cancellationToken));

        await ReplaceRequestChildrenAsync(connection, transaction, requestId, request, userId, cancellationToken);
        await InsertVersionAsync(connection, transaction, requestId, 1, request, userId, cancellationToken);
        transaction.Commit();

        return (await GetRequestAsync(requestId, cancellationToken))!;
    }

    public async Task<ApiRequestDetailDto?> GetRequestAsync(Guid requestId, CancellationToken cancellationToken)
    {
        using var connection = connectionFactory.CreateConnection();
        using var grid = await connection.QueryMultipleAsync(new CommandDefinition("""
            select id, workspaceId, collectionId, folderId, name, description, method, url, authType, authConfigJson, bodyType,
                   timeoutMs, followRedirects, sslVerification, versionNumber, createdOn, modifiedOn,
                   (select top 1 content from requestBodies where requestId = r.id and isDeleted = 0 order by createdOn desc) as bodyContent
            from requests r
            where id = @RequestId and isDeleted = 0;

            select [key], [value], enabled, isSecret
            from requestHeaders
            where requestId = @RequestId and isDeleted = 0
            order by sortOrder;

            select [key], [value], enabled, isSecret
            from requestParams
            where requestId = @RequestId and paramType = 'Query' and isDeleted = 0
            order by sortOrder;

            select [key], [value], enabled, isSecret
            from requestParams
            where requestId = @RequestId and paramType = 'Path' and isDeleted = 0
            order by sortOrder;
            """, new { RequestId = requestId }, cancellationToken: cancellationToken));

        var row = await grid.ReadSingleOrDefaultAsync<RequestRow>();
        if (row is null)
        {
            return null;
        }

        var headers = (await grid.ReadAsync<KeyValueItemDto>()).AsList();
        var query = (await grid.ReadAsync<KeyValueItemDto>()).AsList();
        var path = (await grid.ReadAsync<KeyValueItemDto>()).AsList();

        return new ApiRequestDetailDto(
            row.Id,
            row.WorkspaceId,
            row.CollectionId,
            row.FolderId,
            row.Name,
            row.Description,
            row.Method,
            row.Url,
            row.AuthType,
            row.AuthConfigJson,
            row.BodyType,
            row.BodyContent,
            row.TimeoutMs,
            row.FollowRedirects,
            row.SslVerification,
            headers,
            query,
            path,
            row.VersionNumber,
            row.CreatedOn,
            row.ModifiedOn);
    }

    public async Task<ApiRequestDetailDto?> UpdateRequestAsync(Guid requestId, SaveApiRequestRequest request, Guid userId, CancellationToken cancellationToken)
    {
        using var connection = connectionFactory.CreateConnection();
        connection.Open();
        using var transaction = connection.BeginTransaction();

        var rows = await connection.ExecuteAsync(new CommandDefinition("""
            update requests
            set name = @Name,
                description = @Description,
                method = @Method,
                url = @Url,
                authType = @AuthType,
                authConfigJson = @AuthConfigJson,
                bodyType = @BodyType,
                preRequestScript = @PreRequestScript,
                testScript = @TestScript,
                timeoutMs = @TimeoutMs,
                followRedirects = @FollowRedirects,
                sslVerification = @SslVerification,
                lastModifiedByUserId = @UserId,
                modifiedOn = sysutcdatetime(),
                modifiedBy = @UserId,
                versionNumber = versionNumber + 1
            where id = @RequestId and isDeleted = 0 and versionNumber = @VersionNumber;
            """,
            new
            {
                RequestId = requestId,
                request.Name,
                request.Description,
                request.Method,
                request.Url,
                request.AuthType,
                request.AuthConfigJson,
                request.BodyType,
                request.PreRequestScript,
                request.TestScript,
                request.TimeoutMs,
                request.FollowRedirects,
                request.SslVerification,
                request.VersionNumber,
                UserId = userId
            },
            transaction,
            cancellationToken: cancellationToken));

        if (rows == 0)
        {
            transaction.Rollback();
            return null;
        }

        await ReplaceRequestChildrenAsync(connection, transaction, requestId, request, userId, cancellationToken);
        await InsertVersionAsync(connection, transaction, requestId, request.VersionNumber + 1, request, userId, cancellationToken);
        transaction.Commit();

        return await GetRequestAsync(requestId, cancellationToken);
    }

    private static async Task ReplaceRequestChildrenAsync(System.Data.IDbConnection connection, System.Data.IDbTransaction transaction, Guid requestId, SaveApiRequestRequest request, Guid userId, CancellationToken cancellationToken)
    {
        await connection.ExecuteAsync(new CommandDefinition("""
            update requestHeaders set isDeleted = 1, modifiedOn = sysutcdatetime(), modifiedBy = @UserId where requestId = @RequestId;
            update requestParams set isDeleted = 1, modifiedOn = sysutcdatetime(), modifiedBy = @UserId where requestId = @RequestId;
            update requestBodies set isDeleted = 1, modifiedOn = sysutcdatetime(), modifiedBy = @UserId where requestId = @RequestId;
            """, new { RequestId = requestId, UserId = userId }, transaction, cancellationToken: cancellationToken));

        var headerRows = request.Headers.Select((h, index) => new { Id = Guid.NewGuid(), RequestId = requestId, h.Key, h.Value, h.Enabled, h.IsSecret, SortOrder = index, UserId = userId });
        await connection.ExecuteAsync(new CommandDefinition("""
            insert into requestHeaders (id, requestId, [key], [value], enabled, isSecret, sortOrder, createdOn, createdBy, isDeleted, versionNumber)
            values (@Id, @RequestId, @Key, @Value, @Enabled, @IsSecret, @SortOrder, sysutcdatetime(), @UserId, 0, 1);
            """, headerRows, transaction, cancellationToken: cancellationToken));

        var queryRows = request.QueryParams.Select((p, index) => new { Id = Guid.NewGuid(), RequestId = requestId, p.Key, p.Value, p.Enabled, p.IsSecret, ParamType = "Query", SortOrder = index, UserId = userId });
        var pathRows = request.PathParams.Select((p, index) => new { Id = Guid.NewGuid(), RequestId = requestId, p.Key, p.Value, p.Enabled, p.IsSecret, ParamType = "Path", SortOrder = index, UserId = userId });
        await connection.ExecuteAsync(new CommandDefinition("""
            insert into requestParams (id, requestId, paramType, [key], [value], enabled, isSecret, sortOrder, createdOn, createdBy, isDeleted, versionNumber)
            values (@Id, @RequestId, @ParamType, @Key, @Value, @Enabled, @IsSecret, @SortOrder, sysutcdatetime(), @UserId, 0, 1);
            """, queryRows.Concat(pathRows), transaction, cancellationToken: cancellationToken));

        await connection.ExecuteAsync(new CommandDefinition("""
            insert into requestBodies (id, requestId, bodyType, content, createdOn, createdBy, isDeleted, versionNumber)
            values (newid(), @RequestId, @BodyType, @BodyContent, sysutcdatetime(), @UserId, 0, 1);
            """, new { RequestId = requestId, request.BodyType, request.BodyContent, UserId = userId }, transaction, cancellationToken: cancellationToken));
    }

    private static Task InsertVersionAsync(System.Data.IDbConnection connection, System.Data.IDbTransaction transaction, Guid requestId, int versionNumber, SaveApiRequestRequest request, Guid userId, CancellationToken cancellationToken)
    {
        var snapshot = JsonSerializer.Serialize(request);
        return connection.ExecuteAsync(new CommandDefinition("""
            insert into requestVersions (id, requestId, versionNumber, snapshotJson, createdOn, createdBy, isDeleted)
            values (newid(), @RequestId, @VersionNumber, @SnapshotJson, sysutcdatetime(), @UserId, 0);
            """, new { RequestId = requestId, VersionNumber = versionNumber, SnapshotJson = snapshot, UserId = userId }, transaction, cancellationToken: cancellationToken));
    }

    private sealed class RequestRow
    {
        public Guid Id { get; init; }
        public Guid WorkspaceId { get; init; }
        public Guid CollectionId { get; init; }
        public Guid? FolderId { get; init; }
        public string Name { get; init; } = string.Empty;
        public string? Description { get; init; }
        public string Method { get; init; } = "GET";
        public string Url { get; init; } = string.Empty;
        public string? AuthType { get; init; }
        public string? AuthConfigJson { get; init; }
        public string BodyType { get; init; } = "none";
        public string? BodyContent { get; init; }
        public int TimeoutMs { get; init; }
        public bool FollowRedirects { get; init; }
        public bool SslVerification { get; init; }
        public int VersionNumber { get; init; }
        public DateTime CreatedOn { get; init; }
        public DateTime? ModifiedOn { get; init; }
    }
}
