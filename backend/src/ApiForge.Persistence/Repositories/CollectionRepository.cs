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

    public async Task<CollectionExportDto?> ExportCollectionAsync(Guid collectionId, CancellationToken cancellationToken)
    {
        using var connection = connectionFactory.CreateConnection();
        using var grid = await connection.QueryMultipleAsync(new CommandDefinition("""
            select c.id, c.workspaceId, c.name, c.description, c.ownerUserId, u.fullName as ownerName,
                   (select count(1) from requests r where r.collectionId = c.id and r.isDeleted = 0) as requestCount,
                   c.versionNumber, c.createdOn, c.modifiedOn
            from collections c
            join users u on u.id = c.ownerUserId
            where c.id = @CollectionId and c.isDeleted = 0;

            select r.id, r.name, r.description, r.method, r.url, r.authType, r.authConfigJson, r.bodyType,
                   rb.content as bodyContent, r.preRequestScript, r.testScript, r.timeoutMs, r.followRedirects, r.sslVerification
            from requests r
            outer apply (
                select top 1 content
                from requestBodies rb
                where rb.requestId = r.id and rb.isDeleted = 0
                order by rb.createdOn desc
            ) rb
            where r.collectionId = @CollectionId and r.isDeleted = 0
            order by coalesce(r.modifiedOn, r.createdOn), r.name;

            select h.requestId, h.[key], h.[value], h.enabled, h.isSecret
            from requestHeaders h
            join requests r on r.id = h.requestId and r.isDeleted = 0
            where r.collectionId = @CollectionId and h.isDeleted = 0
            order by h.requestId, h.sortOrder;

            select p.requestId, p.paramType, p.[key], p.[value], p.enabled, p.isSecret
            from requestParams p
            join requests r on r.id = p.requestId and r.isDeleted = 0
            where r.collectionId = @CollectionId and p.isDeleted = 0
            order by p.requestId, p.paramType, p.sortOrder;
            """,
            new { CollectionId = collectionId },
            cancellationToken: cancellationToken));

        var collection = await grid.ReadSingleOrDefaultAsync<CollectionDto>();
        if (collection is null)
        {
            return null;
        }

        var requestRows = (await grid.ReadAsync<ExportRequestRow>()).AsList();
        var headers = (await grid.ReadAsync<ChildKeyValueRow>()).GroupBy(row => row.RequestId).ToDictionary(group => group.Key, group => group.Select(ToKeyValue).ToList());
        var parameters = (await grid.ReadAsync<ParameterKeyValueRow>()).GroupBy(row => row.RequestId).ToDictionary(group => group.Key, group => group.ToList());

        var requests = requestRows.Select(row =>
        {
            parameters.TryGetValue(row.Id, out var paramRows);
            var queryParams = paramRows?.Where(item => item.ParamType.Equals("Query", StringComparison.OrdinalIgnoreCase)).Select(ToKeyValue).ToList() ?? [];
            var pathParams = paramRows?.Where(item => item.ParamType.Equals("Path", StringComparison.OrdinalIgnoreCase)).Select(ToKeyValue).ToList() ?? [];
            return new ApiRequestExportDto(
                row.Id,
                row.Name,
                row.Description,
                row.Method,
                row.Url,
                row.AuthType,
                row.AuthConfigJson,
                row.BodyType,
                row.BodyContent,
                row.PreRequestScript,
                row.TestScript,
                row.TimeoutMs,
                row.FollowRedirects,
                row.SslVerification,
                headers.TryGetValue(row.Id, out var headerRows) ? headerRows : [],
                queryParams,
                pathParams);
        }).ToList();

        return new CollectionExportDto("apidesk.collection.v1", collection, requests);
    }

    public async Task<CollectionImportResultDto> ImportCollectionAsync(Guid workspaceId, ImportCollectionRequest request, Guid userId, CancellationToken cancellationToken)
    {
        using var connection = connectionFactory.CreateConnection();
        connection.Open();
        using var transaction = connection.BeginTransaction();

        var organizationId = await connection.ExecuteScalarAsync<Guid>(new CommandDefinition(
            "select organizationId from workspaces where id = @WorkspaceId and isDeleted = 0;",
            new { WorkspaceId = workspaceId },
            transaction,
            cancellationToken: cancellationToken));

        var collectionId = Guid.NewGuid();
        await connection.ExecuteAsync(new CommandDefinition("""
            insert into collections (id, organizationId, workspaceId, name, description, ownerUserId, createdOn, createdBy, isDeleted, versionNumber)
            values (@CollectionId, @OrganizationId, @WorkspaceId, @Name, @Description, @UserId, sysutcdatetime(), @UserId, 0, 1);
            """,
            new { CollectionId = collectionId, OrganizationId = organizationId, WorkspaceId = workspaceId, request.Name, request.Description, UserId = userId },
            transaction,
            cancellationToken: cancellationToken));

        var importedRequests = request.Requests ?? [];
        var requestRows = importedRequests.Select(item => new ImportRequestRow
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            WorkspaceId = workspaceId,
            CollectionId = collectionId,
            Name = string.IsNullOrWhiteSpace(item.Name) ? "Imported request" : item.Name.Trim(),
            Description = item.Description,
            Method = string.IsNullOrWhiteSpace(item.Method) ? "GET" : item.Method.Trim().ToUpperInvariant(),
            Url = string.IsNullOrWhiteSpace(item.Url) ? "https://example.com" : item.Url.Trim(),
            AuthType = item.AuthType,
            AuthConfigJson = item.AuthConfigJson,
            BodyType = string.IsNullOrWhiteSpace(item.BodyType) ? "none" : item.BodyType,
            BodyContent = item.BodyContent,
            PreRequestScript = item.PreRequestScript,
            TestScript = item.TestScript,
            TimeoutMs = item.TimeoutMs <= 0 ? 30000 : Math.Clamp(item.TimeoutMs, 1000, 120000),
            FollowRedirects = item.FollowRedirects,
            SslVerification = item.SslVerification,
            UserId = userId,
            Source = item
        }).ToList();

        if (requestRows.Count > 0)
        {
            await connection.ExecuteAsync(new CommandDefinition("""
                insert into requests
                (id, organizationId, workspaceId, collectionId, folderId, name, description, method, url, authType, authConfigJson, bodyType,
                 preRequestScript, testScript, timeoutMs, followRedirects, sslVerification, ownerUserId, lastModifiedByUserId, createdOn, createdBy, isDeleted, versionNumber)
                values
                (@Id, @OrganizationId, @WorkspaceId, @CollectionId, null, @Name, @Description, @Method, @Url, @AuthType, @AuthConfigJson, @BodyType,
                 @PreRequestScript, @TestScript, @TimeoutMs, @FollowRedirects, @SslVerification, @UserId, @UserId, sysutcdatetime(), @UserId, 0, 1);
                """,
                requestRows,
                transaction,
                cancellationToken: cancellationToken));

            var headerRows = requestRows.SelectMany(row => (row.Source.Headers ?? []).Where(item => !string.IsNullOrWhiteSpace(item.Key)).Select((item, index) => new
            {
                Id = Guid.NewGuid(),
                RequestId = row.Id,
                item.Key,
                item.Value,
                item.Enabled,
                item.IsSecret,
                SortOrder = index,
                UserId = userId
            })).ToList();

            if (headerRows.Count > 0)
            {
                await connection.ExecuteAsync(new CommandDefinition("""
                    insert into requestHeaders (id, requestId, [key], [value], enabled, isSecret, sortOrder, createdOn, createdBy, isDeleted, versionNumber)
                    values (@Id, @RequestId, @Key, @Value, @Enabled, @IsSecret, @SortOrder, sysutcdatetime(), @UserId, 0, 1);
                    """, headerRows, transaction, cancellationToken: cancellationToken));
            }

            var paramRows = requestRows.SelectMany(row =>
                    (row.Source.QueryParams ?? []).Where(item => !string.IsNullOrWhiteSpace(item.Key)).Select((item, index) => ToImportParam(row.Id, item, "Query", index, userId))
                    .Concat((row.Source.PathParams ?? []).Where(item => !string.IsNullOrWhiteSpace(item.Key)).Select((item, index) => ToImportParam(row.Id, item, "Path", index, userId))))
                .ToList();

            if (paramRows.Count > 0)
            {
                await connection.ExecuteAsync(new CommandDefinition("""
                    insert into requestParams (id, requestId, paramType, [key], [value], enabled, isSecret, sortOrder, createdOn, createdBy, isDeleted, versionNumber)
                    values (@Id, @RequestId, @ParamType, @Key, @Value, @Enabled, @IsSecret, @SortOrder, sysutcdatetime(), @UserId, 0, 1);
                    """, paramRows, transaction, cancellationToken: cancellationToken));
            }

            await connection.ExecuteAsync(new CommandDefinition("""
                insert into requestBodies (id, requestId, bodyType, content, createdOn, createdBy, isDeleted, versionNumber)
                values (newid(), @Id, @BodyType, @BodyContent, sysutcdatetime(), @UserId, 0, 1);
                """, requestRows, transaction, cancellationToken: cancellationToken));

            var versionRows = requestRows.Select(row => new
            {
                RequestId = row.Id,
                SnapshotJson = JsonSerializer.Serialize(ToSaveRequest(workspaceId, collectionId, row.Source)),
                UserId = userId
            }).ToList();
            await connection.ExecuteAsync(new CommandDefinition("""
                insert into requestVersions (id, requestId, versionNumber, snapshotJson, createdOn, createdBy, isDeleted)
                values (newid(), @RequestId, 1, @SnapshotJson, sysutcdatetime(), @UserId, 0);
                """, versionRows, transaction, cancellationToken: cancellationToken));
        }

        transaction.Commit();
        return new CollectionImportResultDto(collectionId, request.Name, requestRows.Count);
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

    private static KeyValueItemDto ToKeyValue(ChildKeyValueRow row) => new(row.Key, row.Value, row.Enabled, row.IsSecret);
    private static KeyValueItemDto ToKeyValue(ParameterKeyValueRow row) => new(row.Key, row.Value, row.Enabled, row.IsSecret);

    private static object ToImportParam(Guid requestId, KeyValueItemDto item, string paramType, int sortOrder, Guid userId) => new
    {
        Id = Guid.NewGuid(),
        RequestId = requestId,
        ParamType = paramType,
        item.Key,
        item.Value,
        item.Enabled,
        item.IsSecret,
        SortOrder = sortOrder,
        UserId = userId
    };

    private static SaveApiRequestRequest ToSaveRequest(Guid workspaceId, Guid collectionId, ImportApiRequestRequest request) => new(
        workspaceId,
        collectionId,
        request.Name,
        request.Description,
        request.Method,
        request.Url,
        request.AuthType,
        request.AuthConfigJson,
        request.BodyType,
        request.BodyContent,
        request.PreRequestScript,
        request.TestScript,
        request.TimeoutMs,
        request.FollowRedirects,
        request.SslVerification,
        request.Headers ?? [],
        request.QueryParams ?? [],
        request.PathParams ?? [],
        1);

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

    private sealed class ExportRequestRow
    {
        public Guid Id { get; init; }
        public string Name { get; init; } = string.Empty;
        public string? Description { get; init; }
        public string Method { get; init; } = "GET";
        public string Url { get; init; } = string.Empty;
        public string? AuthType { get; init; }
        public string? AuthConfigJson { get; init; }
        public string BodyType { get; init; } = "none";
        public string? BodyContent { get; init; }
        public string? PreRequestScript { get; init; }
        public string? TestScript { get; init; }
        public int TimeoutMs { get; init; }
        public bool FollowRedirects { get; init; }
        public bool SslVerification { get; init; }
    }

    private class ChildKeyValueRow
    {
        public Guid RequestId { get; init; }
        public string Key { get; init; } = string.Empty;
        public string? Value { get; init; }
        public bool Enabled { get; init; }
        public bool IsSecret { get; init; }
    }

    private sealed class ParameterKeyValueRow : ChildKeyValueRow
    {
        public string ParamType { get; init; } = string.Empty;
    }

    private sealed class ImportRequestRow
    {
        public Guid Id { get; init; }
        public Guid OrganizationId { get; init; }
        public Guid WorkspaceId { get; init; }
        public Guid CollectionId { get; init; }
        public string Name { get; init; } = string.Empty;
        public string? Description { get; init; }
        public string Method { get; init; } = "GET";
        public string Url { get; init; } = string.Empty;
        public string? AuthType { get; init; }
        public string? AuthConfigJson { get; init; }
        public string BodyType { get; init; } = "none";
        public string? BodyContent { get; init; }
        public string? PreRequestScript { get; init; }
        public string? TestScript { get; init; }
        public int TimeoutMs { get; init; }
        public bool FollowRedirects { get; init; }
        public bool SslVerification { get; init; }
        public Guid UserId { get; init; }
        public ImportApiRequestRequest Source { get; init; } = new(
            string.Empty,
            null,
            "GET",
            string.Empty,
            null,
            null,
            "none",
            null,
            null,
            null,
            30000,
            true,
            true,
            [],
            [],
            []);
    }
}
