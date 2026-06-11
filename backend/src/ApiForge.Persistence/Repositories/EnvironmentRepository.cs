using ApiForge.Application.Abstractions.Persistence;
using ApiForge.Application.DTOs.Environments;
using ApiForge.Shared.Pagination;
using ApiForge.Shared.Security;
using Dapper;
using ApiForge.Persistence.Connection;

namespace ApiForge.Persistence.Repositories;

public sealed class EnvironmentRepository(ISqlConnectionFactory connectionFactory) : IEnvironmentRepository
{
    public async Task<Guid?> GetWorkspaceOrganizationIdByEnvironmentAsync(Guid environmentId, CancellationToken cancellationToken)
    {
        using var connection = connectionFactory.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<Guid?>(new CommandDefinition(
            "select organizationId from environments where id = @EnvironmentId and isDeleted = 0;",
            new { EnvironmentId = environmentId },
            cancellationToken: cancellationToken));
    }

    public async Task<(Guid OrganizationId, Guid WorkspaceId)?> GetEnvironmentScopeAsync(Guid environmentId, CancellationToken cancellationToken)
    {
        using var connection = connectionFactory.CreateConnection();
        var row = await connection.QuerySingleOrDefaultAsync<(Guid OrganizationId, Guid WorkspaceId)>(new CommandDefinition(
            "select organizationId, workspaceId from environments where id = @EnvironmentId and isDeleted = 0;",
            new { EnvironmentId = environmentId },
            cancellationToken: cancellationToken));
        return row.OrganizationId == Guid.Empty ? null : row;
    }

    public async Task<PagedResult<EnvironmentDto>> GetEnvironmentsAsync(Guid workspaceId, PagedRequest request, CancellationToken cancellationToken)
    {
        using var connection = connectionFactory.CreateConnection();
        using var grid = await connection.QueryMultipleAsync(new CommandDefinition("""
            select count(1)
            from environments
            where workspaceId = @WorkspaceId and isDeleted = 0
                and (@Search is null or name like '%' + @Search + '%');

            select e.id, e.workspaceId, e.name, e.isDefault,
                   count(ev.id) as variableCount,
                   sum(case when ev.isSecret = 1 then 1 else 0 end) as secretCount,
                   e.versionNumber, e.createdOn, e.modifiedOn
            from environments e
            left join environmentVariables ev on ev.environmentId = e.id and ev.isDeleted = 0
            where e.workspaceId = @WorkspaceId and e.isDeleted = 0
                and (@Search is null or e.name like '%' + @Search + '%')
            group by e.id, e.workspaceId, e.name, e.isDefault, e.versionNumber, e.createdOn, e.modifiedOn
            order by e.isDefault desc, e.name
            offset @Offset rows fetch next @Count rows only;
            """,
            new { WorkspaceId = workspaceId, Search = request.SearchString, Offset = request.SafeOffset, Count = request.SafeCount },
            cancellationToken: cancellationToken));

        var total = await grid.ReadSingleAsync<int>();
        var items = (await grid.ReadAsync<EnvironmentDto>()).AsList();
        return new PagedResult<EnvironmentDto>(items, total, request.SafeOffset, request.SafeCount);
    }

    public async Task<EnvironmentDto> CreateAsync(CreateEnvironmentRequest request, Guid userId, CancellationToken cancellationToken)
    {
        using var connection = connectionFactory.CreateConnection();
        var organizationId = await connection.ExecuteScalarAsync<Guid>(new CommandDefinition(
            "select organizationId from workspaces where id = @WorkspaceId and isDeleted = 0;",
            new { request.WorkspaceId },
            cancellationToken: cancellationToken));
        var environmentId = Guid.NewGuid();

        await connection.ExecuteAsync(new CommandDefinition("""
            update environments set isDefault = 0 where workspaceId = @WorkspaceId and @IsDefault = 1;

            insert into environments (id, organizationId, workspaceId, name, isDefault, createdOn, createdBy, isDeleted, versionNumber)
            values (@EnvironmentId, @OrganizationId, @WorkspaceId, @Name, @IsDefault, sysutcdatetime(), @UserId, 0, 1);
            """,
            new { EnvironmentId = environmentId, OrganizationId = organizationId, request.WorkspaceId, request.Name, request.IsDefault, UserId = userId },
            cancellationToken: cancellationToken));

        return new EnvironmentDto(environmentId, request.WorkspaceId, request.Name, request.IsDefault, 0, 0, 1, DateTime.UtcNow, null);
    }

    public async Task<EnvironmentDto?> UpdateAsync(Guid environmentId, UpdateEnvironmentRequest request, Guid userId, CancellationToken cancellationToken)
    {
        using var connection = connectionFactory.CreateConnection();
        connection.Open();
        using var transaction = connection.BeginTransaction();

        var scope = await connection.QuerySingleOrDefaultAsync<(Guid OrganizationId, Guid WorkspaceId)>(new CommandDefinition(
            "select organizationId, workspaceId from environments where id = @EnvironmentId and isDeleted = 0;",
            new { EnvironmentId = environmentId },
            transaction,
            cancellationToken: cancellationToken));
        if (scope.OrganizationId == Guid.Empty)
        {
            transaction.Rollback();
            return null;
        }

        if (request.IsDefault)
        {
            await connection.ExecuteAsync(new CommandDefinition(
                "update environments set isDefault = 0, modifiedOn = sysutcdatetime(), modifiedBy = @UserId where workspaceId = @WorkspaceId and isDeleted = 0;",
                new { scope.WorkspaceId, UserId = userId },
                transaction,
                cancellationToken: cancellationToken));
        }

        await connection.ExecuteAsync(new CommandDefinition("""
            update environments
            set name = @Name,
                isDefault = @IsDefault,
                modifiedOn = sysutcdatetime(),
                modifiedBy = @UserId,
                versionNumber = versionNumber + 1
            where id = @EnvironmentId and isDeleted = 0;
            """,
            new { EnvironmentId = environmentId, request.Name, request.IsDefault, UserId = userId },
            transaction,
            cancellationToken: cancellationToken));

        transaction.Commit();
        return await GetEnvironmentAsync(environmentId, cancellationToken);
    }

    public async Task<EnvironmentDto?> DuplicateAsync(Guid environmentId, DuplicateEnvironmentRequest request, Guid userId, CancellationToken cancellationToken)
    {
        using var connection = connectionFactory.CreateConnection();
        connection.Open();
        using var transaction = connection.BeginTransaction();

        var source = await connection.QuerySingleOrDefaultAsync<EnvironmentSourceRow>(new CommandDefinition("""
            select id, organizationId, workspaceId, name, isDefault
            from environments
            where id = @EnvironmentId and isDeleted = 0;
            """,
            new { EnvironmentId = environmentId },
            transaction,
            cancellationToken: cancellationToken));
        if (source is null)
        {
            transaction.Rollback();
            return null;
        }

        var duplicateId = Guid.NewGuid();
        var duplicateName = string.IsNullOrWhiteSpace(request.Name) ? $"{source.Name} Copy" : request.Name!.Trim();

        if (request.IsDefault)
        {
            await connection.ExecuteAsync(new CommandDefinition(
                "update environments set isDefault = 0, modifiedOn = sysutcdatetime(), modifiedBy = @UserId where workspaceId = @WorkspaceId and isDeleted = 0;",
                new { source.WorkspaceId, UserId = userId },
                transaction,
                cancellationToken: cancellationToken));
        }

        await connection.ExecuteAsync(new CommandDefinition("""
            insert into environments (id, organizationId, workspaceId, name, isDefault, createdOn, createdBy, isDeleted, versionNumber)
            values (@DuplicateId, @OrganizationId, @WorkspaceId, @Name, @IsDefault, sysutcdatetime(), @UserId, 0, 1);

            insert into environmentVariables
            (id, organizationId, workspaceId, collectionId, environmentId, userId, [key], [value], scope, isSecret, enabled, createdOn, createdBy, isDeleted, versionNumber)
            select newid(), organizationId, workspaceId, collectionId, @DuplicateId, userId, [key], [value], scope, isSecret, enabled, sysutcdatetime(), @UserId, 0, 1
            from environmentVariables
            where environmentId = @EnvironmentId and isDeleted = 0;

            insert into environmentVersions (id, environmentId, snapshotJson, createdOn, createdBy, isDeleted)
            select newid(), @DuplicateId,
                   (select [key], case when isSecret = 1 then '********' else [value] end as [value], scope, isSecret, enabled
                    from environmentVariables
                    where environmentId = @DuplicateId and isDeleted = 0
                    for json path),
                   sysutcdatetime(), @UserId, 0;
            """,
            new
            {
                DuplicateId = duplicateId,
                source.OrganizationId,
                source.WorkspaceId,
                Name = duplicateName,
                request.IsDefault,
                UserId = userId,
                EnvironmentId = environmentId
            },
            transaction,
            cancellationToken: cancellationToken));

        transaction.Commit();
        return await GetEnvironmentAsync(duplicateId, cancellationToken);
    }

    public async Task<bool> DeleteAsync(Guid environmentId, Guid userId, CancellationToken cancellationToken)
    {
        using var connection = connectionFactory.CreateConnection();
        connection.Open();
        using var transaction = connection.BeginTransaction();

        var source = await connection.QuerySingleOrDefaultAsync<EnvironmentSourceRow>(new CommandDefinition("""
            select id, organizationId, workspaceId, name, isDefault
            from environments
            where id = @EnvironmentId and isDeleted = 0;
            """,
            new { EnvironmentId = environmentId },
            transaction,
            cancellationToken: cancellationToken));
        if (source is null)
        {
            transaction.Rollback();
            return false;
        }

        await connection.ExecuteAsync(new CommandDefinition("""
            update environments
            set isDeleted = 1, modifiedOn = sysutcdatetime(), modifiedBy = @UserId
            where id = @EnvironmentId and isDeleted = 0;

            update environmentVariables
            set isDeleted = 1, modifiedOn = sysutcdatetime(), modifiedBy = @UserId
            where environmentId = @EnvironmentId and isDeleted = 0;
            """,
            new { EnvironmentId = environmentId, UserId = userId },
            transaction,
            cancellationToken: cancellationToken));

        if (source.IsDefault)
        {
            await connection.ExecuteAsync(new CommandDefinition("""
                update environments
                set isDefault = 1, modifiedOn = sysutcdatetime(), modifiedBy = @UserId
                where id = (
                    select top 1 id
                    from environments
                    where workspaceId = @WorkspaceId and isDeleted = 0
                    order by createdOn, name
                );
                """,
                new { source.WorkspaceId, UserId = userId },
                transaction,
                cancellationToken: cancellationToken));
        }

        transaction.Commit();
        return true;
    }

    public async Task<IReadOnlyList<EnvironmentVariableDto>> UpsertVariablesAsync(Guid environmentId, UpsertEnvironmentVariablesRequest request, Guid userId, CancellationToken cancellationToken)
    {
        using var connection = connectionFactory.CreateConnection();
        connection.Open();
        using var transaction = connection.BeginTransaction();

        var scope = await connection.QuerySingleAsync<(Guid OrganizationId, Guid WorkspaceId)>(new CommandDefinition(
            "select organizationId, workspaceId from environments where id = @EnvironmentId and isDeleted = 0;",
            new { EnvironmentId = environmentId },
            transaction,
            cancellationToken: cancellationToken));

        var existingValues = (await connection.QueryAsync<ExistingVariableRow>(new CommandDefinition("""
            select [key], [value], isSecret
            from environmentVariables
            where environmentId = @EnvironmentId and isDeleted = 0;
            """,
            new { EnvironmentId = environmentId },
            transaction,
            cancellationToken: cancellationToken)))
            .ToDictionary(row => row.Key, StringComparer.OrdinalIgnoreCase);

        await connection.ExecuteAsync(new CommandDefinition("""
            update environmentVariables
            set isDeleted = 1, modifiedOn = sysutcdatetime(), modifiedBy = @UserId
            where environmentId = @EnvironmentId and scope in ('Environment', 'LocalPrivate');
            """,
            new { EnvironmentId = environmentId, UserId = userId },
            transaction,
            cancellationToken: cancellationToken));

        var rows = request.Variables.Select(v => new
        {
            Id = Guid.NewGuid(),
            scope.OrganizationId,
            scope.WorkspaceId,
            EnvironmentId = environmentId,
            UserId = string.Equals(v.Scope, "LocalPrivate", StringComparison.OrdinalIgnoreCase) ? userId : (Guid?)null,
            Key = v.Key.Trim(),
            Value = ResolveSubmittedValue(v, existingValues),
            Scope = NormalizeScope(v.Scope),
            v.IsSecret,
            v.Enabled,
            CreatedBy = userId
        });

        await connection.ExecuteAsync(new CommandDefinition("""
            insert into environmentVariables
            (id, organizationId, workspaceId, environmentId, userId, [key], [value], scope, isSecret, enabled, createdOn, createdBy, isDeleted, versionNumber)
            values
            (@Id, @OrganizationId, @WorkspaceId, @EnvironmentId, @UserId, @Key, @Value, @Scope, @IsSecret, @Enabled, sysutcdatetime(), @CreatedBy, 0, 1);
            """,
            rows,
            transaction,
            cancellationToken: cancellationToken));

        await connection.ExecuteAsync(new CommandDefinition("""
            insert into environmentVersions (id, environmentId, snapshotJson, createdOn, createdBy, isDeleted)
            select newid(), @EnvironmentId,
                   (select [key], case when isSecret = 1 then '********' else [value] end as [value], scope, isSecret, enabled
                    from environmentVariables
                    where environmentId = @EnvironmentId and isDeleted = 0
                    for json path),
                   sysutcdatetime(), @UserId, 0;
            """, new { EnvironmentId = environmentId, UserId = userId }, transaction, cancellationToken: cancellationToken));

        transaction.Commit();

        return await GetVariablesAsync(environmentId, cancellationToken);
    }

    public async Task<IReadOnlyDictionary<string, string>> ResolveVariablesAsync(Guid workspaceId, Guid? collectionId, Guid? environmentId, Guid userId, CancellationToken cancellationToken)
    {
        using var connection = connectionFactory.CreateConnection();
        var rows = await connection.QueryAsync<VariableResolutionRow>(new CommandDefinition("""
            select [key], [value], scope,
                case scope
                    when 'Global' then 1
                    when 'Workspace' then 2
                    when 'Collection' then 3
                    when 'Environment' then 4
                    when 'LocalPrivate' then 5
                    else 0
                end as priority
            from environmentVariables
            where enabled = 1 and isDeleted = 0
                and workspaceId = @WorkspaceId
                and (collectionId is null or collectionId = @CollectionId)
                and (environmentId is null or environmentId = @EnvironmentId)
                and (userId is null or userId = @UserId)
            order by priority;
            """,
            new { WorkspaceId = workspaceId, CollectionId = collectionId, EnvironmentId = environmentId, UserId = userId },
            cancellationToken: cancellationToken));

        var variables = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in rows)
        {
            variables[row.Key] = row.Value ?? string.Empty;
        }

        return variables;
    }

    public async Task<IReadOnlyList<EnvironmentVariableDto>> GetVariablesAsync(Guid environmentId, CancellationToken cancellationToken)
    {
        using var connection = connectionFactory.CreateConnection();
        var rows = await connection.QueryAsync<EnvironmentVariableDto>(new CommandDefinition("""
            select id, [key],
                   case when isSecret = 1 then '********' else [value] end as [value],
                   scope, isSecret, enabled, createdOn, modifiedOn
            from environmentVariables
            where environmentId = @EnvironmentId and isDeleted = 0
            order by scope, [key];
            """, new { EnvironmentId = environmentId }, cancellationToken: cancellationToken));

        return rows.Select(v => v with { Value = v.IsSecret ? SensitiveDataMasker.MaskValue(v.Key, v.Value) : v.Value }).ToList();
    }

    private async Task<EnvironmentDto?> GetEnvironmentAsync(Guid environmentId, CancellationToken cancellationToken)
    {
        using var connection = connectionFactory.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<EnvironmentDto>(new CommandDefinition("""
            select e.id, e.workspaceId, e.name, e.isDefault,
                   count(ev.id) as variableCount,
                   sum(case when ev.isSecret = 1 then 1 else 0 end) as secretCount,
                   e.versionNumber, e.createdOn, e.modifiedOn
            from environments e
            left join environmentVariables ev on ev.environmentId = e.id and ev.isDeleted = 0
            where e.id = @EnvironmentId and e.isDeleted = 0
            group by e.id, e.workspaceId, e.name, e.isDefault, e.versionNumber, e.createdOn, e.modifiedOn;
            """,
            new { EnvironmentId = environmentId },
            cancellationToken: cancellationToken));
    }

    private static string NormalizeScope(string scope) =>
        string.Equals(scope, "LocalPrivate", StringComparison.OrdinalIgnoreCase) ? "LocalPrivate" : "Environment";

    private static string? ResolveSubmittedValue(EnvironmentVariableUpsertDto submitted, IReadOnlyDictionary<string, ExistingVariableRow> existingValues)
    {
        if (submitted.IsSecret
            && existingValues.TryGetValue(submitted.Key.Trim(), out var existing)
            && existing.IsSecret
            && IsMaskedPlaceholder(submitted.Value))
        {
            return existing.Value;
        }

        return submitted.Value;
    }

    private static bool IsMaskedPlaceholder(string? value) =>
        !string.IsNullOrEmpty(value) && value.All(character => character == '*');

    private sealed class VariableResolutionRow
    {
        public string Key { get; init; } = string.Empty;
        public string? Value { get; init; }
        public string Scope { get; init; } = string.Empty;
        public int Priority { get; init; }
    }

    private sealed class EnvironmentSourceRow
    {
        public Guid Id { get; init; }
        public Guid OrganizationId { get; init; }
        public Guid WorkspaceId { get; init; }
        public string Name { get; init; } = string.Empty;
        public bool IsDefault { get; init; }
    }

    private sealed class ExistingVariableRow
    {
        public string Key { get; init; } = string.Empty;
        public string? Value { get; init; }
        public bool IsSecret { get; init; }
    }
}
